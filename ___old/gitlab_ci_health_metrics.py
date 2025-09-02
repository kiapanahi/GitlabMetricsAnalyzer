#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
GitLab CI Health Metrics (Concurrent, Local CSV Only)
=====================================================

This module collects CI health metrics from a self-managed GitLab and writes
pipeline facts, per-stage aggregates, and per-project rollups. It runs
**concurrently** across projects and pipelines to reduce wall-clock time.

Environment (required)
----------------------
- TOMAN_GITLAB_API_URL   : Base URL of your GitLab, e.g. https://gitlab.example.com
- TOMAN_GITLAB_API_TOKEN : Personal Access Token with API scope

Command-line options
--------------------
- --group-path "parent/subgroup" : Limit to a group (includes subgroups)
- --days 30                      : Lookback window (default 30)
- --workers 8                    : Project-level concurrency (default 8)
- --per-project-workers 4        : Per-project pipeline concurrency (default 4)

Outputs
-------
- outputs/ci/<namespace__project>.csv          : Pipeline-level facts
- outputs/ci/<namespace__project>__stages.csv  : Per-stage avg job durations
- outputs/ci/_summary.csv                      : Project rollups

Metrics
-------
Per pipeline:
- Status, timestamps, duration (sec) [API duration or finished - started]
- Mean queue time (sec) across jobs [queued_duration or started - created]
- Job outcome counts (success/failed/canceled/skipped)

Per stage:
- Average job duration (sec) with jobs_count

Per project (rollups):
- Pipelines total/success and success rate
- Duration mean/p50/p90, Queue mean/p50/p90
- Default branch success rate and duration p50/p90 only for default ref

Design Notes
------------
- Uses ThreadPoolExecutor for:
  * Project-level fan-out
  * Per-project pipeline fan-out
- Each worker instantiates its own GitLab client (requests.Session).
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
from concurrent.futures import ThreadPoolExecutor, as_completed

import requests

ISO8601 = "%Y-%m-%dT%H:%M:%S.%fZ"


def parse_time(s: Optional[str]) -> Optional[dt.datetime]:
    """Parse a GitLab ISO8601 timestamp into an aware datetime or return None."""
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
    """Convert a timedelta to seconds rounded to milliseconds; None if delta is None."""
    return round(delta.total_seconds(), 3) if delta is not None else None


def quantiles(values: List[float], q: float) -> Optional[float]:
    """Return an inclusive nearest-rank quantile for a list of floats."""
    if not values:
        return None
    vals = sorted(values)
    idx = max(0, min(len(vals) - 1, int(round((len(vals) - 1) * q))))
    return round(vals[idx], 3)


def avg(values: List[float]) -> Optional[float]:
    """Return the arithmetic mean rounded to milliseconds or None for empty input."""
    return round(sum(values) / len(values), 3) if values else None


def sanitize_filename(s: str) -> str:
    """Sanitize an arbitrary string for safe use as a filename."""
    return re.sub(r"[^A-Za-z0-9._-]+", "_", s)


def ensure_outputs_dir() -> str:
    """Create and return the `outputs/ci` directory path if not present."""
    outdir = os.path.join(os.getcwd(), "outputs", "ci")
    os.makedirs(outdir, exist_ok=True)
    return outdir


