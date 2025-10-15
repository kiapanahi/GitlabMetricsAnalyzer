# GitLab Metrics Analyzer - Cleanup & Consolidation Plan

**Created**: October 15, 2025  
**Status**: Planning  
**Author**: VP of Engineering

## Executive Summary

The project has undergone a significant architectural shift from a **data collection and storage approach** to a **live API-based metrics calculation approach**. This has resulted in substantial amounts of stale code, documentation, and planning documents that reference the old architecture.

### Architecture Evolution

**OLD APPROACH (v0.x)**:
```
GitLab API → Data Collection Service → PostgreSQL Storage → Metrics Calculation from DB
```

**NEW APPROACH (v1.0 - Current)**:
```
GitLab API ← Live API Calls ← Metrics Services → REST API Endpoints
```

## Stale & Outdated Assets

### 1. 🗄️ Database-Related Code (Potentially Unused)

#### Models & Entities
**Location**: `src/Toman.Management.KPIAnalysis.ApiService/Features/GitLabMetrics/Models/`

##### Raw Models
- ✗ `Raw/RawCommit.cs` - Raw commit snapshots
- ✗ `Raw/RawMergeRequest.cs` - Raw MR snapshots
- ✗ `Raw/RawMergeRequestNote.cs` - Raw review comments
- ✗ `Raw/RawPipeline.cs` - Raw pipeline data
- ✗ `Raw/RawJob.cs` - Raw CI job data
- ⚠️ `Raw/GitLabApiModels.cs` - DTOs (may be used by live API)
- ⚠️ `Raw/GitLabContributedProject.cs` - Project data (may be used)

##### Fact Tables
- ✗ `Facts/FactMergeRequest.cs` - MR fact table
- ✗ `Facts/FactPipeline.cs` - Pipeline fact table
- ✗ `Facts/FactUserMetrics.cs` - User metrics fact table
- ✗ `Facts/FactGitHygiene.cs` - Git hygiene facts
- ✗ `Facts/FactRelease.cs` - Release facts

##### PRD Entities
- ✗ `Entities/DeveloperMetricsAggregate.cs` - Pre-calculated aggregates
- ✗ `Entities/CommitFact.cs` - Commit facts
- ✗ `Entities/MergeRequestFact.cs` - MR facts
- ✗ `Entities/PipelineFact.cs` - Pipeline facts
- ✗ `Entities/ReviewEvent.cs` - Review events
- ✗ `Entities/Developer.cs` - Developer identity
- ✗ `Entities/DeveloperAlias.cs` - Developer aliases
- ✗ `Entities/Project.cs` - Project metadata

##### Operational Tables
- ✗ `Operational/IngestionState.cs` - Collection state tracking
- ✗ `Operational/CollectionRun.cs` - Collection run metadata

##### Dimension Tables
- ✗ `Dimensions/DimUser.cs` - User dimension
- ✗ `Dimensions/DimBranch.cs` - Branch dimension
- ✗ `Dimensions/DimRelease.cs` - Release dimension

**Impact**: These models are still registered in `GitLabMetricsDbContext` and have migrations. If not used, they add maintenance burden and confusion.

#### Database Context
**Location**: `src/Toman.Management.KPIAnalysis.ApiService/Features/GitLabMetrics/Data/`

- ⚠️ `GitLabMetricsDbContext.cs` - Contains 15+ DbSet properties that may be unused
  - Lines 17-45: All entity DbSets
  - Lines 50-694: Entity configurations

**Investigation Needed**: Determine which (if any) entities are still actively used by the live metrics services.

#### Migrations
**Location**: `src/Toman.Management.KPIAnalysis.ApiService/Migrations/`

