Title: PRD - Review Turnaround Time

Summary
-------
Measure how long it takes for merge requests to receive the first substantive review and the time from MR creation to merge. This helps quantify review responsiveness and bottlenecks.

Data sources
------------
- GitLab Merge Requests (created_at, merged_at, author, assignees)
- Merge request notes (comments) with timestamps and author ids

Definitions
-----------
- TimeToFirstReview: duration between MR.created_at and the first substantive comment by a reviewer.
- TimeToMerge: duration between MR.created_at and MR.merged_at (or closed_at if closed without merge).

Algorithm
---------
1. Identify the first comment on the MR not authored by the MR author and not by bots.
2. Compute TimeToFirstReview = first_comment_time - created_at.
3. Compute TimeToMerge = merged_at - created_at (if merged).
4. Aggregate per developer (author) by day/week and compute percentiles (P50, P75, P90) to handle skew.

Edge cases
----------
- MRs with no comments: treat TimeToFirstReview as null or a large sentinel; configurable.
- Automated comments (CI bots): exclude using identity mapping or known bot list.

DB & API
--------
- Proposed fact: `FactReviewTurnaround` with DeveloperId, Date, P50_FirstReviewMinutes, P75, P90, Median_TimeToMerge

Acceptance
----------
- Values for sample MRs match manual calculations; tests for bot filtering and no-comment handling.
