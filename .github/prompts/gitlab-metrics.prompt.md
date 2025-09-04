---
mode: agent
---

# Product Requirements Document (PRD)

**Product:** Toman Engineering Metrics (TEM)
**Phase:** V1 — GitLab-only baseline (collection, storage, analysis, exports)
**Owner:** VP of Engineering (you)
**Stakeholders:** CTO, Tech Leads, Platform/Business-line TLs (Corporate Services, Exchange, C-Side)
**Competency Alignment:** DevOps, Git & GitOps, Coding & Code Review, Observability & Optimization (per JD/Framework)

---

## 1) Problem, Goals, Success Criteria

**Problem**
We lack an objective, repeatable baseline of engineering flow, CI health, and Git/GitOps hygiene across Toman’s repos and teams.

**V1 Goal (GitLab-only):**
Collect, compute, and export agreed metrics from GitLab (projects/MRs/commits/pipelines/jobs/releases/issues), sliced by team/service/business-line. Maps to JD & competency KPIs for performance reviews and org experiments.

**Organization slicing**
Dashboards and exports must filter by business line and platform teams (Corporate Services, Exchange, C-Side, Platform/Core).

**Success Criteria (measurable)**

* Coverage: ≥95% of active projects (MR in last 90 days) included per run.
* Freshness: daily aggregates available by 03:00 Europe/Amsterdam.
* Accuracy: ≥98% parity vs spot-checked GitLab UI counts.
* Performance: ≤60 min nightly run for ≤300 projects / ≤10k MRs per month.
* Review-readiness: Exports (JSON & CSV) per team/service ready weekly for exec readouts, mapping to competencies/JD.

**Out of Scope (V1)**
SonarQube, Prometheus/Grafana ingestion (planned V2/V3) though the PRD tags where they’ll land in competency evidence.

---

## 2) Metric Inventory (V1 GitLab-only)

**Flow & Throughput**

