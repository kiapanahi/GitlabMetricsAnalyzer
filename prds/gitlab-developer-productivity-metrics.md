# PRD — Developer Productivity Metrics from GitLab (Per‑Developer, Rolling Window)

**Owner:** VP of Engineering (you)
**Tech Leads:** Platform (Data/Observability), DevEx
**Date:** 2025‑09‑26
**Status:** Draft for build
**Target stack:** C#/.NET (ASP.NET Core) services orchestrated with **.NET Aspire**; background Worker Services *(automated scheduling deferred to vNext)*; TS/React dashboard; **storage: Postgres only (v1)** (no object storage).

---

## 1) Problem Statement & Goals

We need a reliable, low‑friction way to quantify developer flow, collaboration, CI/CD health, and quality signals using only the organization’s GitLab server APIs. The system must compute **per‑developer** metrics over rolling time windows (e.g., 14/28/90 days), produce **auditable outputs (JSON)**, and expose an internal API for dashboards/OKRs.

### Primary Goals

1. Implement a metrics pipeline that ingests GitLab artifacts (commits, merge requests, comments/reviews, pipelines, approvals) and produces **per‑developer** aggregates.
2. Provide a **clear, immutable metric catalog** (IDs, formulas, edge cases) with robust defaults (medians, size‑normalization).
3. Offer **configurable windows** and **project scopes**, excluding bots and non‑human identities.
4. Deliver **machine‑readable outputs** for dashboards and notebooks; include a lightweight REST API.

### Non‑Goals

* Ranking or stack‑ranking individuals.
* Measuring "impact" or performance beyond code/review/CI signals.
* Cross‑tool correlations (e.g., Jira, PagerDuty) — can be Phase 2.

### Success Criteria (Measurable)

* ≥ 95% of active developers (committed ≥ 1 MR in window or pushed ≥ 1 commit in window) included in daily run.
* Data latency ≤ 2 hours after run start.
* Reproducibility: recomputation on same inputs yields bit‑identical aggregates.
* Metric completeness ≥ 98% (share of non‑null metric values when logically computable).
* Pipeline runtime ≤ 45 min for org size ≤ 100 devs, ≤ 1000 active MRs/month.

---

## 2) Glossary & Notation

* **W**: Rolling time window (default 28 days).
* **D**: A canonical developer identity (see identity mapping).
* **MR** fields: `created_at`, `merged_at`, `updated_at`, `author`, `assignees`, `approved_at` (derive from approvals timeline), `squash`, `state`, `changes_count`, `additions`, `deletions`.
* **First Commit** of MR: earliest commit timestamp among commits associated with the MR (or source branch if needed).
* **First Review**: timestamp of the first **non‑author** note/discussion on the MR.
* **Merge Pipeline**: the pipeline associated with the merge commit; fallback to the latest successful pipeline before `merged_at`.
* **Rework**: MR received changes after review started (new commits after first review event or MR moved to needs‑changes).
* **Hotfix**: label `hotfix` or `hot-fix` on an MR; fallback heuristic: MR title/branch contains `hotfix|hot-fix`.

---

## 3) Scope

### In‑Scope Artifacts (GitLab v4)

* Projects (IDs, path_with_namespace)
* Merge Requests (incl. changes, timelines, notes/discussions, approvals)
* Commits (authorship, timestamps, parents, titles)
* Pipelines (status, duration, coverage if present) & Jobs (optional)
* Users (username, email, state)

### Out‑of‑Scope (Phase 1)

* Issues, milestones, epics
* Incident/on‑call data
* External CI systems
* SAST/DAST artifacts beyond pipeline status

---

## 4) Data Model

### 4.1 Entities

* **developer**: `{developer_id, usernames[], emails[], is_bot, canonical_email}`
* **project**: `{project_id, path, group_id}`
* **mr**: `{project_id, iid, author_id, created_at, merged_at, approved_at, first_commit_at, first_review_at, additions, deletions, squash, state, labels[], conflicted:boolean, revert_of_mr_iid?:int}`
* **pipeline**: `{project_id, pipeline_id, sha, status, duration_sec, coverage:float?, created_at, updated_at}`
* **mr_pipeline_link**: `{project_id, mr_iid, pipeline_id, link_type:'merge'|'pre-merge'}`
* **review_event**: `{project_id, mr_iid, user_id, type:'comment'|'approval'|'request_changes'|'resolve', created_at}`
* **commit**: `{project_id, sha, author_id, committed_at, title}`

