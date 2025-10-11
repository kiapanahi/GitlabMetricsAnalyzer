Title: PRD - Developer Productivity: Per-Developer Metrics

Summary
-------
We will design a suite of developer-specific metrics to measure developer productivity, code quality, and collaboration signals. These metrics will be computed from our on-prem GitLab data and stored in PostgreSQL for analysis and reporting.

Goals
-----
- Provide actionable, developer-level metrics that help engineering leadership identify coaching opportunities and track improvements over time.
- Ensure metrics are reproducible, explainable, and resilient to noise (e.g., bot commits, merge strategies).
- Preserve privacy: measure team trends and anonymized summaries before any public dashboards.

Assumptions
-----------
- GitLab data (commits, merge requests, pipelines, issues, reviews) is available and mapped to internal developer identities via `IdentityMapping`.
- CommitTimeAnalysis remains and will be integrated where applicable.

Proposed initial metrics (each will have a separate PRD):
1. Active Development Time (daily/weekly): time spent in active commits and code reviews per developer.
2. Review Turnaround Time: average time to first review comment and time to merge after MR creation.
3. Merge Request Throughput: number and size of MRs merged per period; normalized by churn.
4. Failed CI Rate: percentage of developer-triggered pipelines that fail on first run.
5. Code Review Contribution: number of review comments made, and percent of MRs with substantive reviews.

Data sources
------------
- GitLab API: commits, merge requests, notes (comments), pipelines, users, projects
- Existing database raw tables (if present) under `GitLabMetrics` feature

Privacy and ethics
-----------------
- Aggregate or anonymize data when exposing to reports.
- Flag and exclude automated accounts and bots.

Deliverables
------------
- One PRD per metric in `prds/`.
- Matching GitHub issues (in `requests/`) for implementation tasks.
- Implementation in `src/.../Features/GitLabMetrics/` with tests and migrations where necessary.

Timeline
--------
- Draft PRD for first metric (Active Development Time) within 2 days.

Acceptance criteria
-------------------
- Each metric PRD includes data definitions, computation steps, edge cases, API/DB contract, and tests.

----
This is a high-level PRD to be split into focused PRDs per metric.
