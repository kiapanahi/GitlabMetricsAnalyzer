# Changelog

All notable changes to GitLab Metrics Analyzer will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.0.0] - 2025-10-16

### 🎉 Major Architecture Consolidation

This is a **major architectural release** that consolidates the project from a database-centric approach to a pure live API-based metrics calculation system.

### What Changed

**Old Architecture (v1.x)**:
```
GitLab API → Data Collection Service → PostgreSQL Storage → Metrics Calculation → API
```

**New Architecture (v2.0)**:
```
GitLab API ← Live API Calls ← Metrics Services → REST API Endpoints
```

### Added
- ✅ **Live Metrics Calculation**: All metrics calculated on-demand from GitLab API
- ✅ **10 REST API Endpoints**: Comprehensive metrics for users, teams, projects, and pipelines
- ✅ **Real-time Insights**: No stale data - always current metrics
- ✅ **Comprehensive Documentation**: 
  - `CURRENT_STATE.md` - Accurate architecture documentation
  - `archive/INVESTIGATION_REPORT.md` - Detailed analysis of architecture (archived)
  - `ENDPOINT_AUDIT.md` - Complete API endpoint documentation
  - Updated README with v2.0 architecture
- ✅ **Resilient GitLab Integration**: Polly policies (retry, circuit breaker, timeout)
- ✅ **Flexible Time Windows**: Query metrics for any period (1-365 days)
- ✅ **Bot Filtering**: Configurable regex patterns to exclude bot accounts
- ✅ **Commit/Branch/File Exclusions**: Flexible filtering patterns

### Removed
- ❌ **PostgreSQL Database Infrastructure**: ~3,000 lines removed
  - 30+ EF Core migration files (~10,000 lines)
  - 29+ entity/model classes for database storage
  - 20+ DbSet properties in DbContext
  - All database configuration and setup
- ❌ **Data Collection Services**: Batch collection workflows removed
- ❌ **ETL/Seeding Operations**: No longer needed with live API approach
- ❌ **Historical Data Storage**: No database storage means no stale data

### Changed
- 🔄 **Configuration Simplified**: Removed database connection strings and collection settings
- 🔄 **Deployment Simplified**: No database setup or migrations required
- 🔄 **Operations Simplified**: No background jobs or data collection to monitor

### Breaking Changes

⚠️ **This is a breaking release** if you were using v1.x (database-based approach):

1. **No Data Migration Path**: v2.0 does not use a database
   - Previous stored data cannot be migrated
   - All metrics are now calculated live from GitLab API

2. **Configuration Changes**:
   - Removed: `ConnectionStrings:PostgresDatabase`
   - Removed: `Collection` configuration section
   - Removed: `Exports` configuration section
   - Required: `GitLab:BaseUrl` and `GitLab:Token`

3. **Deployment Changes**:
   - No PostgreSQL database required
   - No database migrations to run
   - Simplified deployment process

4. **API Behavior**:
   - All metrics calculated on-demand (may have higher latency for first request)
   - No pre-calculated aggregates
   - Time window applies to GitLab API queries

### Migration Guide

**From v1.x to v2.0**:

1. **Configuration**:
   ```json
   {
     "GitLab": {
       "BaseUrl": "https://your-gitlab-instance.com",
       "Token": "your-personal-access-token"
     },
     "Metrics": {
       "Identity": {
         "BotRegexPatterns": ["^.*bot$", "^.*\\[bot\\]$"]
       },
       "Excludes": {
         "CommitPatterns": ["^Merge branch.*"],
         "BranchPatterns": ["^dependabot/.*"],
         "FilePatterns": ["^.*\\.min\\.(js|css)$"]
       }
     }
   }
   ```

2. **Environment Variables**:
   ```bash
   export GitLab__Token="your-gitlab-token"
   export GitLab__BaseUrl="https://your-gitlab-instance.com"
   ```

3. **Deployment**:
   - Remove PostgreSQL database setup
   - Remove database connection strings
   - Deploy API service only
   - No migration commands needed

4. **API Usage**:
   - Use same endpoints as before
   - Add `windowDays` parameter for time range (default: 30)
   - First request may take longer (calculating live)
   - Subsequent requests benefit from GitLab API caching

### Technical Details