### 4.2 Storage (v1)

* **Postgres (single store)** for incremental sync state, identities, **facts** (MRs, pipelines, reviews, commits) and **aggregates**.
* Use **native table partitioning** (by `ingestion_date` and/or `project_id`) and **materialized views** for hot aggregates.
* Indexes: `btree` on timestamps; composite indexes on `(project_id, merged_at)`, `(author_id, merged_at)`; GIN on labels.

### 4.3 Identity Mapping

* Seed from GitLab `/users` endpoint.
* Build `developer_id` by grouping on normalized emails + usernames.
* Mark bots via `bot`, `support-bot`, `ci`, `dependabot`, regex list (configurable).
* Maintain overrides in `developer_aliases` table.

---

## 5) Ingestion & API Usage

### 5.1 Required GitLab Endpoints (v4)

* **Projects**: `GET /groups/:id/projects` (paginate)
* **MRs**: `GET /projects/:id/merge_requests?state=all&updated_after=ISO&updated_before=ISO&per_page=100`
* **MR details**: `GET /projects/:id/merge_requests/:iid`
* **MR changes**: `GET /projects/:id/merge_requests/:iid/changes` → additions/deletions
* **MR notes**: `GET /projects/:id/merge_requests/:iid/notes` or **discussions** `.../discussions`
* **Approvals**: `GET /projects/:id/merge_requests/:iid/approvals` + **approval events** `.../approval_state`
* **Pipelines**:

  * by MR SHA: `GET /projects/:id/pipelines?sha=<sha>`
  * by ID: `GET /projects/:id/pipelines/:pipeline_id`
* **Commits**: `GET /projects/:id/repository/commits?since=ISO&until=ISO&per_page=100`
* **Users**: `GET /users?username=...`, `GET /users/:id`

### 5.2 Ingestion Strategy

* **Windowed incremental** by `updated_after/updated_before`.
* MR timelines require **backfill depth** (e.g., 90 days) to link reverts/hotfixes.
* **Retry w/ exponential backoff**, honor rate limits; page size 100; ETag caching when available.
* **Idempotent upserts** keyed by `(project_id, iid)` or `(project_id, pipeline_id)`.

---

## 6) Metric Catalog (Per‑Developer)

**Defaults:** window=W=28 days; use **median** unless otherwise specified; exclude bots; only MRs/commits with author = D.
**Notation:** see formulas in inline blocks; timestamps in UTC.

> Each metric includes: **id**, **name**, **category**, **definition**, **formula**, **edge_cases**, **inputs** (entity fields), **null_when** (conditions to produce null), **direction** (↑ good / ↓ good), **unit**.

### 6.1 Flow (Throughput & Speed)

1. **`merged_mrs`** — *Merged MRs count*

   * **formula**: `count(MR_D where merged_at ∈ W)`
   * **direction**: ↑
   * **unit**: count

2. **`lines_changed`** — *Total lines changed in merged MRs*

   * **formula**: `Σ(additions+deletions) over MR_D merged in W`
   * **edge_cases**: if changes unavailable, compute from diff stats; exclude vendor/lock files via glob filters.
   * **direction**: context
   * **unit**: LOC

3. **`coding_time_median`** — *First commit → MR open*

   * **formula**: `median(m.open - m.first_commit)`
   * **null_when**: missing first_commit
   * **direction**: ↓
   * **unit**: hours

4. **`t2_first_review_median`** — *MR open → first non‑author note*

   * **formula**: `median(m.first_review - m.open)`
   * **direction**: ↓
   * **unit**: hours

5. **`review_time_median`** — *First review → approval*

   * **formula**: `median(m.approved_at - m.first_review)`
   * **direction**: ↓
   * **unit**: hours

6. **`merge_time_median`** — *Approval → merged*

   * **formula**: `median(m.merged_at - m.approved_at)`
   * **direction**: ↓
   * **unit**: hours

7. **`cycle_time_median`** — *First commit → merge*

   * **formula**: `median(m.merged_at - m.first_commit)`
   * **direction**: ↓
   * **unit**: hours

8. **`wip_open_mrs`** — *Open (not merged) MRs snapshot*

   * **formula**: `count(MR_D where state in {opened, draft} at T0)`
   * **direction**: ↓
   * **unit**: count

