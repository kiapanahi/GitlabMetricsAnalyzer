#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
GitLab Dev Flow Metrics (Concurrent, Local CSV Only)
====================================================

This module collects developer-flow metrics from a self-managed GitLab and writes
per-project CSVs plus a portfolio summary. It is optimized for on-prem installs
with many projects/merge requests by running **concurrently**.

Environment (required)
----------------------
- TOMAN_GITLAB_API_URL   : Base URL of your GitLab, e.g. https://gitlab.example.com
- TOMAN_GITLAB_API_TOKEN : Personal Access Token with API scope

Command-line options
--------------------
- --group-path "parent/subgroup" : Limit to a group (includes subgroups)
- --days 90                      : Lookback window (default 90)
- --workers 8                    : Project-level concurrency (default 8)
- --per-project-workers 4        : Per-project MR concurrency (default 4)

Outputs
-------
- outputs/<namespace__project>.csv  : MR-level facts
- outputs/_summary.csv              : Project rollups

Metrics
-------
Per MR:
- Time to Merge (hours)         : merged_at - (ready_note_time or created_at)
- Time to First Review (hours)  : first non-author comment - created_at
- Review Rounds (count)         : cycles of "reviewer comment → author commit"
- Files Changed (count)         : number of files in MR changes

Per Project (rollups):
- MTTM mean/p50/p90 (hours), TTFR mean/p50/p90 (hours), Avg review rounds
- Size bucket counts (XS ≤3, S 4-10, M 11-25, L 26-50, XL >50)

Design Notes
------------
- Uses ThreadPoolExecutor at two levels:
  * Project-level fan-out across all projects
  * Per-project fan-out across MRs
- Each worker instantiates its own lightweight GitLab client (requests.Session)
  to avoid cross-thread session sharing.
