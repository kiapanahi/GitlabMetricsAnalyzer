Title: Create PRDs and issues for each developer-specific metric

Description
-----------
Create focused PRDs (one per metric) under `prds/` and a corresponding GitHub issue under `requests/` so each metric can be implemented and tracked independently.

Initial metrics to create PRDs for (from `prds/developer-productivity-metrics.md`):
- Active Development Time
- Review Turnaround Time
- Merge Request Throughput
- Failed CI Rate
- Code Review Contribution

Checklist
---------
- [ ] Create `prds/active-development-time.md` and `requests/002-active-development-time.md`
- [ ] Create `prds/review-turnaround-time.md` and `requests/003-review-turnaround-time.md`
- [ ] Create `prds/merge-request-throughput.md` and `requests/004-merge-request-throughput.md`
- [ ] Create `prds/failed-ci-rate.md` and `requests/005-failed-ci-rate.md`
- [ ] Create `prds/code-review-contribution.md` and `requests/006-code-review-contribution.md`
- [ ] For each metric, include data sources, computation algorithm, edge cases, DB schema changes, API contract, tests, and acceptance criteria.

Next steps
----------
I'll start by opening the first PRD and issue for "Active Development Time" unless you'd like a different priority.