9. **`context_index_projects`** — *Distinct projects touched*

   * **formula**: `distinct_count(project_id over MR_D ∪ commits_D in W)`
   * **direction**: context
   * **unit**: count

### 6.2 Quality & Reliability

10. **`rework_ratio`** — *MRs with changes after review started*

    * **formula**: `(# merged MRs where commits_after_first_review > 0) / merged_mrs`
    * **direction**: ↓
    * **unit**: ratio [0..1]

11. **`revert_rate`** — *Merged MRs later reverted*

    * **formula**: `(# merged MRs with revert_of = true within 30d) / merged_mrs`
    * **direction**: ↓
    * **unit**: ratio

12. **`conflict_rate`** — *MRs that hit merge conflicts*

    * **formula**: `(# MRs flagged conflicted) / count(MR_D in W)`
    * **direction**: ↓
    * **unit**: ratio

13. **`hotfix_followups_rate`** — *Hotfix MRs within 48h changing same files*

    * **formula**: `(# events where a hotfix MR touches ≥1 file from D’s prior merged MR within 48h) / merged_mrs`
    * **direction**: ↓
    * **unit**: ratio

14. **`defect_proxy_per_kloc`** — *Reverts+hotfixes per 1k changed lines*

    * **formula**: `1000*(revert_count + hotfix_followups)/max(1, Σ changed LOC)`
    * **direction**: ↓
    * **unit**: events/kloc

### 6.3 Collaboration & Review

15. **`reviews_given`** — *Reviews performed by D (others’ MRs)*

    * **formula**: `count(review_events by D where MR.author != D)`
    * **direction**: ↑
    * **unit**: count

16. **`comments_per_review_median`** — *Depth per review*

    * **formula**: `median(# comments in each review session by D)`
    * **direction**: contextual
    * **unit**: comments

17. **`review_response_time_median`** — *Assigned/requested → first comment by D*

    * **formula**: `median(first_comment_at_D - review_requested_at_D)`
    * **direction**: ↓
    * **unit**: hours

18. **`feedback_address_rate`** — *Author resolves reviewer threads*

    * **formula**: `Σ resolved_threads_by_author / Σ threads_from_others on D’s MRs`
    * **direction**: ↑
    * **unit**: ratio

19. **`silent_approvals_rate`** — *Approvals with zero comments*

    * **formula**: `(approvals_by_D_with_0_comments) / (total_approvals_by_D)`
    * **direction**: ↓
    * **unit**: ratio

### 6.4 CI/CD Efficiency

20. **`mr_pipeline_success_rate`**

    * **formula**: `success_merge_pipelines / merged_mrs`
    * **direction**: ↑
    * **unit**: ratio

21. **`mr_pipeline_duration_median`**

    * **formula**: `median(merge_pipeline.duration_sec for D’s merged MRs)`
    * **direction**: ↓
    * **unit**: seconds

22. **`red_runs_per_mr_median`** — *Failed pipelines pre‑merge*

    * **formula**: `median(# failed pipelines on MR before merge)`
    * **direction**: ↓
    * **unit**: count

23. **`coverage_delta_median`** (if coverage available)

    * **formula**: `median(coverage_after - coverage_before for D’s merged MRs)`
    * **direction**: ↑
    * **unit**: pct points

24. **`flaky_signal_rate`** — *fail→immediate success for same SHA*

    * **formula**: `(count sequences [failed, success] within 2h on same SHA) / (total pipelines on D’s MRs)`
    * **direction**: ↓
    * **unit**: ratio

### 6.5 Hygiene & Work Patterns

25. **`commit_days_count`** — *Distinct days with commits*

    * **formula**: `distinct_count(date(committed_at) for commits by D)`
    * **direction**: contextual
    * **unit**: days

26. **`good_commit_msg_rate`** — *Heuristic quality*

    * **formula**: `# commits passing message_rules / total_commits_by_D`
    * **message_rules (default)**: length ≥ 15, not in {"fix", "wip"}, contains verb + scope pattern `(feat|fix|refactor|docs|test)(:|\()`.
    * **direction**: ↑
    * **unit**: ratio

27. **`squash_rate`** — *MRs merged with squash*

    * **formula**: `# merged MRs with squash=true / merged_mrs`
    * **direction**: contextual
    * **unit**: ratio

28. **`small_mr_rate`** — *MRs ≤ 400 changed LOC*

    * **formula**: `# merged MRs with (additions+deletions) ≤ 400 / merged_mrs`
    * **direction**: ↑
    * **unit**: ratio

