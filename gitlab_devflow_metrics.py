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

This module provides a command-line tool for collecting and analyzing GitLab development
metrics. It extracts data about merge requests, calculates various metrics related to
development flow, and generates CSV reports for analysis.
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
from urllib.parse import quote

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
    """Calculate the q-th quantile of the given values.
    
    This function uses the inclusive nearest-rank method to calculate quantiles.
    
    Args:
        values (List[float]): List of values to calculate quantile from.
        q (float): Quantile to calculate (between 0 and 1).
        
    Returns:
        Optional[float]: The q-th quantile of the values, rounded to 3 decimal places.
                         Returns None if the input list is empty.
    """
    if not values:
        return None
    vals = sorted(values)
    # inclusive nearest-rank
    idx = max(0, min(len(vals) - 1, int(round((len(vals) - 1) * q))))
    return round(vals[idx], 3)


def sanitize_filename(s: str) -> str:
    """Sanitize a string to be used as a filename.
    
    Replaces characters that are not alphanumeric, dot, underscore, or hyphen with underscores.
    
    Args:
        s (str): The string to sanitize.
        
    Returns:
        str: The sanitized string that can be safely used as a filename.
    """
    return re.sub(r"[^A-Za-z0-9._-]+", "_", s)


def ensure_outputs_dir() -> str:
    """Ensure the outputs directory exists.
    
    Creates the 'outputs' directory in the current working directory if it doesn't exist.
    
    Returns:
        str: The absolute path to the outputs directory.
    """
    outdir = os.path.join(os.getcwd(), "outputs")
    os.makedirs(outdir, exist_ok=True)
    return outdir

# ------------------------------
# GitLab API client
# ------------------------------


class GitLab:
    """Client for interacting with GitLab's API.
    
    This class provides methods to authenticate with GitLab's API and make various 
    API requests to fetch data about projects, merge requests, and related entities.
    It handles pagination, rate limiting, and retries automatically.
    
    Attributes:
        base_url (str): The base URL of the GitLab instance.
        s (requests.Session): A session object for making HTTP requests.
        timeout (int): Timeout in seconds for HTTP requests.
    """
    
    def __init__(self, base_url: str, token: str, timeout: int = 30):
        """Initialize a GitLab API client.
        
        Args:
            base_url (str): The base URL of the GitLab instance.
            token (str): The private access token for API authentication.
            timeout (int, optional): Timeout in seconds for HTTP requests. Defaults to 30.
        """
        self.base_url = base_url.rstrip("/")
        self.s = requests.Session()
        self.s.headers.update({"PRIVATE-TOKEN": token})
        self.timeout = timeout

    def _get(self, path: str, params: Optional[dict] = None) -> requests.Response:
        """Make a GET request to the GitLab API.
        
        This method handles retries for certain HTTP status codes that indicate 
        temporary failures or rate limiting.
        
        Args:
            path (str): The API endpoint path.
            params (Optional[dict], optional): Query parameters. Defaults to None.
            
        Returns:
            requests.Response: The response from the API.
            
        Raises:
            requests.exceptions.HTTPError: If the request fails after retries.
        """
        url = f"{self.base_url}{path}"
        r = None
        for attempt in range(5):
            r = self.s.get(url, params=params, timeout=self.timeout)
            if r.status_code in (429, 502, 503, 504):
                time.sleep(1.5 * (attempt + 1))
                continue
            r.raise_for_status()
            return r
        # If we get here, all attempts failed but we have a response
        if r:  
            r.raise_for_status()  # This will raise an exception
        # This should never happen, but just in case
        raise requests.exceptions.RequestException("All retry attempts failed")

    def _paginate(self, path: str, params: Optional[dict] = None) -> Iterable[dict]:
        """Paginate through API results.
        
        This method handles pagination for GitLab API endpoints that return lists.
        It uses the X-Next-Page header to determine if there are more pages.
        
        Args:
            path (str): The API endpoint path.
            params (Optional[dict], optional): Query parameters. Defaults to None.
            
        Yields:
            dict: Each item from the paginated response.
        """
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
        """List all projects that the authenticated user is a member of.
        
        Projects are sorted by last activity date in descending order.
        
        Returns:
            List[dict]: A list of project dictionaries.
        """
        return list(self._paginate("/api/v4/projects", {"membership": True, "order_by": "last_activity_at", "sort": "desc"}))

    def list_group_projects(self, group_path: str) -> List[dict]:
        """List all projects in a group, including subgroups.
        
        Projects are sorted by last activity date in descending order.
        
        Args:
            group_path (str): The path of the group, e.g., "parent/subgroup".
            
        Returns:
            List[dict]: A list of project dictionaries.
        """
        group_enc = quote(group_path, safe="")
        return list(self._paginate(f"/api/v4/groups/{group_enc}/projects", {"include_subgroups": True, "order_by": "last_activity_at", "sort": "desc"}))

    # Merge Requests

    def list_merge_requests(self, project_id: int, state: str, updated_after: Optional[str] = None) -> List[dict]:
        """List merge requests for a project.
        
        Args:
            project_id (int): The ID of the project.
            state (str): The state of the merge requests to list (e.g., "merged", "opened").
            updated_after (Optional[str], optional): ISO 8601 timestamp to filter MRs updated after this date. Defaults to None.
            
        Returns:
            List[dict]: A list of merge request dictionaries.
        """
        params = {"state": state, "scope": "all",
                  "order_by": "updated_at", "sort": "desc", "per_page": 100}
        if updated_after:
            params["updated_after"] = updated_after
        return list(self._paginate(f"/api/v4/projects/{project_id}/merge_requests", params))

    def get_merge_request(self, project_id: int, iid: int) -> dict:
        """Get a single merge request by its internal ID.
        
        Args:
            project_id (int): The ID of the project.
            iid (int): The internal ID of the merge request.
            
        Returns:
            dict: The merge request details.
        """
        return self._get(f"/api/v4/projects/{project_id}/merge_requests/{iid}").json()

    def get_merge_request_notes(self, project_id: int, iid: int) -> List[dict]:
        """Get all notes (comments) for a merge request.
        
        Args:
            project_id (int): The ID of the project.
            iid (int): The internal ID of the merge request.
            
        Returns:
            List[dict]: A list of note dictionaries, sorted by creation time in ascending order.
        """
        return list(self._paginate(f"/api/v4/projects/{project_id}/merge_requests/{iid}/notes", {"sort": "asc"}))

    def get_merge_request_commits(self, project_id: int, iid: int) -> List[dict]:
        """Get all commits for a merge request.
        
        Args:
            project_id (int): The ID of the project.
            iid (int): The internal ID of the merge request.
            
        Returns:
            List[dict]: A list of commit dictionaries.
        """
        return list(self._paginate(f"/api/v4/projects/{project_id}/merge_requests/{iid}/commits", {"per_page": 100}))

    def get_merge_request_changes(self, project_id: int, iid: int) -> dict:
        """Get the changes (files affected) for a merge request.
        
        Args:
            project_id (int): The ID of the project.
            iid (int): The internal ID of the merge request.
            
        Returns:
            dict: The merge request changes, including the list of files modified.
        """
        return self._get(f"/api/v4/projects/{project_id}/merge_requests/{iid}/changes").json()