* Lead time for change (Issue created → first prod deployment including its MR)
* MR cycle time (first commit on MR branch → merged)
* Time to first review (MR opened → first reviewer comment/approval)
* Time in code review (first review → approvals/met merge)
* Rework rate (% MRs with >1 review round or >N force-pushes)
* MR throughput (# merged per week)
* WIP MR count & WIP age (p50/p90)
* Release cadence (tags/releases per week; SemVer validity)

**CI/CD Health**

* Pipeline success rate
* Mean time to green (failed pipeline → next pass on same ref)
* Avg pipeline duration & critical path duration
* Flaky job rate (fail→rerun→pass with identical SHA)
* Deployment frequency (successful “prod” pipelines/week)
* Rollback incidence (rollback-tagged pipelines or reverted tag within 24h)

**Git/GitOps Hygiene**

* Direct pushes to default branch (non-merge commits)
* Force-pushes on protected branches (heuristic or audit API)
* Approval bypass ratio (merged with approvals < required)
* Signed commit ratio (GPG/SSH signatures on default)
* Branch TTL (creation → merged/deleted; outliers >30 days)

**Issues/Quality Signals**

* Issue SLA breach rate (open beyond target days/labels)
* Reopened issue rate
* Defect escape rate (post-release bugs vs total bugs in window)

> These support JD/competency pillars: DevOps, Git & GitOps, Coding & Code Review, Observability & Optimization (measurement).

---

## 3) Definitions & Inference Rules

* **Prod deployment inference:** pipeline on default branch **and/or** `environment=production` **and/or** pipeline associated with a release tag on that SHA.
* **Lead time for change:** `issue.created_at → first passing prod pipeline` that includes the MR(s) linked to the issue (if multiple MRs, use the last merged MR in that release batch).
* **MR cycle time:** first commit timestamp on MR source branch → `merged_at`.
* **Flaky job:** job fails, is re-run, and passes on same commit SHA (no code delta).
* **Direct push:** commit on default branch not a merge commit and not authored by “Merge Bot”.
* **Approval bypass:** `approvals_given < approvals_required` at merge.
* **Branch TTL:** branch created → MR merged/deleted; report 50/90th and long tail.

All durations computed in UTC, presented in **Europe/Amsterdam**.

---

## 4) High-Level Architecture

**Services**

1. **Collector** (BackgroundService)

   * Discovers projects; pulls MRs, commits (with stats), pipelines, jobs, releases, issues.
   * Rate-limit aware; incremental by `updated_after` watermarks.
2. **Processor**

   * Builds joins MR↔commit↔pipeline; infers prod deployments; computes **facts** & **KPIs**.
3. **Exporter API** (Minimal API)

   * Serves JSON summaries and writes CSV artifacts to persistent storage.
   * Health, readiness, and run controls (kick backfill, re-run window).
4. **Scheduler**

   * Nightly full compute @ 02:00; hourly incrementals (:15) Europe/Amsterdam.
   * Delivered as CronJobs or Quartz triggers.

**Concurrency model**

* Per-namespace/project worker queue; bounded parallelism `min(8, cores)`; `SemaphoreSlim` guarded; **Channels** for ingestion → processing pipelines.

**Security**

* GitLab PAT from K8s Secret (read\_api, read\_repository).
* Export/download endpoints protected by OIDC (internal SSO).
* PII minimization: store email hashes, not raw emails (aligns to governance expectations).

---

## 5) Data Model (PostgreSQL)

### Dimensions

* `dim_project(project_id PK, path_with_namespace, default_branch, visibility, active_flag)`
* `dim_user(user_id PK, username, name, state, is_bot, email_hash)`
* `dim_branch(project_id FK, branch, protected_flag)`
* `dim_release(project_id FK, tag_name, released_at timestamptz, semver_valid bool)`

### Raw Snapshots (upserted by GitLab IDs + `updated_at`)

* `raw_commit(project_id, commit_id PK, author_user_id, committed_at, additions int, deletions int, is_signed bool)`
* `raw_mr(project_id, mr_id PK, author_user_id, created_at, merged_at, closed_at, state, changes_count, source_branch, target_branch, approvals_required, approvals_given, first_review_at)`
* `raw_pipeline(project_id, pipeline_id PK, sha, ref, status, created_at, updated_at, duration_sec int, environment)`
* `raw_job(project_id, job_id PK, pipeline_id, name, status, duration_sec, started_at, finished_at, retried_flag bool)`
* `raw_issue(project_id, issue_id PK, author_user_id, created_at, closed_at, state, reopened_count int, labels jsonb)`

### Derived Facts

* `fact_mr(mr_id PK, project_id, cycle_time_hours numeric, review_wait_hours numeric, rework_count int, lines_added int, lines_removed int)`
* `fact_pipeline(pipeline_id PK, project_id, mtg_seconds int, is_prod bool, is_rollback bool, is_flaky_candidate bool, duration_sec int)`
* `fact_git_hygiene(project_id, day date, direct_pushes_default int, force_pushes_protected int, unsigned_commit_count int)`
* `fact_release(tag_name, project_id, is_semver bool, cadence_bucket text)`

### Operational

* `ingestion_state(entity text PK, last_seen_updated_at timestamptz, last_run_at timestamptz)`

---

## 6) Configuration & Org Mapping

**ENV (required)**
`GITLAB_API_URL`, `GITLAB_API_TOKEN`, `TEM_DB_CONNECTION`, `TEM_EXPORT_DIR`, `TIMEZONE=Europe/Amsterdam`.

**Org Scope**
A YAML/JSON file maps **root groups → business lines/teams** (Corporate Services, Exchange, C-Side, Core/Platform) so slicing reflects Toman’s structure.

---

## 7) External Interfaces (Minimal API)

* `GET /healthz` – liveness
* `GET /readyz` – readiness (DB & token check)
* `POST /runs/backfill?days=180` – trigger backfill
* `POST /runs/incremental` – trigger incremental
* `GET /exports/daily/{yyyy-mm-dd}.json` – merged org/team/repo summaries
* `GET /exports/daily/{yyyy-mm-dd}.csv` – timeseries per metric key
* `GET /status` – last run stats, coverage %, lag, API 429 counts

**Export JSON contract (example)**

```json
{
  "date": "2025-09-04",
  "org": "Toman",
  "team": "Platform",
  "repo": "checkout",
  "metrics": {
    "mr_cycle_time_p50_h": 21.3,
    "pipeline_success_rate": 0.92,
    "deployment_frequency_wk": 14,
    "approval_bypass_ratio": 0.03
  }
}
```

---

## 8) Processing Algorithm (deterministic spec)

1. **Discover Projects:** list/group projects; cache `project_id`, `default_branch`.
2. **Ingest (Backfill, then Incremental):** pull paged MRs, commits(+stats), pipelines, jobs, releases, issues; upsert by IDs; watermark on `updated_at`.
3. **Link & Infer:**

   * MR↔Commit via merge requests changes API & branch commits.
   * Pipeline↔MR via commit SHA and ref.
   * Prod deployment per inference rules (§3).
4. **Compute Facts:** populate `fact_*` and roll daily aggregates.
5. **Export:** write JSON/CSV files to `TEM_EXPORT_DIR` and serve via API.
6. **Quality Gates:** 20 random MRs/week parity check; negative/zero duration guards; time-zone normalization.

---

## 9) Non-Functional Requirements

* **Reliability:** Resume after 429/5xx (Polly jittered backoff, respect rate-limit headers).
* **Idempotency:** Upserts keyed by GitLab IDs + `updated_at` watermark.
* **Observability:** OTel traces for API calls; self-metrics: rows/sec, API errors, lag, run duration; structured logs.
* **Security:** No raw emails; only `email_hash`.
* **Performance:** Targets in §1; bounded concurrency with backpressure.

---

## 10) Acceptance Tests (must pass)

* **AT-01** Pipeline success rate for repo X last 30 days within ±1 pipeline vs GitLab UI.
* **AT-02** MR `!N` cycle time = (first commit → merged\_at) ±1 minute.
* **AT-03** MTG > 0 when a failed pipeline is followed by a passing one on same ref.
* **AT-04** Direct push to `main` increments `direct_pushes_default` for that date.
* **AT-05** Non-SemVer tag sets `semver_valid=false`.
* **AT-06** Export contains org/team mapping for Corporate Services/Exchange/C-Side/Core.
* **AT-07** Coverage ≥95% of active projects; report list of excluded with reasons.

---

## 11) Delivery Plan (engineering backlog)

**P0**

1. Project discovery & watermarks
2. Raw entity ingestion (MRs/commits/pipelines/jobs/releases/issues)
3. Linkage and prod inference
4. Core flow metrics (cycle/lead/review)
5. CI metrics (success, MTG, duration, flaky heuristic)
6. Git/GitOps hygiene metrics

**P1**
7\) Exports + Minimal API + auth
8\) K8s Manifests (Deployment + CronJobs + Secrets/ConfigMaps)
9\) QA harness & AT suite