class GitLab:
    """Minimal GitLab API client using requests with simple pagination.

    Each instance holds its own requests.Session to avoid cross-thread sharing.
    """

    def __init__(self, base_url: str, token: str, timeout: int = 30):
        """Initialize client with base URL, private token, and request timeout."""
        self.base_url = base_url.rstrip("/")
        self.s = requests.Session()
        self.s.headers.update({"PRIVATE-TOKEN": token})
        self.timeout = timeout

    def _get(self, path: str, params: Optional[dict] = None) -> requests.Response:
        """Perform a GET with basic retry on transient HTTP 429/50x codes."""
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
        """Yield items across pages for list-returning API endpoints."""
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

    # ---- Project endpoints ----

    def list_projects_membership(self) -> List[dict]:
        """Return projects where the token holder has membership."""
        return list(self._paginate("/api/v4/projects", {"membership": True, "order_by": "last_activity_at", "sort": "desc"}))

    def list_group_projects(self, group_path: str) -> List[dict]:
        """Return all projects in a group (including subgroups)."""
        group_enc = requests.utils.quote(group_path, safe="")
        return list(self._paginate(f"/api/v4/groups/{group_enc}/projects", {"include_subgroups": True, "order_by": "last_activity_at", "sort": "desc"}))

    # ---- Pipeline endpoints ----

    def list_pipelines(self, project_id: int) -> List[dict]:
        """Return recent pipelines for a project ordered by updated_at desc."""
        return list(self._paginate(f"/api/v4/projects/{project_id}/pipelines", {"order_by": "updated_at", "sort": "desc"}))

    def get_pipeline(self, project_id: int, pipeline_id: int) -> dict:
        """Return details for a specific pipeline."""
        return self._get(f"/api/v4/projects/{project_id}/pipelines/{pipeline_id}").json()

    def get_pipeline_jobs(self, project_id: int, pipeline_id: int) -> List[dict]:
        """Return jobs for a specific pipeline."""
        return list(self._paginate(f"/api/v4/projects/{project_id}/pipelines/{pipeline_id}/jobs", {"per_page": 100}))


def compute_job_queue_seconds(job: dict) -> Optional[float]:
    """Compute a job's queue time in seconds using queued_duration or started - created."""
    qd = job.get("queued_duration")
    if isinstance(qd, (int, float)):
        return float(qd)
    created = parse_time(job.get("created_at"))
    started = parse_time(job.get("started_at"))
    if created and started:
        return to_seconds(started - created)
    return None


def compute_job_duration_seconds(job: dict) -> Optional[float]:
    """Compute a job's duration in seconds using API `duration` or finished - started."""
    dur = job.get("duration")
    if isinstance(dur, (int, float)):
        return float(dur)
    started = parse_time(job.get("started_at"))
    finished = parse_time(job.get("finished_at"))
    if started and finished:
        return to_seconds(finished - started)
    return None


def process_single_pipeline(base_url: str, token: str, project: dict, p: dict,
                            since: dt.datetime) -> Optional[Tuple[dict, Dict[str, List[float]]]]:
    """Fetch details and jobs for a pipeline; return (fact_row, stage_durations) or None."""
    try:
        gl = GitLab(base_url, token)
        pid = project["id"]
        ppath = project["path_with_namespace"]
        default_branch = project.get("default_branch")

        created_at = parse_time(
            p.get("created_at")) or parse_time(p.get("updated_at"))
        if not created_at or created_at < since:
            return None

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
            dur_sec = to_seconds((finished_at - started_at)
                                 if started_at and finished_at else None)

        jobs = gl.get_pipeline_jobs(pid, pipeline_id)
        jobs_total = len(jobs)
        jobs_failed = sum(1 for j in jobs if (
            j.get("status") or "").lower() == "failed")
        jobs_success = sum(1 for j in jobs if (
            j.get("status") or "").lower() == "success")
        jobs_canceled = sum(1 for j in jobs if (
            j.get("status") or "").lower() == "canceled")
        jobs_skipped = sum(1 for j in jobs if (
            j.get("status") or "").lower() == "skipped")

        job_queues = [q for q in (compute_job_queue_seconds(j)
                                  for j in jobs) if q is not None]
        queue_mean = avg(job_queues)

        stage_durations: Dict[str, List[float]] = {}
        for j in jobs:
            stg = j.get("stage") or "unknown"
            jd = compute_job_duration_seconds(j)
            if jd is not None:
                stage_durations.setdefault(stg, []).append(jd)

        fact = {
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
        }
        return fact, stage_durations
    except Exception as e:
        ppath = project.get("path_with_namespace", "?")
        print(
            f"[WARN] project {ppath} pipeline {p.get('id')} failed: {e}", file=sys.stderr)
        return None