# ------------------------------
# Data Structures
# ------------------------------


@dataclass
class MRFact:
    """Represents factual data about a merge request.
    
    This class holds metrics and metadata for a single GitLab merge request,
    calculated from the raw API data.
    
    Attributes:
        project_id (int): The ID of the project.
        project_path (str): The path of the project with namespace.
        mr_id (int): The global ID of the merge request.
        mr_iid (int): The internal ID of the merge request within the project.
        title (str): The title of the merge request.
        author_username (str): The username of the author.
        created_at (dt.datetime): When the MR was created.
        merged_at (dt.datetime): When the MR was merged.
        start_time (dt.datetime): When work on the MR began (created or marked ready).
        time_to_merge_h (Optional[float]): Hours from start_time to merge, if merged.
        time_to_first_review_h (Optional[float]): Hours from creation to first review, if reviewed.
        review_rounds (int): Number of review-commit cycles.
        files_changed (Optional[int]): Number of files changed.
    """
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
    """Aggregated metrics for a project.
    
    This class holds summarized metrics across all merge requests
    in a specific project for a given time period.
    
    Attributes:
        project_id (int): The ID of the project.
        project_path (str): The path of the project with namespace.
        mrs_merged (int): Number of merge requests merged.
        mttm_mean_h (Optional[float]): Mean time to merge in hours.
        mttm_p50_h (Optional[float]): Median (50th percentile) time to merge in hours.
        mttm_p90_h (Optional[float]): 90th percentile time to merge in hours.
        ttfr_mean_h (Optional[float]): Mean time to first review in hours.
        ttfr_p50_h (Optional[float]): Median time to first review in hours.
        ttfr_p90_h (Optional[float]): 90th percentile time to first review in hours.
        review_rounds_avg (Optional[float]): Average number of review rounds.
        size_xs (int): Count of MRs with ≤3 files.
        size_s (int): Count of MRs with 4–10 files.
        size_m (int): Count of MRs with 11–25 files.
        size_l (int): Count of MRs with 26–50 files.
        size_xl (int): Count of MRs with >50 files.
    """
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