29. **`review_balance_ratio`** — *Given vs. received*

    * **formula**: `reviews_given / max(1, reviews_on_Ds_MRs_by_others)`
    * **direction**: ≈1 is healthy
    * **unit**: ratio

30. **`batching_size_median`** — *Typical MR size*

    * **formula**: `median(additions+deletions over merged MRs)`
    * **direction**: target band 100–400
    * **unit**: LOC

---

## 7) Computation Rules & Edge Cases

* **Timezones**: Ingest as ISO‑8601; compute in UTC; display in local tz at UI.
* **Medians**: Use P50; if sample size < 3, flag `low_n=true`.
* **Bots**: Exclude users by regex + explicit allowlist; expose computed list in outputs.
* **Vendor/Generated files**: Exclude paths via config globs (e.g., `**/package-lock.json`, `**/yarn.lock`, `**/dist/**`, `**/*.min.js`, `**/vendor/**`).
* **First Commit**: Prefer MR commits endpoint; if empty due to squash/rebase, derive from source branch history up to `created_at`.
* **First Review**: First MR note/discussion by someone other than author; exclude system notes.
* **Approval time**: If multiple, take last approval before merge; fallback to earliest approval if merge lacks explicit timestamp.
* **Merge pipeline association**: (a) find pipeline on merge commit SHA; else (b) latest pipeline on MR source SHA before `merged_at`; mark `link_type`.
* **Reverts**: Detect via GitLab revert event or presence of "Revert "…"" commit linking to MR; else match by SHA parentage.
* **Hotfix follow‑up**: Primary via `hotfix` label; else heuristic on title/branch; file‑overlap via MR changes with 1+ common path.
* **Nulls**: If inputs missing, set metric to null and record `null_reason` (e.g., `no_merged_mrs`).
* **Outliers**: Winsorize at 1st/99th percentile for durations; still show raw in audit tables.
* **File‑size filters**: For `small_mr_rate`, apply excludes before counting LOC.

---

## 8) Configuration

JSON (checked into repo as `config.json`):

```json
{
  "windows": {
    "default_days": 28,
    "alt_days": [14, 90]
  },
  "project_scope": {
    "include": ["group/backend/**", "group/frontend/**"],
    "exclude": ["group/archived/**"]
  },
  "file_excludes": [
    "**/package-lock.json",
    "**/yarn.lock",
    "**/dist/**",
    "**/vendor/**",
    "**/*.min.js"
  ],
  "identity": {
    "bot_regex": [".*bot.*", ".*ci.*"],
    "overrides": [
      { "alias": "john.d@toman.ir", "canonical": "john@toman.ir" }
    ]
  },
  "metrics": {
    "small_mr_loc_threshold": 400,
    "flaky_window_hours": 2,
    "hotfix_labels": ["hotfix", "hot-fix"]
  },
  "auth": {
    "gitlab_base_url": "https://gitlab.qcluster.org",
    "token_env": "GITLAB_TOKEN"
  }
}
```

---

## 9) Outputs

### 9.1 Per‑Developer Aggregate (JSON)

Schema:

```json
{
  "schema_version": "developer_metrics.v1",
  "developer_id": "string",
  "canonical_email": "string",
  "window_start": "ISO-8601",
  "window_end": "ISO-8601",
  "low_n": false,
  "metrics": {
    "merged_mrs": 7,
    "cycle_time_median": 46.2,
    "t2_first_review_median": 6.3,
    "small_mr_rate": 0.57,
    "rework_ratio": 0.22,
    "revert_rate": 0.00,
    "mr_pipeline_success_rate": 0.86,
    "mr_pipeline_duration_median": 910,
    "reviews_given": 12,
    "review_response_time_median": 3.1,
    "context_index_projects": 3,
    "...": "other metrics"
  },
  "nulls": { "coverage_delta_median": "coverage_unavailable" },
  "audit": {
    "n_merged_mrs": 7,
    "n_mrs": 9,
    "n_commits": 42
  }
}
```

### 9.2 Machine‑Readable Metric Catalog

One file `metric_catalog.json` with entries like:

```json
{
  "id": "cycle_time_median",
  "name": "Cycle Time (median)",
  "category": "flow",
  "level": "developer",
  "window": "rolling",
  "formula": "median(merged_at - first_commit_at)",
  "unit": "hours",
  "direction": "down",
  "inputs": ["mr.merged_at", "mr.first_commit_at"],
  "null_when": ["no_merged_mrs"],
  "notes": "Exclude bots; UTC; winsorize durations at p1/p99 for display"
}
```

### 9.3 REST API (Internal)

#### Versioning strategy

* Base path: `/api/v{major}` (e.g., `/api/v1`). Clients must include `Accept: application/vnd.toman.dev-metrics+json; version=1` (or `X-Api-Version: 1`) for forward compatibility.
* Breaking changes increment the path segment; additive changes reuse the same major and bump the catalog `schema_version` field.

#### Endpoints (v1)

* `GET /api/v1/metrics/developers?window_days=28&from=2025-09-01&to=2025-09-28&project=group/backend/*`
* `GET /api/v1/metrics/developers/{developer_id}` returns latest aggregate + history sparkline.
* `GET /api/v1/catalog` returns metric catalog (includes `schema_version`).

---

## 10) Validation & Testing

### 10.1 Unit Tests (Deterministic Fixtures)

* **Synthetic repo** with 5 MRs, known timestamps, known approvals & notes, known pipelines; assert each metric against expected.
* **Edge cases**: MR without commits (squash), MR with no review, missing coverage, conflicting MR, revert chain.

### 10.2 Data Quality Checks (DQ)

* **Referential integrity**: every MR maps to a project & author.
* **Monotonicity**: `approved_at ≤ merged_at` (or null).
* **Range checks**: durations within [0h, 720h] post‑winsorization.
* **Population checks**: ≥ 95% active devs present.
* **Completeness**: share of non‑null metrics ≥ 98% where computable.

### 10.3 Acceptance Criteria (Demo)

* Given a 30‑day window, dashboard shows per‑developer rows with **Top 8** metrics and drill‑down to MR list with evidence (timestamps, links).
* Recompute on same fixture -> identical aggregates.
* API latency P95 < 300ms for cached responses.

---

## 11) Security, Privacy, Governance

* Store only GitLab usernames/emails already present on server; no external PII.
* Secrets via Kubernetes secrets/CSI.
* Row‑level access by group/project in API.
* **Anti‑gaming & Ethics**: UI labels metrics as **coaching indicators**; present team roll‑ups by default; show `low_n` warnings.

---

## 12) Performance & Scaling (Postgres‑only)

* Partition large fact tables by `ingestion_date` and/or `project_id`.
* Create **materialized views** for hot aggregations; refresh incrementally post‑ingestion.
* Ensure all queries are window/project selective; avoid full table scans.
* Use **EF Core** compiled queries for hot paths; benchmark with PgBouncer.

---

## 13) Developer Flow & Quality Index (Composite)

Optional composite index in `composites.json`:

```json
{
  "id": "dev_flow_quality_index",
  "components": [
    { "metric": "cycle_time_median", "weight": 0.2, "map": "band_down_0_1" },
    { "metric": "t2_first_review_median", "weight": 0.15, "map": "band_down_0_1" },
    { "metric": "small_mr_rate", "weight": 0.10, "map": "identity" },
    { "metric": "rework_ratio", "weight": 0.15, "map": "invert_identity" },
    { "metric": "revert_rate", "weight": 0.10, "map": "invert_identity" },
    { "metric": "mr_pipeline_success_rate", "weight": 0.10, "map": "identity" },
    { "metric": "mr_pipeline_duration_median", "weight": 0.10, "map": "band_down_0_1" },
    { "metric": "review_response_time_median", "weight": 0.10, "map": "band_down_0_1" }
  ],
  "bands": {
    "cycle_time_median": [24, 48, 96],
    "t2_first_review_median": [2, 8, 24],
    "mr_pipeline_duration_median": [600, 1200, 2400],
    "review_response_time_median": [1, 4, 12]
  },
  "notes": "Band mapping converts raw to [0..1] via piecewise linear across thresholds (good..ok..poor)."
}
```

---

## 14) API Contracts & Worker Scheduling

Automated job orchestration with Hangfire or Aspire background workers is deferred to the next version. For v1, ingestion and aggregation runs are triggered exclusively via the versioned REST APIs (Section 9.3) by automation pipelines or manual operators.

### 14.1 C#/.NET Interfaces (for implementers)

> Provided for Copilot context — not mandatory to implement here.

