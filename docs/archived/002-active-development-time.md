Title: Implement metric - Active Development Time

Description
-----------
Implement the Active Development Time metric as specified in `prds/active-development-time.md`.

Tasks
-----
- [ ] Add `FactDeveloperActiveTime` EF entity and migration
- [ ] Implement computation service under `Features/GitLabMetrics/Services/` (e.g., ActiveDevelopmentTimeService)
- [ ] Add endpoint `PerDeveloperActiveTimeEndpoints` or extend existing per-developer endpoints
- [ ] Add unit tests for sessionization and review credit logic
- [ ] Add integration test that runs computation on a small dataset and asserts expected outputs

Acceptance criteria
-------------------
- The DB has the new fact table populated for sample data.
- Endpoint returns expected values for the sample developer.

Depends on
----------
- Identity mapping for matching GitLab user ids to DeveloperId
- Existing raw commits ingestion

Estimate
--------
2-4 days depending on data quality and mapping availability.
