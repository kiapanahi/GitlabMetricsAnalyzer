Title: Remove unused features (non-CommitTimeAnalysis)

Description
-----------
We will remove the currently unused/unfinished features in the `GitLabMetrics` feature set so we can start fresh and implement new, focused developer-specific metrics. The `CommitTimeAnalysis` feature has been validated and should remain. This issue captures the deletion scope, safety checklist, and acceptance criteria.

Background
----------
The repository currently contains several feature areas under `src/Toman.Management.KPIAnalysis.ApiService/Features/GitLabMetrics/` beyond `CommitTimeAnalysis`. Many of these are incomplete, unused, or not providing useful metrics in their current form. Removing them will make it easier to design and implement a new, maintainable set of per-developer metrics.

Scope (what to delete)
----------------------
Keep:
- `CommitTimeAnalysis` (endpoints and services used by commit-time metrics)
- Shared infrastructure required by commit-time analysis (e.g., `GitLabHttpClient` if used by commit collection)

Delete (candidate files / folders) — review before applying:
- Services:
  - src/Toman.Management.KPIAnalysis.ApiService/Features/GitLabMetrics/Services/MetricsCalculationService.cs
  - src/Toman.Management.KPIAnalysis.ApiService/Features/GitLabMetrics/Services/MetricsExportService.cs
  - src/Toman.Management.KPIAnalysis.ApiService/Features/GitLabMetrics/Services/PerDeveloperMetricsComputationService.cs
  - src/Toman.Management.KPIAnalysis.ApiService/Features/GitLabMetrics/Services/DataQualityService.cs
  - src/Toman.Management.KPIAnalysis.ApiService/Features/GitLabMetrics/Services/MetricsAggregatesPersistenceService.cs
  - src/Toman.Management.KPIAnalysis.ApiService/Features/GitLabMetrics/Services/MetricCatalogService.cs
  - src/Toman.Management.KPIAnalysis.ApiService/Features/GitLabMetrics/Services/ObservabilityMetricsService.cs
  - src/Toman.Management.KPIAnalysis.ApiService/Features/GitLabMetrics/Services/MigratorHostedService.cs
  - src/Toman.Management.KPIAnalysis.ApiService/Features/GitLabMetrics/Services/GitLabCollectorService.cs
  - src/Toman.Management.KPIAnalysis.ApiService/Features/GitLabMetrics/Services/DataEnrichmentService.cs
  - src/Toman.Management.KPIAnalysis.ApiService/Features/GitLabMetrics/Services/UserSyncService.cs
  - src/Toman.Management.KPIAnalysis.ApiService/Features/GitLabMetrics/Services/DataResetService.cs

- Endpoints / APIs (non-commit):
  - src/Toman.Management.KPIAnalysis.ApiService/Features/GitLabMetrics/PerDeveloperMetricsEndpoints.cs
  - src/Toman.Management.KPIAnalysis.ApiService/Features/GitLabMetrics/MetricsExportEndpoints.cs
  - src/Toman.Management.KPIAnalysis.ApiService/Features/GitLabMetrics/GitLabMetricsEndpoints.cs (if it primarily surfaces the old metrics)
  - src/Toman.Management.KPIAnalysis.ApiService/Features/GitLabMetrics/GitLabCollectionEndpoints.cs
  - src/Toman.Management.KPIAnalysis.ApiService/Features/GitLabMetrics/DataQualityEndpoints.cs

- Models / Entities (may be re-created later):
  - src/Toman.Management.KPIAnalysis.ApiService/Features/GitLabMetrics/Models/Facts/* (e.g., FactMergeRequest, FactPipeline, FactRelease, FactGitHygiene, FactUserMetrics)
  - src/Toman.Management.KPIAnalysis.ApiService/Features/GitLabMetrics/Models/Entities/* (e.g., DeveloperMetricsAggregate, Developer, DeveloperAlias, ReviewEvent, Project, PipelineFact, MergeRequestFact, CommitFact — NOTE: keep CommitFact if used by CommitTimeAnalysis)

- Jobs:
  - src/Toman.Management.KPIAnalysis.ApiService/Features/GitLabMetrics/Jobs/NightlyProcessingJob.cs

- Infrastructure that is tightly coupled to the above and not used by commit-time analysis:
  - ServiceCollectionExtensions.cs (if it wires up removed services — update instead of delete)
  - Diagnostics.cs (if it only relates to removed features)

Important: this list is a recommended starting point. Before deleting anything, run a compile and confirm what references remain; remove only the files that are not referenced by `CommitTimeAnalysis` or by shared infra required elsewhere.

Proposed branch name
--------------------
chore/remove-unused-features

Checklist (step-by-step)
------------------------
- [ ] Create branch `chore/remove-unused-features`
- [ ] Create this issue on GitHub (copy this file content)
- [ ] Update `ServiceCollectionExtensions` and `Program.cs` to remove/guard registrations for services that will be deleted
- [ ] Remove the files listed above (commit as one or more commits with clear messages)
- [ ] Build solution: `dotnet build` (fix any missing references)
- [ ] Run unit tests: `dotnet test` (or run the repo test task)
- [ ] Run a manual smoke test of relevant endpoints (commit-time endpoints remain functioning)
- [ ] Push branch and open PR with the deletion changes

Acceptance criteria
-------------------
- The repository builds successfully after the deletions.
- All unit tests pass (or known failing tests are documented in the PR).
- `CommitTimeAnalysis` endpoints and features remain functional and fully compiling.
- The PR includes a clear rollback path (what was removed and how to restore).

Risk & Rollback
---------------
- Risk: deleting shared models or data context pieces that `CommitTimeAnalysis` needs. Mitigation: take small commits, run build/tests often, and avoid deleting models that are referenced by commit-time analysis until we confirm they are unused.
- Rollback: branch/PR contains all removal commits. If a problem is found, revert the PR or restore files from branch history.

Notes for reviewers
------------------
- Pay special attention to `GitLabMetricsDbContext` and EF migrations — don't accidentally break migrations needed for other data.
- Keep migrations and database entities only if they are used by `CommitTimeAnalysis` or planned new metrics; otherwise they can be archived in a separate branch for record-keeping.

Next steps (after issue exists)
-----------------------------
1. Once this issue is opened on GitHub, I'll proceed to create the `chore/remove-unused-features` branch locally and start making minimal edits to unregister services in DI and then perform the deletions in small, testable commits. If you'd like, I can proceed now and open a PR draft with the first commit.

----
Generated: automated draft by assistant. Copy to GitHub issues or attach to an issue when ready.