- ✗ `20250914091532_InitialGitLabMetrics.cs` - Initial schema
- ✗ `20250914122229_Consolidate.cs` - Consolidation
- ✗ `20250915111359_Add-Id-To-IngestionState-Model.cs`
- ✗ `20250923072442_AddFactUserMetrics.cs`
- ✗ `20250925005249_AddIssueAssigneeUserId.cs`
- ✗ `20250926185325_AddPrdEntities.cs`
- ✗ `20250926185625_RemoveObsoleteTables.cs`
- ✗ `20250927091225_AddWindowedCollectionSupport.cs`
- ✗ `20250927091902_EnhanceRawModelsForEnrichment.cs`
- ✗ `20250927185020_UpdateDeveloperMetricsAggregateForPRD.cs`
- ✗ `20250929134336_ConvertDateTimeOffsetToDateTime.cs`
- ✗ `20251004093505_RemoveRunTypeColumn.cs`
- ✗ `GitLabMetricsDbContextModelSnapshot.cs`

**Count**: 30+ migration files totaling ~10,000+ lines of code

**Impact**: If database is not used, these can all be removed. If partially used, may need to keep some.

---

### 2. 📝 Documentation Files (Outdated Content)

#### README.md
**Status**: ⚠️ Major updates needed  
**Location**: Root

**Stale Sections**:
- Lines 3-4: "collects developer productivity metrics from GitLab and stores them in PostgreSQL"
- Lines 7: "Data Collection: Collects commits, merge requests, and pipeline data from GitLab API"
- Lines 14: "Tech Stack: .NET 9, Entity Framework Core, PostgreSQL"
- Lines 17: "Data Collection: Manual trigger workflows for on-demand collection"
- Lines 26-38: Entire "Database Setup" section
- Lines 49: PostgreSQL connection string configuration
- Lines 97-105: "Database Migration" section
- Lines 107-130: "Manual Data Collection" section with backfill/incremental collection
- Lines 143-157: Data Models section (all entity references)

**Correct Content Should Be**:
- Architecture: Live API-based metrics calculation
- No database required (or minimal for caching only)
- Focus on available metrics endpoints
- Remove all collection/seeding references

#### docs/OPERATIONS_RUNBOOK.md
**Status**: ⚠️ Major overhaul needed  
**Location**: `docs/`

**Stale Sections**:
- Lines 1-27: System Overview - "Stores data in PostgreSQL", "Manual trigger workflow"
- Lines 29-55: "Daily Operations" - Trigger Incremental Collection
- Lines 56-70: Monitor Collection Status
- Lines 84-90: Review recent collection success rates
- Lines 98-110: "Full Backfill (if needed)"
- Lines 122-125: Collection Health KPIs
- Lines 150+: Entire sections on collection monitoring

**Should Be Replaced With**:
- Live API monitoring
- GitLab API health checks
- Rate limiting considerations
- Performance optimization for API calls

#### docs/DATA_RESEEDING_GUIDE.md
**Status**: ✗ Entire document obsolete  
**Location**: `docs/`

**Problem**: Entire 216-line document about re-seeding database data
- All endpoints referenced don't exist or shouldn't exist
- Concepts of "incremental collection", "backfill", "reset raw data" are obsolete

**Action**: Delete or archive

#### docs/DEPLOYMENT_GUIDE.md
**Status**: ⚠️ Major updates needed  
**Location**: `docs/`

**Stale Sections**:
- Lines 1-50: Architecture diagrams showing PostgreSQL, data collection
- Lines 51-100: Database Setup section
- PostgreSQL configuration and optimization
- Database migration procedures
- Partitioning strategies

**Keep**: 
- .NET 9 deployment
- Aspire configuration
- GitLab API configuration
- Monitoring setup

**Remove/Update**:
- All database-related deployment steps

#### docs/CONFIGURATION_GUIDE.md
**Status**: ⚠️ Partial updates needed  
**Location**: `docs/`

**Stale Sections**:
- Database configuration section
- Processing configuration (backfill, parallelism for collection)
- ConnectionStrings configuration

**Keep**:
- GitLab API configuration
- Metrics configuration (bot patterns, excludes)
- Logging configuration

#### docs/API_USAGE_GUIDE.md
**Status**: ⚠️ Updates needed  
**Location**: `docs/`

**Stale Sections**:
- Line 19: "Manual data collection triggers"
- Lines 836, 873: References to collection errors and data gaps
- Any endpoints related to `/gitlab-metrics/collect/*`