def collect_for_project(base_url: str, token: str, project: dict, since: dt.datetime,
                        per_project_workers: int) -> Tuple[List[dict], dict, List[Tuple[str, int, float]]]:
    """Collect pipeline facts, project rollup, and per-stage rows for a project concurrently."""
    pid = project["id"]
    ppath = project["path_with_namespace"]
    default_branch = project.get("default_branch")

    gl = GitLab(base_url, token)
    pipelines = gl.list_pipelines(pid)

    facts: List[dict] = []
    all_durations: List[float] = []
    all_queues: List[float] = []
    all_statuses: List[str] = []
    default_durations: List[float] = []
    default_statuses: List[str] = []
    stage_durations: Dict[str, List[float]] = {}

    with ThreadPoolExecutor(max_workers=max(1, per_project_workers)) as ex:
        futures = [ex.submit(process_single_pipeline, base_url,
                             token, project, p, since) for p in pipelines]
        for fut in as_completed(futures):
            res = fut.result()
            if not res:
                continue
            fact, stage_map = res
            facts.append(fact)

            dur = fact["duration_sec"]
            if isinstance(dur, (int, float)):
                all_durations.append(dur)
                if fact["is_default_branch"] == 1:
                    default_durations.append(dur)
            queue = fact["queue_mean_sec"]
            if isinstance(queue, (int, float)):
                all_queues.append(queue)
            all_statuses.append(fact["status"])
            if fact["is_default_branch"] == 1:
                default_statuses.append(fact["status"])

            for stg, durs in stage_map.items():
                if durs:
                    stage_durations.setdefault(stg, []).extend(durs)

    def rate(statuses: List[str], good: str = "success") -> Optional[float]:
        return round(sum(1 for s in statuses if s == good) / len(statuses), 4) if statuses else None

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

    stage_rows: List[Tuple[str, int, float]] = []
    for stg, durs in stage_durations.items():
        if not durs:
            continue
        stage_rows.append((stg, len(durs), avg(durs) or 0.0))

    return facts, rollup, stage_rows