"""

from __future__ import annotations

import argparse
import csv
import datetime as dt
import os
import re
import sys
import time
from dataclasses import dataclass
from typing import Dict, Iterable, List, Optional, Tuple
from concurrent.futures import ThreadPoolExecutor, as_completed

import requests

ISO8601 = "%Y-%m-%dT%H:%M:%S.%fZ"


def parse_time(s: str) -> dt.datetime:
    """Parse a GitLab ISO8601 timestamp (UTC) into a timezone-aware datetime.

    GitLab sometimes omits microseconds; this function handles both forms.
    """
    try:
        return dt.datetime.strptime(s, ISO8601).replace(tzinfo=dt.timezone.utc)
    except ValueError:
        return dt.datetime.fromisoformat(s.replace("Z", "+00:00")).astimezone(dt.timezone.utc)


def to_hours(delta: dt.timedelta) -> float:
    """Convert a timedelta to fractional hours rounded to milliseconds."""
    return round(delta.total_seconds() / 3600.0, 3)


def quantiles(values: List[float], q: float) -> Optional[float]:
    """Return an inclusive nearest-rank quantile for a list of floats."""
    if not values:
        return None
    vals = sorted(values)
    idx = max(0, min(len(vals) - 1, int(round((len(vals) - 1) * q))))
    return round(vals[idx], 3)


def sanitize_filename(s: str) -> str:
    """Sanitize an arbitrary string for safe use as a filename."""
    return re.sub(r"[^A-Za-z0-9._-]+", "_", s)


def ensure_outputs_dir() -> str:
    """Create and return the `outputs` directory path if not present."""
    outdir = os.path.join(os.getcwd(), "outputs")
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

    # ---- Merge Request endpoints ----

    def list_merge_requests(self, project_id: int, state: str, updated_after: Optional[str] = None) -> List[dict]:
        """Return merge requests in a given state with optional updated_after filter."""
        params = {"state": state, "scope": "all",
                  "order_by": "updated_at", "sort": "desc", "per_page": 100}
        if updated_after:
            params["updated_after"] = updated_after
        return list(self._paginate(f"/api/v4/projects/{project_id}/merge_requests", params))

    def get_merge_request(self, project_id: int, iid: int) -> dict:
        """Return details of a single merge request by IID."""
        return self._get(f"/api/v4/projects/{project_id}/merge_requests/{iid}").json()

    def get_merge_request_notes(self, project_id: int, iid: int) -> List[dict]:
        """Return notes (comments) on a merge request, ascending by time."""
        return list(self._paginate(f"/api/v4/projects/{project_id}/merge_requests/{iid}/notes", {"sort": "asc"}))

    def get_merge_request_commits(self, project_id: int, iid: int) -> List[dict]:
        """Return commits associated with a merge request."""
        return list(self._paginate(f"/api/v4/projects/{project_id}/merge_requests/{iid}/commits", {"per_page": 100}))

    def get_merge_request_changes(self, project_id: int, iid: int) -> dict:
        """Return file changes for a merge request (used to count files)."""
        return self._get(f"/api/v4/projects/{project_id}/merge_requests/{iid}/changes").json()


@dataclass
class MRFact:
    """A single merge request fact row suitable for CSV output."""

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
    """Per-project aggregate metrics computed from MR facts."""

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
    size_s: int = 0   # 4-10 files
    size_m: int = 0   # 11-25 files
    size_l: int = 0   # 26-50 files
    size_xl: int = 0  # >50 files


READY_PATTERNS = [
    "marked this merge request as ready",
    "marked this merge request as ready to merge",
]
DRAFT_PATTERNS = [
    "marked this merge request as draft",
    "marked this merge request as work in progress",
]


def find_ready_time(created_at: dt.datetime, notes: List[dict]) -> dt.datetime:
    """Return the last 'ready' timestamp if present; otherwise the MR creation time."""
    ready_time = created_at
    for n in notes:
        if not n.get("system"):
            continue
        b = (n.get("body") or "").lower()
        t = parse_time(n["created_at"])
        if any(p in b for p in READY_PATTERNS):
            ready_time = t
        elif any(p in b for p in DRAFT_PATTERNS):
            # Explicit draft note resets expectation; next "ready" will override.
            pass
    return ready_time


def compute_ttfr(created_at: dt.datetime, mr_author: str, notes: List[dict]) -> Optional[float]:
    """Compute Time to First Review in hours from MR creation to first non-author comment."""
    for n in notes:
        if n.get("system"):
            continue
        author = (n.get("author") or {}).get("username")
        if author and author != mr_author:
            t = parse_time(n["created_at"])
            if t >= created_at:
                return to_hours(t - created_at)
    return None


def compute_review_rounds(mr_author: str, notes: List[dict], commits: List[dict],
                          start_time: dt.datetime, merged_at: dt.datetime) -> int:
    """Count review rounds as cycles of (reviewer comment → a subsequent author commit)."""
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
    """Bucket an MR by number of changed files into xs/s/m/l/xl."""
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


def process_single_mr(base_url: str, token: str, pid: int, ppath: str, iid: int,
                      since: dt.datetime) -> Optional[MRFact]:
    """Fetch and compute metrics for a single MR; returns MRFact or None on skip/failure."""
    try:
        gl = GitLab(base_url, token)
        mr_full = gl.get_merge_request(pid, iid)
        created_at = parse_time(mr_full["created_at"])
        merged_at = parse_time(mr_full["merged_at"]) if mr_full.get(
            "merged_at") else None
        if merged_at is None or merged_at < since:
            return None

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

        return MRFact(
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
        )
    except Exception as e:
        print(f"[WARN] project {ppath} MR {iid} failed: {e}", file=sys.stderr)
        return None


def collect_for_project(base_url: str, token: str, project: dict, since: dt.datetime,
                        per_project_workers: int) -> Tuple[List[MRFact], ProjectRollup]:
    """Collect MR facts and project rollup concurrently for a single project."""
    pid = project["id"]
    ppath = project["path_with_namespace"]
    gl = GitLab(base_url, token)
    merged_mrs = gl.list_merge_requests(
        pid, state="merged", updated_after=since.isoformat())

    facts: List[MRFact] = []
    mttm_list: List[float] = []
    ttfr_list: List[float] = []
    review_rounds_list: List[int] = []
    size_counts = {"xs": 0, "s": 0, "m": 0, "l": 0, "xl": 0}

    with ThreadPoolExecutor(max_workers=max(1, per_project_workers)) as ex:
        futures = [ex.submit(process_single_mr, base_url, token, pid, ppath, mr["iid"], since)
                   for mr in merged_mrs]
        for fut in as_completed(futures):
            fact = fut.result()
            if not fact:
                continue
            facts.append(fact)
            if fact.time_to_merge_h is not None:
                mttm_list.append(fact.time_to_merge_h)
            if fact.time_to_first_review_h is not None:
                ttfr_list.append(fact.time_to_first_review_h)
            review_rounds_list.append(fact.review_rounds)
            size_counts[size_bucket(fact.files_changed)] += 1

    def avg(nums: List[float]) -> Optional[float]:
        return round(sum(nums) / len(nums), 3) if nums else None

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


def write_project_csv(outdir: str, facts: List[MRFact], project_path: str) -> str:
    """Write MR facts for a project to CSV; return file path."""
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
    """Write the portfolio rollup CSV covering all processed projects."""
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
        for r in sorted(rollups, key=lambda x: x.project_path.lower()):
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


def main() -> None:
    """Entry point: parse args, fan out across projects/MRs, write CSVs."""
    base_url = os.getenv("TOMAN_GITLAB_API_URL")
    token = os.getenv("TOMAN_GITLAB_API_TOKEN")
    if not base_url or not token:
        print("Environment variables TOMAN_GITLAB_API_URL and TOMAN_GITLAB_API_TOKEN must be set.", file=sys.stderr)
        sys.exit(2)

    parser = argparse.ArgumentParser(
        description="Collect GitLab Dev Flow metrics concurrently (local CSV outputs only).")
    parser.add_argument(
        "--group-path", help="Optional GitLab group path to limit projects, e.g., 'parent/subgroup'")
    parser.add_argument("--days", type=int, default=90,
                        help="Lookback window in days (default 90)")
    parser.add_argument("--workers", type=int, default=8,
                        help="Project-level concurrency (default 8)")
    parser.add_argument("--per-project-workers", type=int,
                        default=4, help="Per-project MR concurrency (default 4)")
    args = parser.parse_args()

    outdir = ensure_outputs_dir()
    since = dt.datetime.now(tz=dt.timezone.utc) - dt.timedelta(days=args.days)

    # Discover projects
    gl_discovery = GitLab(base_url, token)
    if args.group_path:
        projects = gl_discovery.list_group_projects(args.group_path)
    else:
        projects = gl_discovery.list_projects_membership()

    rollups: List[ProjectRollup] = []
    futures = []
    with ThreadPoolExecutor(max_workers=max(1, args.workers)) as ex:
        for p in projects:
            futures.append(ex.submit(collect_for_project, base_url,
                           token, p, since, args.per_project_workers))

        for fut in as_completed(futures):
            try:
                facts, rollup = fut.result()
                if facts:
                    csv_path = write_project_csv(
                        outdir, facts, rollup.project_path)
                    print(f"[INFO] wrote {csv_path}")
                    rollups.append(rollup)
                else:
                    print(
                        f"[INFO] no merged MRs for project in window; skipped.", file=sys.stderr)
            except Exception as e:
                print(
                    f"[WARN] project processing failed: {e}", file=sys.stderr)

    if rollups:
        sum_path = append_summary_csv(outdir, rollups)
        print(f"[INFO] Wrote portfolio summary: {sum_path}")
    else:
        print("[INFO] No data found for any project in the window.")


if __name__ == "__main__":
    main()