**Action**: Update to reflect only live metrics APIs

#### Feature Summary Documents
**Status**: ⚠️ May need review  
**Location**: `docs/`

- `COMMIT_TIME_ANALYSIS_FEATURE_SUMMARY.md` - States "fetches data directly from GitLab (not from the stored database)" ✅ (Correct!)
- `QUALITY_METRICS_FEATURE_SUMMARY.md` - May reference data collection
- `PIPELINE_METRICS_FEATURE_SUMMARY.md` - May reference data collection
- `CODE_CHARACTERISTICS_FEATURE_SUMMARY.md` - May reference data collection
- `ADVANCED_METRICS_FEATURE_SUMMARY.md` - May reference data collection

**Action**: Review each for references to database storage/collection

---

### 3. 📋 PRD Files (Outdated Architecture)

#### prds/developer-productivity-metrics.md
**Status**: ⚠️ Major updates needed  
**Location**: `prds/`

**Stale Content**:
- Lines 15-18: "GitLab data is available and mapped to internal developer identities via `IdentityMapping`"
- Line 23: "Existing database raw tables (if present) under `GitLabMetrics` feature"
- All references to database storage and collection

**Action**: Rewrite to reflect live API approach or archive

#### prds/gitlab-developer-productivity-metrics.md
**Status**: ⚠️ Major updates needed  
**Location**: `prds/`

**Stale Content**:
- Lines 1-100: Extensive sections on:
  - "Data Model" with entity descriptions
  - "Storage (v1)" - "Postgres (single store) for incremental sync state"
  - "Ingestion & API Usage"
  - Database partitioning strategies
  - Materialized views

**Action**: Complete rewrite or archive

#### prds/active-development-time.md
**Status**: ⚠️ Complete overhaul needed  
**Location**: `prds/`

**Stale Content**:
- Lines 10-11: "GitLab commits (timestamp, author, commit id, project)"
- Lines 15-21: "Raw commits table", "Fact table `FactDeveloperActiveTime`"
- Lines 47-51: "DB schema suggestion"
- Database-centric computation algorithm

**Action**: Rewrite for live API approach or mark as archived

#### prds/review-turnaround-time.md
**Status**: ⚠️ Needs review  
**Location**: `prds/`

**Action**: Check if references database storage

#### docs/PRD_ENTITY_DESIGN.md
**Status**: ✗ Entire document obsolete  
**Location**: `docs/`

**Problem**: 291-line document entirely about database entity design
- SQL schemas
- Table structures
- Relationships
- Partitioning strategies

**Action**: Delete or move to archive folder

---

### 4. 📄 Request Files (Outdated)

#### requests/000-delete-unused-features.md
**Status**: ⚠️ Ironic - This is about deleting old code  
**Location**: `requests/`

**Content**: Document about removing unused collection services
- Lists services to delete
- Most of these still exist in codebase

**Action**: Either execute this plan or update based on current state

#### requests/001-create-metric-prds.md
**Status**: ⚠️ Needs review  
**Location**: `requests/`

**Action**: Check if still relevant or if PRDs need different approach

#### requests/002-active-development-time.md
**Status**: ⚠️ Needs review  
**Location**: `requests/`

**Action**: Check if references old DB approach

---

### 5. ✅ Current Working Code (Keep!)

#### Services That Work Correctly
**Location**: `src/Toman.Management.KPIAnalysis.ApiService/Features/GitLabMetrics/Services/`

These services make LIVE API calls to GitLab:
- ✅ `CommitTimeAnalysisService.cs` - Live commit time analysis
- ✅ `PerDeveloperMetricsService.cs` - Live per-dev metrics
- ✅ `CollaborationMetricsService.cs` - Live collaboration metrics
- ✅ `QualityMetricsService.cs` - Live quality metrics
- ✅ `CodeCharacteristicsService.cs` - Live code characteristics
- ✅ `PipelineMetricsService.cs` - Live pipeline metrics
- ✅ `AdvancedMetricsService.cs` - Live advanced metrics

