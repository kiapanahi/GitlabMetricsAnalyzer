#!/usr/bin/env python3
# -*- coding: utf-8 -*-

"""
gitlab_ci_health_metrics.py (local CSV only)

Collects CI health metrics from a self-managed GitLab:
- Pipeline success rate
- Pipeline duration (mean/p50/p90)
- Pipeline queue time (mean/p50/p90) via job queued_duration or (job.started - job.created)
- Job outcomes (success/failed/canceled/skipped)
- Per-stage average job durations

Outputs:
- outputs/ci/<namespace__project>.csv        -> pipeline-level facts
- outputs/ci/<namespace__project>__stages.csv -> per-stage job duration averages
- outputs/ci/_summary.csv                    -> per-project rollups

Configuration (required in environment):
  TOMAN_GITLAB_API_URL   -> e.g., https://gitlab.example.com
  TOMAN_GITLAB_API_TOKEN -> personal access token with API scope

Optional flags:
  --group-path "parent/subgroup"
  --days 30
"""

from __future__ import annotations

import argparse
import csv
import datetime as dt
import os
import re
import sys
import time
from typing import Dict, Iterable, List, Optional, Tuple

import requests

# ------------------------------
# Helpers
# ------------------------------

ISO8601 = "%Y-%m-%dT%H:%M:%S.%fZ"

def parse_time(s: Optional[str]) -> Optional[dt.datetime]:
    if not s:
        return None
    try:
        return dt.datetime.strptime(s, ISO8601).replace(tzinfo=dt.timezone.utc)
    except ValueError:
        try:
            return dt.datetime.fromisoformat(s.replace("Z", "+00:00")).astimezone(dt.timezone.utc)
        except Exception:
            return None

def to_seconds(delta: Optional[dt.timedelta]) -> Optional[float]:
    return round(delta.total_seconds(), 3) if delta is not None else None

def quantiles(values: List[float], q: float) -> Optional[float]:
    if not values:
        return None
    vals = sorted(values)
    idx = max(0, min(len(vals) - 1, int(round((len(vals) - 1) * q))))
    return round(vals[idx], 3)

def avg(values: List[float]) -> Optional[float]:
    return round(sum(values)/len(values), 3) if values else None

def sanitize_filename(s: str) -> str:
    return re.sub(r"[^A-Za-z0-9._-]+", "_", s)

def ensure_outputs_dir() -> str:
    outdir = os.path.join(os.getcwd(), "outputs", "ci")
    os.makedirs(outdir, exist_ok=True)
    return outdir

# ------------------------------
# GitLab API client
# ------------------------------

class GitLab:
    def __init__(self, base_url: str, token: str, timeout: int = 30):
        self.base_url = base_url.rstrip("/")
        self.s = requests.Session()
        self.s.headers.update({"PRIVATE-TOKEN": token})
        self.timeout = timeout

    def _get(self, path: str, params: Optional[dict] = None) -> requests.Response:
        url = f"{self.base_url}{path}"
        for attempt in range(5):
            r = self.s.get(url, params=params, timeout=self.timeout)
            if r.status_code in (429, 502, 503, 504):
                time.sleep(1.5 * (attempt + 1))
                continue
            r.raise_for_status()
            return r
        r.raise_for_status()
        return r  # pragma: no cover

    def _paginate(self, path: str, params: Optional[dict] = None) -> Iterable[dict]:
        p = dict(params or {})
        p.setdefault("per_page", 100)
        page = 1
        while True:
            p["page"] = page
            r = self._get(path, p)
            items = r.json()
            if not isinstance(items, list):
                yield items
                return
            for it in items:
                yield it
            next_page = r.headers.get("X-Next-Page")
            if not next_page:
                break
            page = int(next_page)

    # Projects

    def list_projects_membership(self) -> List[dict]:
        return list(self._paginate("/api/v4/projects", {"membership": True, "order_by": "last_activity_at", "sort": "desc"}))

    def list_group_projects(self, group_path: str) -> List[dict]:
        group_enc = requests.utils.quote(group_path, safe="")
        return list(self._paginate(f"/api/v4/groups/{group_enc}/projects", {"include_subgroups": True, "order_by": "last_activity_at", "sort": "desc"}))

    # Pipelines

    def list_pipelines(self, project_id: int, page_params: Optional[dict] = None) -> List[dict]:
        return list(self._paginate(f"/api/v4/projects/{project_id}/pipelines", page_params or {"order_by":"updated_at","sort":"desc"}))

    def get_pipeline(self, project_id: int, pipeline_id: int) -> dict:
        return self._get(f"/api/v4/projects/{project_id}/pipelines/{pipeline_id}").json()

    def get_pipeline_jobs(self, project_id: int, pipeline_id: int) -> List[dict]:
        return list(self._paginate(f"/api/v4/projects/{project_id}/pipelines/{pipeline_id}/jobs", {"per_page": 100}))