```csharp
public interface IGitLabClient
{
    IAsyncEnumerable<Project> ListProjectsAsync(string group);
    IAsyncEnumerable<MergeRequest> ListMergeRequestsAsync(long projectId, DateTime updatedAfter, DateTime updatedBefore);
    Task<MrChanges> GetMrChangesAsync(long projectId, int mrIid);
    IAsyncEnumerable<Note> ListMrNotesAsync(long projectId, int mrIid);
    Task<Approvals> GetMrApprovalsAsync(long projectId, int mrIid);
    IAsyncEnumerable<Pipeline> ListPipelinesByShaAsync(long projectId, string sha);
    Task<Pipeline> GetPipelineAsync(long projectId, long pipelineId);
    IAsyncEnumerable<Commit> ListCommitsAsync(long projectId, DateTime since, DateTime until, string? authorEmail = null);
}

public interface IMetricComputer
{
    Task<IReadOnlyList<DeveloperAggregate>> ComputePerDeveloperAsync(Window window, Scope scope, CancellationToken ct = default);
}

// Notes:
// - Compose services with .NET Aspire (resilience, health, telemetry).
// - Use HttpClientFactory + Polly for GitLab calls; EF Core for Postgres persistence.
```

---

## 15) Observability & Ops

* Emit run metrics: ingestion counts, API calls, rate‑limit sleeps, per‑stage durations, error counts.
* Push Prometheus metrics: `metrics_run_duration_seconds`, `gitlab_api_calls_total`, `developer_coverage_ratio`.
* Structured logs with `run_id` and window.
* **.NET specifics**: Use **.NET Aspire** for service composition & dashboards; instrument with **OpenTelemetry (.NET SDK)**

---

## 16) Risks & Mitigations

* **API coverage gaps**: Some fields (coverage, approvals timeline) may be absent → produce nulls with `null_reason`; document in catalog.
* **Data volume spikes**: Use backpressure + persistence; chunk per project.
* **Gaming**: UI copy and reviews emphasize coaching use; composite index weights tunable; expose raw evidence links per metric.

---

## 17) Roadmap (Optional Phases)

* **vNext**: Introduce Hangfire/Aspire background scheduling for automated ingestion and processing triggers.
* **Phase 2**: Add issue linkage, on‑call context, per‑team roll‑ups, and optional warehouse/lake integrations if scale requires.
* **Phase 3**: IDE telemetry (opt‑in), ML/NLP for review quality classification.

---

## 18) Appendix — Minimal Metric JSON Schema

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "title": "DeveloperMetricsAggregate",
  "type": "object",
  "properties": {
    "schema_version": {"type": "string"},
    "developer_id": {"type": "string"},
    "window_start": {"type": "string", "format": "date-time"},
    "window_end": {"type": "string", "format": "date-time"},
    "low_n": {"type": "boolean"},
    "metrics": {"type": "object", "additionalProperties": {"type": ["number", "integer", "null"]}},
    "nulls": {"type": "object", "additionalProperties": {"type": "string"}},
    "audit": {
      "type": "object",
      "properties": {
        "n_merged_mrs": {"type": "integer"},
        "n_mrs": {"type": "integer"},
        "n_commits": {"type": "integer"}
      },
      "required": ["n_merged_mrs", "n_mrs", "n_commits"]
    }
  },
    "required": ["schema_version", "developer_id", "window_start", "window_end", "metrics"]
}
```

---

## 19) Appendix — Example End‑to‑End Pseudocode (LLM‑friendly)

```text
FOR each project IN scope:
  mrs = fetch_mrs(project, updated_after=W.start-7d, updated_before=W.end)
  FOR each mr IN mrs:
    details = fetch_mr_details(mr)
    changes = fetch_mr_changes(mr)
    notes   = fetch_mr_notes(mr)
    approvals = fetch_mr_approvals(mr)
    commits = fetch_commits_for_mr_or_branch(mr)
    merge_pipeline = resolve_merge_pipeline(mr)
    persist_all(...)

per_dev = group_by_developer(entities)
FOR each developer D:
  compute 30 metrics using formulas
  set low_n if sample size < 3 for medians
  write aggregate JSON/CSV row
```

---

## 20) UI Hints (for DevEx Dashboard)

* Default to **team roll‑up**; drill‑down to per‑developer.
* Show **Top 8** with green/amber/red bands; tooltips include formulas and evidence counts.
* Provide trend sparklines (last 6 windows).

---