#### Endpoints That Work Correctly
**Location**: `src/Toman.Management.KPIAnalysis.ApiService/Features/GitLabMetrics/`

- ✅ `UserMetricsEndpoints.cs` - User-level metrics API
- ✅ `PipelineMetricsEndpoints.cs` - Pipeline metrics API
- ✅ `AdvancedMetricsEndpoints.cs` - Advanced metrics API

#### Infrastructure
- ✅ `GitLabHttpClient.cs` - HTTP client for GitLab API calls
- ✅ `GitLabService.cs` - GitLab API service
- ✅ `ServiceCollectionExtensions.cs` - DI configuration (review needed)

---

## Investigation Required

### Questions to Answer Before Cleanup

1. **Database Usage**
   - ❓ Is PostgreSQL still used AT ALL?
   - ❓ If yes, for what specifically? (caching? state tracking?)
   - ❓ Are any entities/tables actively queried by live metrics services?

2. **Collection Endpoints**
   - ❓ Do `/gitlab-metrics/collect/*` endpoints still exist?
   - ❓ Are they in use or referenced anywhere?
   - ❓ Should they be removed completely?

3. **Configuration**
   - ❓ Is `ConnectionStrings:DefaultConnection` still required?
   - ❓ Is `Processing.BackfillDays` still used?
   - ❓ Is `Exports.Directory` still used?

4. **Migrations**
   - ❓ Do we need to maintain migration history?
   - ❓ Can we start fresh with new migrations if DB is needed?

---

## Recommended Actions

### Phase 1: Investigation & Documentation (Week 1)

**Goal**: Understand current state accurately

1. **Code Analysis**
   - [ ] Search for all database queries in services
   - [ ] Identify which DbSets are actually used
   - [ ] Find all references to `CollectionRun`, `IngestionState`
   - [ ] Check if any endpoints use EF Core queries

2. **Endpoint Audit**
   - [ ] List all active API endpoints
   - [ ] Verify which endpoints are documented vs. implemented
   - [ ] Test collection endpoints (if they exist)

3. **Configuration Audit**
   - [ ] Review all `appsettings.json` settings
   - [ ] Identify which settings are actually used
   - [ ] Check for database connection strings in production

4. **Document Findings**
   - [ ] Create `CURRENT_STATE.md` with accurate architecture
   - [ ] List all active vs. inactive features
   - [ ] Map data flow diagrams

### Phase 2: Quick Wins (Week 1-2)

**Goal**: Clean up obviously outdated content

1. **Documentation**
   - [ ] Update README.md to reflect live API architecture
   - [ ] Archive or delete `DATA_RESEEDING_GUIDE.md`
   - [ ] Archive or delete `PRD_ENTITY_DESIGN.md`
   - [ ] Add warning banners to stale docs

2. **Request Files**
   - [ ] Execute or archive `000-delete-unused-features.md`
   - [ ] Review and update other request files
   - [ ] Clean up stale GitHub issues (if any)

3. **Configuration**
   - [ ] Remove unused configuration sections
   - [ ] Update configuration examples
   - [ ] Clean up `appsettings.json` comments

### Phase 3: Code Cleanup (Week 2-3)

**Goal**: Remove unused code safely

1. **Database Layer** (If confirmed unused)
   - [ ] Remove all entity models not in use
   - [ ] Delete migrations (keep snapshot only if needed)
   - [ ] Simplify or remove `GitLabMetricsDbContext`
   - [ ] Remove EF Core packages if not needed

2. **Services** (If collection services exist)
   - [ ] Remove collection services
   - [ ] Remove data enrichment services
   - [ ] Remove metrics calculation from DB services
   - [ ] Clean up DI registrations

3. **Endpoints** (If collection endpoints exist)
   - [ ] Remove collection endpoints
   - [ ] Remove export endpoints (if based on DB)
   - [ ] Remove data quality endpoints (if DB-based)

### Phase 4: Consolidation (Week 3-4)

**Goal**: Create clean, maintainable structure