# ------------------------------
# Collection
# ------------------------------

def compute_job_queue_seconds(job: dict) -> Optional[float]:
    qd = job.get("queued_duration")
    if isinstance(qd, (int, float)):
        return float(qd)
    created = parse_time(job.get("created_at"))
    started = parse_time(job.get("started_at"))
    if created and started:
        return to_seconds(started - created)
    return None

def compute_job_duration_seconds(job: dict) -> Optional[float]:
    dur = job.get("duration")
    if isinstance(dur, (int, float)):
        return float(dur)
    started = parse_time(job.get("started_at"))
    finished = parse_time(job.get("finished_at"))
    if started and finished:
        return to_seconds(finished - started)
    return None

def collect_for_project(gl: GitLab, project: dict, since: dt.datetime) -> Tuple[List[dict], dict, List[Tuple[str,int,float]]]:
    pid = project["id"]
    ppath = project["path_with_namespace"]
    default_branch = project.get("default_branch")

    pipelines = gl.list_pipelines(pid)
    facts: List[dict] = []
    created_after = since

    all_durations: List[float] = []
    all_queues: List[float] = []
    all_statuses: List[str] = []

    default_durations: List[float] = []
    default_statuses: List[str] = []

    stage_durations: Dict[str, List[float]] = {}

    for p in pipelines:
        try:
            created_at = parse_time(p.get("created_at")) or parse_time(p.get("updated_at"))
            if not created_at or created_at < created_after:
                continue

            pipeline_id = p["id"]
            ref = p.get("ref") or ""
            status = (p.get("status") or "").lower()
            p_full = gl.get_pipeline(pid, pipeline_id)

            started_at = parse_time(p_full.get("started_at"))
            finished_at = parse_time(p_full.get("finished_at"))
            duration = p_full.get("duration")
            if isinstance(duration, (int, float)):
                dur_sec = float(duration)
            else:
                dur_sec = to_seconds((finished_at - started_at) if started_at and finished_at else None)

            jobs = gl.get_pipeline_jobs(pid, pipeline_id)
            jobs_total = len(jobs)
            jobs_failed = sum(1 for j in jobs if (j.get("status") or "").lower() == "failed")
            jobs_success = sum(1 for j in jobs if (j.get("status") or "").lower() == "success")
            jobs_canceled = sum(1 for j in jobs if (j.get("status") or "").lower() == "canceled")
            jobs_skipped = sum(1 for j in jobs if (j.get("status") or "").lower() == "skipped")

            job_queues = [q for q in (compute_job_queue_seconds(j) for j in jobs) if q is not None]
            queue_mean = avg(job_queues)

            for j in jobs:
                stg = j.get("stage") or "unknown"
                jd = compute_job_duration_seconds(j)
                if jd is not None:
                    stage_durations.setdefault(stg, []).append(jd)

            facts.append({
                "project_path": ppath,
                "pipeline_id": pipeline_id,
                "ref": ref,
                "is_default_branch": 1 if (default_branch and ref == default_branch) else 0,
                "status": status,
                "created_at": created_at.isoformat(),
                "started_at": started_at.isoformat() if started_at else "",
                "finished_at": finished_at.isoformat() if finished_at else "",
                "duration_sec": dur_sec or "",
                "queue_mean_sec": queue_mean or "",
                "jobs_total": jobs_total,
                "jobs_success": jobs_success,
                "jobs_failed": jobs_failed,
                "jobs_canceled": jobs_canceled,
                "jobs_skipped": jobs_skipped,
            })

            if dur_sec is not None:
                all_durations.append(dur_sec)
                if default_branch and ref == default_branch:
                    default_durations.append(dur_sec)
            if queue_mean is not None:
                all_queues.append(queue_mean)
            all_statuses.append(status)
            if default_branch and ref == default_branch:
                default_statuses.append(status)

        except Exception as e:
            print(f"[WARN] project {ppath} pipeline {p.get('id')} failed: {e}", file=sys.stderr)
            continue

    def rate(statuses: List[str], good: str = "success") -> Optional[float]:
        if not statuses:
            return None
        return round(sum(1 for s in statuses if s == good) / len(statuses), 4)

    rollup = {
        "project_path": ppath,
        "pipelines_total": len(all_statuses),
        "pipelines_success": sum(1 for s in all_statuses if s == "success"),
        "success_rate": rate(all_statuses) or "",
        "duration_mean_sec": avg(all_durations) or "",
        "duration_p50_sec": quantiles(all_durations, 0.50) or "",
        "duration_p90_sec": quantiles(all_durations, 0.90) or "",
        "queue_mean_sec": avg(all_queues) or "",
        "queue_p50_sec": quantiles(all_queues, 0.50) or "",
        "queue_p90_sec": quantiles(all_queues, 0.90) or "",
        "default_branch": default_branch or "",
        "default_success_rate": rate(default_statuses) or "",
        "default_duration_p50_sec": quantiles(default_durations, 0.50) or "",
        "default_duration_p90_sec": quantiles(default_durations, 0.90) or "",
    }

    stage_rows: List[Tuple[str,int,float]] = []
    for stg, durs in stage_durations.items():
        if not durs:
            continue
        stage_rows.append((stg, len(durs), avg(durs) or 0.0))

    return facts, rollup, stage_rows