# Patterns to detect when a merge request moves between draft and ready states
READY_PATTERNS = [
    "marked this merge request as ready",
    "marked this merge request as ready to merge",
]
DRAFT_PATTERNS = [
    "marked this merge request as draft",
    "marked this merge request as work in progress",
]


def find_ready_time(created_at: dt.datetime, notes: List[dict]) -> dt.datetime:
    """Find when a merge request was marked as ready for review.
    
    Analyzes system notes to determine when a merge request transitioned from draft
    to ready state. If no such transition is found, defaults to the creation time.
    
    Args:
        created_at (dt.datetime): When the MR was created.
        notes (List[dict]): List of notes (comments) on the MR.
        
    Returns:
        dt.datetime: The timestamp when the MR became ready for review.
    """
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
    """Compute time to first review.
    
    Finds the first non-system note from someone other than the MR author
    and calculates the time between MR creation and that first review.
    
    Args:
        created_at (dt.datetime): When the MR was created.
        mr_author (str): Username of the MR author.
        notes (List[dict]): List of notes (comments) on the MR.
        
    Returns:
        Optional[float]: Time to first review in hours, or None if no review was found.
    """
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
    """Compute the number of review rounds.
    
    A review round is counted when a commit follows a review comment, indicating
    the author responded to feedback with changes.
    
    Args:
        mr_author (str): Username of the MR author.
        notes (List[dict]): List of notes (comments) on the MR.
        commits (List[dict]): List of commits in the MR.
        start_time (dt.datetime): When the MR became ready for review.
        merged_at (dt.datetime): When the MR was merged.
        
    Returns:
        int: The number of review rounds.
    """
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
    """Determine the size bucket for a merge request based on files changed.
    
    Args:
        files_changed (Optional[int]): Number of files changed in the MR.
        
    Returns:
        str: Size category - "xs" (≤3 files), "s" (4-10), "m" (11-25), 
             "l" (26-50), "xl" (>50), or "unknown" if files_changed is None.
    """
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
    """Collect merge request metrics for a project.
    
    This function retrieves merge requests from GitLab, calculates various metrics
    for each MR, and aggregates those metrics at the project level.
    
    Args:
        gl (GitLab): GitLab API client.
        project (dict): Project data from GitLab API.
        since (dt.datetime): Start date for data collection.
        
    Returns:
        Tuple[List[MRFact], ProjectRollup]: A tuple containing:
            - A list of MRFact objects, one for each merge request
            - A ProjectRollup object with aggregated metrics
    """
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

        except (KeyError, ValueError, requests.exceptions.RequestException) as e:
            print(
                f"[WARN] project {ppath} MR {mr.get('iid')} failed: {e}", file=sys.stderr)
            continue

    def avg(nums: List[float]) -> Optional[float]:
        """Calculate the average of a list of numbers.
        
        Args:
            nums (List[float]): List of numbers to average.
            
        Returns:
            Optional[float]: The average, rounded to 3 decimal places, or None if the list is empty.
        """
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
        review_rounds_avg=avg([float(r) for r in review_rounds_list]),
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
    """Write merge request facts for a project to a CSV file.
    
    Args:
        outdir (str): Output directory path.
        facts (List[MRFact]): List of merge request facts.
        project_path (str): Project path with namespace.
        
    Returns:
        str: The path to the created CSV file.
    """
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
    """Write project rollups to a summary CSV file.
    
    Args:
        outdir (str): Output directory path.
        rollups (List[ProjectRollup]): List of project rollups.
        
    Returns:
        str: The path to the created summary CSV file.
    """
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
    """Main entry point for the GitLab metrics collection script.
    
    This function:
    1. Reads configuration from environment variables and command-line arguments
    2. Initializes the GitLab API client
    3. Discovers projects based on user membership or group path
    4. Collects metrics for each project
    5. Writes project-specific and summary CSV files
    
    Environment variables required:
    - TOMAN_GITLAB_API_URL: URL of the GitLab instance
    - TOMAN_GITLAB_API_TOKEN: Personal access token with API scope
    
    Command-line arguments:
    - --group-path: Optional GitLab group path to limit projects
    - --days: Lookback window in days (default 90)
    
    Exit codes:
    - 0: Success
    - 2: Missing required environment variables
    """
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
            print("[INFO]   No merged MRs in window; skipping CSV.",
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