1. **Documentation Rewrite**
   - [ ] Rewrite README.md completely
   - [ ] Create new ARCHITECTURE.md
   - [ ] Update API_USAGE_GUIDE.md
   - [ ] Rewrite DEPLOYMENT_GUIDE.md
   - [ ] Update OPERATIONS_RUNBOOK.md

2. **PRD Updates**
   - [ ] Archive old PRDs in `prds/archived/`
   - [ ] Create new PRDs reflecting live API approach
   - [ ] Document current metrics catalog

3. **Testing**
   - [ ] Remove tests for deleted features
   - [ ] Update integration tests
   - [ ] Add new tests for live metrics

4. **Project Cleanup**
   - [ ] Remove unused NuGet packages
   - [ ] Clean up project references
   - [ ] Update CI/CD pipelines
   - [ ] Remove unused configuration

---

## Risk Assessment

### High Risk
- **Accidental deletion of active code**: Mitigate with thorough investigation first
- **Breaking existing integrations**: Document all endpoints before changes
- **Data loss**: Ensure backups if database contains anything important

### Medium Risk
- **Documentation out of sync**: Update incrementally and test
- **Configuration issues**: Test in dev environment first
- **Missing features**: Create feature inventory before cleanup

### Low Risk
- **Stale documentation**: Safe to update anytime
- **Unused code**: Safe to remove after verification
- **Old PRDs**: Safe to archive

---

## Success Criteria

### Clean Codebase
- [ ] No unused database models or migrations
- [ ] All code has clear purpose
- [ ] No stale endpoints or services
- [ ] Clean DI registration

### Accurate Documentation
- [ ] README accurately describes architecture
- [ ] All docs reflect current implementation
- [ ] API documentation matches actual endpoints
- [ ] Deployment guide is actionable

### Clear Project Direction
- [ ] PRDs reflect current approach
- [ ] Feature roadmap is clear
- [ ] Technical debt is documented
- [ ] Team understands architecture

---

## Estimated Effort

- **Investigation**: 3-5 days
- **Documentation Updates**: 5-7 days
- **Code Cleanup**: 7-10 days
- **Testing & Validation**: 3-5 days
- **Total**: 3-4 weeks (1 person) or 2 weeks (2 people)

---

## Next Steps

1. **Approve this cleanup plan**
2. **Start Phase 1 investigation**
3. **Create tracking issues for each phase**
4. **Schedule cleanup sprints**
5. **Execute phase by phase with reviews**

---

## Appendix: Files to Review

### Code Files
```
src/Toman.Management.KPIAnalysis.ApiService/
├── Features/GitLabMetrics/
│   ├── Data/GitLabMetricsDbContext.cs
│   ├── Models/
│   │   ├── Raw/*.cs (10 files)
│   │   ├── Facts/*.cs (5 files)
│   │   ├── Entities/*.cs (9 files)
│   │   ├── Operational/*.cs (2 files)
│   │   └── Dimensions/*.cs (3 files)
│   ├── Services/*.cs (14 files - review each)
│   └── *Endpoints.cs (4 files)
└── Migrations/*.cs (30+ files)
```

### Documentation Files
```
docs/
├── OPERATIONS_RUNBOOK.md (500 lines) ⚠️
├── DATA_RESEEDING_GUIDE.md (216 lines) ✗
├── DEPLOYMENT_GUIDE.md (887 lines) ⚠️
├── CONFIGURATION_GUIDE.md (735 lines) ⚠️
├── API_USAGE_GUIDE.md (1064 lines) ⚠️
└── PRD_ENTITY_DESIGN.md (291 lines) ✗

prds/
├── developer-productivity-metrics.md ⚠️
├── gitlab-developer-productivity-metrics.md (645 lines) ⚠️
└── active-development-time.md ⚠️

requests/
├── 000-delete-unused-features.md ⚠️
├── 001-create-metric-prds.md ⚠️
└── 002-active-development-time.md ⚠️
```

### Legend
- ✗ = Delete/Archive
- ⚠️ = Update Required
- ✅ = Keep As-Is

---

**Document Version**: 1.0  
**Last Updated**: October 15, 2025
