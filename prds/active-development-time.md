Title: PRD - Active Development Time

Summary
-------
Active Development Time estimates the time a developer spends actively working on code during a day/week. It combines commit timestamps, active editing signals (if available), and code review activity to estimate productive time.

Why it matters
------------
This metric gives a directional view of developer activity patterns without relying solely on commit counts. It helps detect burn-out signals, identify focus time, and measure changes after process interventions.

Data sources
------------
- GitLab commits (timestamp, author, commit id, project)
- Merge request notes/comments (timestamps of review activity)
- Pipelines timestamps (optional — used to attribute CI time)

Definitions / Contract
----------------------
- Input shape: Raw commits table with columns: CommitId, AuthorGitLabId, CommitTimestamp, ProjectId, FilesChanged
- Output: Fact table `FactDeveloperActiveTime`
  - DeveloperId (FK to DimUser)
  - Date (UTC date)
  - ActiveMinutes (int)
  - CommitCount (int)
  - ReviewMinutes (int)
  - SourceProjects (json/text list or count)

Computation algorithm (initial)
-----------------------------
1. Group commits by developer and day (UTC).
2. For each developer-day, sort commit timestamps.
3. Compute time gaps between consecutive commits. For gaps <= 2 hours, consider the gap as active coding time and sum it. For gaps > 2 hours, cap the contribution at 2 hours per continuous session.
4. Add review activity: for each review comment by the developer on that day, count a fixed amount (e.g., 15 minutes) per substantive comment, up to a configurable daily cap.
5. ActiveMinutes = sum(session durations) + ReviewMinutes.
6. Exclude commits from known bot users or automated accounts.

Parameters & configuration
--------------------------
- Session gap threshold: 2 hours (configurable)
- Max session length cap: 4 hours (configurable)
- Review comment minute credit: 15 minutes per substantive comment
- Substantive comment heuristic: comment length > 20 characters or contains code snippets

Edge cases
----------
- Sparse commit activity (e.g., only 1 short commit in a day): still credit a base minimum (e.g., 10 minutes) to avoid zeros for obvious work.
- Timezones: store and compute in UTC to avoid DST issues.
- Merged commits authored by others: use author fields — attribute to commit author, not merger.

DB schema suggestion
--------------------
- Add `FactDeveloperActiveTime` entity/migration with columns above, indexed on (DeveloperId, Date).

API contract
------------
- GET /api/v1/per-developer/active-time?developerId={id}&from={yyyy-MM-dd}&to={yyyy-MM-dd}
- Response: list of { date, activeMinutes, commitCount, reviewMinutes }

Acceptance criteria
-------------------
- Computed values for a sample developer match manual checks for a selected week.
- Tests cover sessionization logic, bot filtering, and review credit heuristics.
- DB migration added and verified.

Implementation checklist
------------------------
- [ ] Create PRD (this file)
- [ ] Create issue (`requests/002-active-development-time.md`)
- [ ] Add EF entity and migration for `FactDeveloperActiveTime`
- [ ] Implement computation service and unit tests
- [ ] Expose endpoint and add integration test
- [ ] Document configuration knobs in `Configuration/MetricsConfiguration.cs`

----
This covers the initial design; algorithm tweaks and parameter tuning will follow after sample data validation.
