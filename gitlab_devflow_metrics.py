#!/usr/bin/env python3
# -*- coding: utf-8 -*-

"""
gitlab_devflow_metrics.py (local CSV only)

Collects Developer Flow metrics from a self-managed GitLab:
- Mean/Median/P90 Time to Merge (MTTM)
- Time to First Review (TTFR)
- Review Rounds (heuristic)
- PR Size (files changed buckets)

Outputs (local only):
- outputs/<namespace__project>.csv   -> MR-level facts
- outputs/_summary.csv               -> per-project rollups

Configuration (required):
- Environment variables must be set in your user profile:
  TOMAN_GITLAB_API_URL   -> e.g., https://gitlab.example.com
  TOMAN_GITLAB_API_TOKEN -> personal access token with API scope

Optional:
- --group-path "parent/subgroup" to restrict projects
- --days 90 to adjust lookback window
"""

from __future__ import annotations
from dataclasses import dataclass

import argparse
import csv
import datetime as dt
import os
import re
import sys
import time
from typing import Iterable, List, Optional, Tuple

import requests

# ------------------------------
# Helpers
# ------------------------------

ISO8601 = "%Y-%m-%dT%H:%M:%S.%fZ"  # GitLab returns UTC in this format


def parse_time(s: str) -> dt.datetime:
    """
    Parse a string timestamp into a datetime object with UTC timezone.

    This function handles different ISO 8601 timestamp formats that might be 
    returned by GitLab API, including timestamps with and without microseconds.

    Parameters
    ----------
    s : str
        A timestamp string in ISO 8601 format.

    Returns
    -------
    datetime.datetime
        A datetime object with UTC timezone.

    Raises
    ------
    ValueError
        If the string cannot be parsed using any of the supported formats.
    """
    # GitLab sometimes returns without microseconds
    try:
        return dt.datetime.strptime(s, ISO8601).replace(tzinfo=dt.timezone.utc)
    except ValueError:
        return dt.datetime.fromisoformat(s.replace("Z", "+00:00")).astimezone(dt.timezone.utc)


def to_hours(delta: dt.timedelta) -> float:
    """Convert a timedelta object to hours.

    Args:
        delta (dt.timedelta): The timedelta object to convert.

    Returns:
        float: The equivalent time in hours, rounded to 3 decimal places.
    """
    return round(delta.total_seconds() / 3600.0, 3)


def quantiles(values: List[float], q: float) -> Optional[float]:
    if not values:
        return None
    vals = sorted(values)
    # inclusive nearest-rank
    idx = max(0, min(len(vals) - 1, int(round((len(vals) - 1) * q))))
    return round(vals[idx], 3)


def sanitize_filename(s: str) -> str:
    return re.sub(r"[^A-Za-z0-9._-]+", "_", s)


def ensure_outputs_dir() -> str:
    outdir = os.path.join(os.getcwd(), "outputs")
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

    # Merge Requests

    def list_merge_requests(self, project_id: int, state: str, updated_after: Optional[str] = None) -> List[dict]:
        params = {"state": state, "scope": "all",
                  "order_by": "updated_at", "sort": "desc", "per_page": 100}
        if updated_after:
            params["updated_after"] = updated_after
        return list(self._paginate(f"/api/v4/projects/{project_id}/merge_requests", params))

    def get_merge_request(self, project_id: int, iid: int) -> dict:
        return self._get(f"/api/v4/projects/{project_id}/merge_requests/{iid}").json()

    def get_merge_request_notes(self, project_id: int, iid: int) -> List[dict]:
        return list(self._paginate(f"/api/v4/projects/{project_id}/merge_requests/{iid}/notes", {"sort": "asc"}))

    def get_merge_request_commits(self, project_id: int, iid: int) -> List[dict]:
        return list(self._paginate(f"/api/v4/projects/{project_id}/merge_requests/{iid}/commits", {"per_page": 100}))

    def get_merge_request_changes(self, project_id: int, iid: int) -> dict:
        return self._get(f"/api/v4/projects/{project_id}/merge_requests/{iid}/changes").json()

# ------------------------------
# Data Structures
# ------------------------------


@dataclass
class MRFact:
    project_id: int
    project_path: str
    mr_id: int
    mr_iid: int
    title: str
    author_username: str
    created_at: dt.datetime
    merged_at: dt.datetime
    start_time: dt.datetime  # created_at or "marked ready" time
    time_to_merge_h: Optional[float]
    time_to_first_review_h: Optional[float]
    review_rounds: int
    files_changed: Optional[int]