**Phase 1: Investigation & Documentation** (PR #107)
- Analyzed database usage across entire codebase
- Created comprehensive investigation reports
- Confirmed 100% of database infrastructure was unused
- Documented actual architecture in `CURRENT_STATE.md`

**Phase 3: Code Cleanup** (PR #109)
- Removed all database models and entities
- Removed all EF Core migrations
- Removed DbContext registration and configuration
- Updated dependency injection
- Cleaned up unused NuGet packages
- All tests updated and passing (56/56)

**Phase 4: Consolidation** (This PR)
- Version bumped to 2.0.0 (MAJOR release for breaking changes)
- Created CHANGELOG.md
- Final documentation review
- Release preparation

### API Endpoints (v2.0)

All endpoints calculate metrics live from GitLab API:

**User Metrics**:
- `GET /api/v1/{userId}/analysis/commit-time` - Commit time distribution
- `GET /api/v1/{userId}/metrics/mr-cycle-time` - MR cycle time (P50)
- `GET /api/v1/{userId}/metrics/flow` - Throughput, WIP, coding time
- `GET /api/v1/{userId}/metrics/collaboration` - Reviews, approvals, discussions
- `GET /api/v1/{userId}/metrics/quality` - Rework, reverts, CI success
- `GET /api/v1/{userId}/metrics/code-characteristics` - Commit size, MR size, file churn

**Pipeline Metrics**:
- `GET /api/v1/metrics/pipelines/{projectId}` - CI/CD pipeline health

**Advanced Metrics**:
- `GET /api/v1/metrics/advanced/{userId}` - Bus factor, response time, batch size

**Team & Project Metrics**:
- `GET /api/v1/teams/{teamId}/metrics` - Team velocity, cross-project contributions
- `GET /api/v1/projects/{projectId}/metrics` - Activity score, branch lifecycle

### Performance Characteristics

**v2.0 Performance Profile**:
- **First Request**: Higher latency (live calculation from GitLab API)
- **Calculation Time**: Depends on time window and data volume
- **GitLab API Limits**: Subject to GitLab API rate limits
- **Caching**: Benefits from GitLab API's internal caching
- **Scalability**: Horizontally scalable (stateless API)

**Trade-offs**:
- ✅ Always current data (no staleness)
- ✅ Simplified architecture (no database maintenance)
- ✅ Lower operational complexity (no background jobs)
- ⚠️ Higher per-request latency (live calculation)
- ⚠️ Subject to GitLab API rate limits
- ⚠️ No historical data beyond GitLab retention

### Metrics Available

**Developer Productivity Metrics**:
- Commit time distribution and patterns
- MR cycle time (median)
- Flow metrics (throughput, WIP, coding time, review time, context switching)
- Collaboration metrics (reviews, approvals, discussions, turnaround time)
- Quality metrics (rework ratio, revert rate, CI success, hotfix rate)
- Code characteristics (commit frequency, size, file churn, message quality)

**Team & Project Metrics**:
- Team velocity and cross-project contributions
- Review coverage and distribution
- Project activity score
- Branch lifecycle metrics
- Milestone completion tracking

**Pipeline Metrics**:
- Failed job rate and retry rate
- Wait time and deployment frequency
- Duration trends and success rate
- Coverage trends

**Advanced Analytics**:
- Bus factor (knowledge distribution risk)
- Response time distribution
- Batch size analysis
- Draft duration tracking
- Iteration count metrics
- Idle time in review
- Cross-team collaboration index

### Documentation

**Core Documentation**:
- `README.md` - Quick start and overview
- `CURRENT_STATE.md` - Current architecture (100% accurate)
- `ENDPOINT_AUDIT.md` - Complete API reference
- `CONFIGURATION_GUIDE.md` - Configuration options
- `DEPLOYMENT_GUIDE.md` - Deployment instructions
- `archive/INVESTIGATION_REPORT.md` - Phase 1 findings (archived)
- `archive/CONFIGURATION_REVIEW.md` - Configuration analysis (archived)

**Feature Documentation**:
- Multiple feature-specific docs in `docs/` covering individual metrics

**Archived Documentation**:
- Obsolete docs moved to `docs/archived/`
- All obsolete sections clearly marked with warnings

### Testing

- ✅ **56 unit tests** passing
- ✅ **Build succeeds** (70.7s)
- ✅ **All metrics services** tested
- ✅ **Integration tests** for GitLab API client

### Technology Stack

- **.NET 9** with C# latest features
- **ASP.NET Core Minimal APIs**
- **.NET Aspire** for orchestration
- **NGitLab** for GitLab API integration
- **Polly** for resilience (retry, circuit breaker, timeout)
- **Serilog** for structured logging
- **OpenTelemetry** for distributed tracing

### Dependencies

**Required**:
- .NET 9 SDK
- GitLab instance with API access
- GitLab Personal Access Token with `api` scope

**Not Required** (removed):
- ~~PostgreSQL database~~
- ~~Entity Framework Core migrations~~
- ~~Background job processor~~

### Contributors

**Architecture Consolidation Team**:
- Phase 1: Investigation & Documentation
- Phase 2: Documentation Cleanup
- Phase 3: Code Cleanup (Database removal)
- Phase 4: Final Consolidation & v2.0 Release

### Related Issues

- Epic: #106 - Architecture Consolidation - v2.0 Migration
- Phase 1: #102 - Investigation & Documentation (✅ Complete)
- Phase 2: #103 - Quick Wins - Documentation Cleanup (✅ Complete)
- Phase 3: #104 - Code Cleanup (✅ Complete - PR #109)
- Phase 4: #105 - Consolidation (✅ Complete - This release)

### Future Plans

**Potential Future Enhancements** (out of scope for v2.0):
- Caching layer for frequently requested metrics
- Batch API requests for multiple users
- WebSocket support for real-time updates
- Dashboard UI for metrics visualization
- Export to various formats (CSV, JSON, Excel)
- Custom metrics and formulas
- Alerting and notifications
- Historical trending (if GitLab provides APIs)

---

## Version History

- **2.0.0** (2025-10-16) - Major architecture consolidation, live API-based metrics
- **1.x** - Previous database-centric architecture (deprecated)

---

**Note**: This CHANGELOG will be maintained going forward for all releases.