# ------------------------------
# IO
# ------------------------------

def write_pipeline_csv(outdir: str, facts: List[dict], project_path: str) -> str:
    fname = sanitize_filename(project_path.replace("/", "__")) + ".csv"
    fpath = os.path.join(outdir, fname)
    with open(fpath, "w", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        w.writerow([
            "project_path","pipeline_id","ref","is_default_branch","status",
            "created_at","started_at","finished_at","duration_sec","queue_mean_sec",
            "jobs_total","jobs_success","jobs_failed","jobs_canceled","jobs_skipped"
        ])
        for x in facts:
            w.writerow([
                x["project_path"], x["pipeline_id"], x["ref"], x["is_default_branch"], x["status"],
                x["created_at"], x["started_at"], x["finished_at"], x["duration_sec"], x["queue_mean_sec"],
                x["jobs_total"], x["jobs_success"], x["jobs_failed"], x["jobs_canceled"], x["jobs_skipped"]
            ])
    return fpath

def write_stage_csv(outdir: str, stages: List[Tuple[str,int,float]], project_path: str) -> str:
    fname = sanitize_filename(project_path.replace("/", "__")) + "__stages.csv"
    fpath = os.path.join(outdir, fname)
    with open(fpath, "w", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        w.writerow(["stage","jobs_count","avg_job_duration_sec"])
        for stg, jobs_count, avg_sec in sorted(stages, key=lambda x: x[0]):
            w.writerow([stg, jobs_count, avg_sec])
    return fpath

def write_summary_csv(outdir: str, rollups: List[dict]) -> str:
    fpath = os.path.join(outdir, "_summary.csv")
    with open(fpath, "w", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        w.writerow([
            "project_path","pipelines_total","pipelines_success","success_rate",
            "duration_mean_sec","duration_p50_sec","duration_p90_sec",
            "queue_mean_sec","queue_p50_sec","queue_p90_sec",
            "default_branch","default_success_rate","default_duration_p50_sec","default_duration_p90_sec"
        ])
        for r in rollups:
            w.writerow([
                r["project_path"], r["pipelines_total"], r["pipelines_success"], r["success_rate"],
                r["duration_mean_sec"], r["duration_p50_sec"], r["duration_p90_sec"],
                r["queue_mean_sec"], r["queue_p50_sec"], r["queue_p90_sec"],
                r["default_branch"], r["default_success_rate"], r["default_duration_p50_sec"], r["default_duration_p90_sec"]
            ])
    return fpath

# ------------------------------
# Main
# ------------------------------

def main():
    base_url = os.getenv("TOMAN_GITLAB_API_URL")
    token = os.getenv("TOMAN_GITLAB_API_TOKEN")
    if not base_url or not token:
        print("Environment variables TOMAN_GITLAB_API_URL and TOMAN_GITLAB_API_TOKEN must be set.", file=sys.stderr)
        sys.exit(2)

    parser = argparse.ArgumentParser(description="Collect GitLab CI health metrics (local CSV outputs only).")
    parser.add_argument("--group-path", help="Optional GitLab group path to limit projects, e.g., 'parent/subgroup'")
    parser.add_argument("--days", type=int, default=30, help="Lookback window in days (default 30)")
    args = parser.parse_args()

    gl = GitLab(base_url, token)
    outdir = ensure_outputs_dir()
    since = dt.datetime.now(tz=dt.timezone.utc) - dt.timedelta(days=args.days)

    # Discover projects
    if args.group_path:
        projects = gl.list_group_projects(args.group_path)
    else:
        projects = gl.list_projects_membership()

    rollups: List[dict] = []
    for p in projects:
        ppath = p["path_with_namespace"]
        print(f"[INFO] Project: {ppath}")
        facts, rollup, stages = collect_for_project(gl, p, since)
        if facts:
            pipelines_csv = write_pipeline_csv(outdir, facts, ppath)
            print(f"[INFO]   wrote {pipelines_csv}")
            if stages:
                stage_csv = write_stage_csv(outdir, stages, ppath)
                print(f"[INFO]   wrote {stage_csv}")
            rollups.append(rollup)
        else:
            print(f"[INFO]   No pipelines in lookback window; skipping.", file=sys.stderr)

    if rollups:
        sum_path = write_summary_csv(outdir, rollups)
        print(f"[INFO] Wrote CI summary: {sum_path}")
    else:
        print("[INFO] No CI data found for any project in the window.")

if __name__ == "__main__":
    main()