@dataclass
class ProjectRollup:
    project_id: int
    project_path: str
    mrs_merged: int = 0
    mttm_mean_h: Optional[float] = None
    mttm_p50_h: Optional[float] = None
    mttm_p90_h: Optional[float] = None
    ttfr_mean_h: Optional[float] = None
    ttfr_p50_h: Optional[float] = None
    ttfr_p90_h: Optional[float] = None
    review_rounds_avg: Optional[float] = None
    size_xs: int = 0  # ≤3 files
    size_s: int = 0   # 4–10 files
    size_m: int = 0   # 11–25 files
    size_l: int = 0   # 26–50 files
    size_xl: int = 0  # >50 files

# ------------------------------
# Core logic
# ------------------------------


READY_PATTERNS = [
    "marked this merge request as ready",
    "marked this merge request as ready to merge",
]
DRAFT_PATTERNS = [
    "marked this merge request as draft",
    "marked this merge request as work in progress",
]


def find_ready_time(created_at: dt.datetime, notes: List[dict]) -> dt.datetime:
    ready_time = created_at
    for n in notes:
        if not n.get("system"):
            continue
        b = (n.get("body") or "").lower()
        t = parse_time(n["created_at"])
        if any(p in b for p in READY_PATTERNS):
            ready_time = t
        elif any(p in b for p in DRAFT_PATTERNS):
            pass
    return ready_time


def compute_ttfr(created_at: dt.datetime, mr_author: str, notes: List[dict]) -> Optional[float]:
    for n in notes:
        if n.get("system"):
            continue
        author = (n.get("author") or {}).get("username")
        if author and author != mr_author:
            t = parse_time(n["created_at"])
            if t >= created_at:
                return to_hours(t - created_at)
    return None


def compute_review_rounds(mr_author: str, notes: List[dict], commits: List[dict], start_time: dt.datetime, merged_at: dt.datetime) -> int:
    events: List[Tuple[dt.datetime, str]] = []
    for n in notes:
        if n.get("system"):
            continue
        author = (n.get("author") or {}).get("username")
        if author and author != mr_author:
            t = parse_time(n["created_at"])
            if start_time <= t <= merged_at:
                events.append((t, "review"))
    for c in commits:
        t = parse_time(c["created_at"])
        if start_time <= t <= merged_at:
            events.append((t, "commit"))
    events.sort(key=lambda x: x[0])

    rounds = 0
    awaiting_commit = False
    for t, kind in events:
        if kind == "review":
            awaiting_commit = True
        elif kind == "commit" and awaiting_commit:
            rounds += 1
            awaiting_commit = False
    return rounds


def size_bucket(files_changed: Optional[int]) -> str:
    if files_changed is None:
        return "unknown"
    if files_changed <= 3:
        return "xs"
    if files_changed <= 10:
        return "s"
    if files_changed <= 25:
        return "m"
    if files_changed <= 50:
        return "l"
    return "xl"


def collect_for_project(gl: GitLab, project: dict, since: dt.datetime) -> Tuple[List[MRFact], ProjectRollup]:
    pid = project["id"]
    ppath = project["path_with_namespace"]
    merged_mrs = gl.list_merge_requests(
        pid, state="merged", updated_after=since.isoformat())
    facts: List[MRFact] = []
    mttm_list: List[float] = []
    ttfr_list: List[float] = []
    review_rounds_list: List[int] = []
    size_counts = {"xs": 0, "s": 0, "m": 0, "l": 0, "xl": 0}

    for mr in merged_mrs:
        try:
            iid = mr["iid"]
            mr_full = gl.get_merge_request(pid, iid)
            created_at = parse_time(mr_full["created_at"])
            merged_at = parse_time(mr_full["merged_at"]) if mr_full.get(
                "merged_at") else None
            if merged_at is None or merged_at < since:
                continue

            notes = gl.get_merge_request_notes(pid, iid)
            commits = gl.get_merge_request_commits(pid, iid)
            changes = gl.get_merge_request_changes(pid, iid)
            files_changed = len(changes.get("changes", []))

            author_username = (mr_full.get("author") or {}
                               ).get("username") or "unknown"
            start_time = find_ready_time(created_at, notes)
            ttm_h = to_hours(
                merged_at - start_time) if merged_at and start_time else None
            ttfr_h = compute_ttfr(created_at, author_username, notes)

            rounds = compute_review_rounds(
                author_username, notes, commits, start_time, merged_at)

            facts.append(MRFact(
                project_id=pid,
                project_path=ppath,
                mr_id=mr_full["id"],
                mr_iid=iid,
                title=mr_full.get("title") or "",
                author_username=author_username,
                created_at=created_at,
                merged_at=merged_at,
                start_time=start_time,
                time_to_merge_h=ttm_h,
                time_to_first_review_h=ttfr_h,
                review_rounds=rounds,
                files_changed=files_changed,
            ))

            if ttm_h is not None:
                mttm_list.append(ttm_h)
            if ttfr_h is not None:
                ttfr_list.append(ttfr_h)
            review_rounds_list.append(rounds)
            size_counts[size_bucket(files_changed)] += 1

        except Exception as e:
            print(
                f"[WARN] project {ppath} MR {mr.get('iid')} failed: {e}", file=sys.stderr)
            continue

    def avg(nums: List[float]) -> Optional[float]:
        return round(sum(nums)/len(nums), 3) if nums else None

    rollup = ProjectRollup(
        project_id=pid,
        project_path=ppath,
        mrs_merged=len(facts),
        mttm_mean_h=avg(mttm_list),
        mttm_p50_h=quantiles(mttm_list, 0.50),
        mttm_p90_h=quantiles(mttm_list, 0.90),
        ttfr_mean_h=avg(ttfr_list),
        ttfr_p50_h=quantiles(ttfr_list, 0.50),
        ttfr_p90_h=quantiles(ttfr_list, 0.90),
        review_rounds_avg=avg(review_rounds_list),
        size_xs=size_counts["xs"],
        size_s=size_counts["s"],
        size_m=size_counts["m"],
        size_l=size_counts["l"],
        size_xl=size_counts["xl"],
    )
    return facts, rollup