def write_pipeline_csv(outdir: str, facts: List[dict], project_path: str) -> str:
    """Write pipeline facts for a project to CSV; return file path."""
    fname = sanitize_filename(project_path.replace("/", "__")) + ".csv"
    fpath = os.path.join(outdir, fname)
    with open(fpath, "w", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        w.writerow([
            "project_path", "pipeline_id", "ref", "is_default_branch", "status",
            "created_at", "started_at", "finished_at", "duration_sec", "queue_mean_sec",
            "jobs_total", "jobs_success", "jobs_failed", "jobs_canceled", "jobs_skipped"
        ])
        for x in facts:
            w.writerow([
                x["project_path"], x["pipeline_id"], x["ref"], x["is_default_branch"], x["status"],
                x["created_at"], x["started_at"], x["finished_at"], x["duration_sec"], x["queue_mean_sec"],
                x["jobs_total"], x["jobs_success"], x["jobs_failed"], x["jobs_canceled"], x["jobs_skipped"]
            ])
    return fpath


def write_stage_csv(outdir: str, stages: List[Tuple[str, int, float]], project_path: str) -> str:
    """Write per-stage average job duration rows for a project to CSV."""
    fname = sanitize_filename(project_path.replace("/", "__")) + "__stages.csv"
    fpath = os.path.join(outdir, fname)
    with open(fpath, "w", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        w.writerow(["stage", "jobs_count", "avg_job_duration_sec"])
        for stg, jobs_count, avg_sec in sorted(stages, key=lambda x: x[0]):
            w.writerow([stg, jobs_count, avg_sec])
    return fpath


def rget(d: dict, key: str, default=None):
    """Return dict value or default (helper for safe sort key)."""
    return d.get(key, default)


def write_summary_csv(outdir: str, rollups: List[dict]) -> str:
    """Write the per-project rollup CSV for the CI portfolio."""
    fpath = os.path.join(outdir, "_summary.csv")
    with open(fpath, "w", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        w.writerow([
            "project_path", "pipelines_total", "pipelines_success", "success_rate",
            "duration_mean_sec", "duration_p50_sec", "duration_p90_sec",
            "queue_mean_sec", "queue_p50_sec", "queue_p90_sec",
            "default_branch", "default_success_rate", "default_duration_p50_sec", "default_duration_p90_sec"
        ])
        for r in sorted(rollups, key=lambda x: rget(x, "project_path", "").lower()):
            w.writerow([
                r["project_path"], r["pipelines_total"], r["pipelines_success"], r["success_rate"],
                r["duration_mean_sec"], r["duration_p50_sec"], r["duration_p90_sec"],
                r["queue_mean_sec"], r["queue_p50_sec"], r["queue_p90_sec"],
                r["default_branch"], r["default_success_rate"], r["default_duration_p50_sec"], r["default_duration_p90_sec"]
            ])
    return fpath


def main() -> None:
    """Entry point: parse args, fan out across projects/pipelines, write CSVs."""
    base_url = os.getenv("TOMAN_GITLAB_API_URL")
    token = os.getenv("TOMAN_GITLAB_API_TOKEN")
    if not base_url or not token:
        print("Environment variables TOMAN_GITLAB_API_URL and TOMAN_GITLAB_API_TOKEN must be set.", file=sys.stderr)
        sys.exit(2)

    parser = argparse.ArgumentParser(
        description="Collect GitLab CI health metrics concurrently (local CSV outputs only).")
    parser.add_argument(
        "--group-path", help="Optional GitLab group path to limit projects, e.g., 'parent/subgroup'")
    parser.add_argument("--days", type=int, default=30,
                        help="Lookback window in days (default 30)")
    parser.add_argument("--workers", type=int, default=8,
                        help="Project-level concurrency (default 8)")
    parser.add_argument("--per-project-workers", type=int, default=4,
                        help="Per-project pipeline concurrency (default 4)")
    args = parser.parse_args()

    outdir = ensure_outputs_dir()
    since = dt.datetime.now(tz=dt.timezone.utc) - dt.timedelta(days=args.days)

    # Discover projects
    gl_discovery = GitLab(base_url, token)
    if args.group_path:
        projects = gl_discovery.list_group_projects(args.group_path)
    else:
        projects = gl_discovery.list_projects_membership()

    rollups: List[dict] = []
    futures = []
    with ThreadPoolExecutor(max_workers=max(1, args.workers)) as ex:
        for p in projects:
            futures.append(ex.submit(collect_for_project, base_url,
                           token, p, since, args.per_project_workers))

        for fut in as_completed(futures):
            try:
                facts, rollup, stages = fut.result()
                if facts:
                    pipelines_csv = write_pipeline_csv(
                        outdir, facts, rollup["project_path"])
                    print(f"[INFO] wrote {pipelines_csv}")
                    if stages:
                        stage_csv = write_stage_csv(
                            outdir, stages, rollup["project_path"])
                        print(f"[INFO] wrote {stage_csv}")
                    rollups.append(rollup)
                else:
                    print(
                        f"[INFO] no pipelines in window for a project; skipped.", file=sys.stderr)
            except Exception as e:
                print(
                    f"[WARN] project processing failed: {e}", file=sys.stderr)

    if rollups:
        sum_path = write_summary_csv(outdir, rollups)
        print(f"[INFO] Wrote CI summary: {sum_path}")
    else:
        print("[INFO] No CI data found for any project in the window.")


if __name__ == "__main__":
    main()