**P2**
10\) Team/business-line mapping file & validations
11\) Packaging (Helm), runbooks, admin dashboard

> Future phases (separate PRDs): **V2 SonarQube** (coverage, bugs, maintainability) and **V3 Observability** (SLIs/SLOs from Prometheus), tied to competencies “Coding Abilities/Code Review” and “Observability & Optimization”.

---

## 12) Reporting → Competency/JD Evidence

Each dashboard tile must be labeled to a competency/KPI so TLs and Execs can review against the framework, e.g.:

* **DevOps** → Deployment frequency, MTG, rollback time.
* **Git & GitOps** → Approval bypass, direct/force pushes, GitOps maturity proxies.
* **Coding & Code Review** → Time to first review, review time, rework rate.
* **Observability & Optimization (measurement)** → Pipeline duration/critical path as efficiency proxies; more in V3.

---

## 13) Operational Runbook (capsule)

* Nightly **02:00** compute, hourly incrementals **:15**.
* If a run misses SLA (not done by 03:00), auto-page TL channel and auto-retry once with reduced parallelism.
* Weekly parity sample (20 MRs); if parity <98%, flag “Data Quality Red” and open an incident for RCA.

---

## 14) Risk Register & Mitigations

* **GitLab rate limits:** Adaptive pacing via headers + Polly + rolling watermarks.
* **Heuristic misclassification (prod/rollback):** Keep rule set declarative & overrideable per repo.
* **Identity mapping gaps:** Use usernames + email hash; later join to HRIS when approved.
* **Schema drift:** Migrations gated; backward-compatible DTOs with source generators.

---

## 15) Definition of Done (V1)

* Container images published; Helm chart delivered.
* Postgres schema migrated; **/healthz**/**/readyz** green in staging & prod.
* Nightly/Hourly jobs deployed; exports available for all four org slices.
* AT-01…AT-07 all passing; parity ≥98%; coverage ≥95%.
* Executive JSON and CSV deliverables validated in a weekly review.

---

### Appendix A — Minimal `appsettings.json`

```json
{
  "GitLab": {
    "BaseUrl": "__GITLAB_API_URL__",
    "Token": "__GITLAB_API_TOKEN__",
    "RootGroups": ["toman/core", "toman/corporate-services", "toman/exchange", "toman/c-side"]
  },
  "Database": { "ConnectionString": "__TEM_DB_CONNECTION__" },
  "Exports": { "Directory": "/data/exports" },
  "Timezone": "Europe/Amsterdam",
  "Processing": { "MaxDegreeOfParallelism": 8, "BackfillDays": 180 }
}
```