# ------------------------------
# IO
# ------------------------------


def write_project_csv(outdir: str, facts: List[MRFact], project_path: str) -> str:
    fname = sanitize_filename(project_path.replace("/", "__")) + ".csv"
    fpath = os.path.join(outdir, fname)
    with open(fpath, "w", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        w.writerow(["project_path", "mr_iid", "title", "author", "created_at", "ready_or_created_at",
                   "merged_at", "time_to_merge_h", "time_to_first_review_h", "review_rounds", "files_changed"])
        for x in facts:
            w.writerow([
                x.project_path,
                x.mr_iid,
                x.title,
                x.author_username,
                x.created_at.isoformat(),
                x.start_time.isoformat(),
                x.merged_at.isoformat(),
                x.time_to_merge_h if x.time_to_merge_h is not None else "",
                x.time_to_first_review_h if x.time_to_first_review_h is not None else "",
                x.review_rounds,
                x.files_changed if x.files_changed is not None else "",
            ])
    return fpath


def append_summary_csv(outdir: str, rollups: List[ProjectRollup]) -> str:
    fpath = os.path.join(outdir, "_summary.csv")
    with open(fpath, "w", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        w.writerow([
            "project_path", "mrs_merged",
            "mttm_mean_h", "mttm_p50_h", "mttm_p90_h",
            "ttfr_mean_h", "ttfr_p50_h", "ttfr_p90_h",
            "review_rounds_avg",
            "size_xs", "size_s", "size_m", "size_l", "size_xl"
        ])
        for r in rollups:
            w.writerow([
                r.project_path, r.mrs_merged,
                r.mttm_mean_h or "",
                r.mttm_p50_h or "",
                r.mttm_p90_h or "",
                r.ttfr_mean_h or "",
                r.ttfr_p50_h or "",
                r.ttfr_p90_h or "",
                r.review_rounds_avg or "",
                r.size_xs, r.size_s, r.size_m, r.size_l, r.size_xl
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

    parser = argparse.ArgumentParser(
        description="Collect GitLab Dev Flow metrics (local CSV outputs only).")
    parser.add_argument(
        "--group-path", help="Optional GitLab group path to limit projects, e.g., 'parent/subgroup'")
    parser.add_argument("--days", type=int, default=90,
                        help="Lookback window in days (default 90)")
    args = parser.parse_args()

    gl = GitLab(base_url, token)
    outdir = ensure_outputs_dir()
    since = dt.datetime.now(tz=dt.timezone.utc) - dt.timedelta(days=args.days)

    # Discover projects
    if args.group_path:
        projects = gl.list_group_projects(args.group_path)
    else:
        projects = gl.list_projects_membership()

    all_rollups: List[ProjectRollup] = []
    for p in projects:
        ppath = p["path_with_namespace"]
        print(f"[INFO] Project: {ppath}")
        facts, rollup = collect_for_project(gl, p, since)
        if not facts:
            print(f"[INFO]   No merged MRs in window; skipping CSV.",
                  file=sys.stderr)
            continue
        csv_path = write_project_csv(outdir, facts, ppath)
        print(f"[INFO]   wrote {csv_path}")
        all_rollups.append(rollup)

    if all_rollups:
        sum_path = append_summary_csv(outdir, all_rollups)
        print(f"[INFO] Wrote portfolio summary: {sum_path}")
    else:
        print("[INFO] No data found for any project in the window.")


if __name__ == "__main__":
    main()
