# Current State Documentation

**Date**: October 18, 2025  
**Branch**: `main`  
**Purpose**: Accurate description of the application's actual architecture and functionality

---

## Overview

**GitLabMetricsAnalyzer** is a **live metrics calculation API** that provides developer productivity insights by analyzing data from an on-premise GitLab instance via real-time API calls.

**What it IS**:
- ✅ REST API for calculating GitLab metrics on-demand
- ✅ Live API integration with GitLab API v4
- ✅ Real-time metrics calculation and aggregation
- ✅ Developer, team, project, and pipeline analytics

**What it is NOT**:
- ❌ NOT a data collection/ETL system
- ❌ NOT a data warehouse or historical data store
- ❌ NOT a background job processor
- ❌ NOT a metrics database

---

## Architecture

### Current Architecture (As-Built)

```
┌─────────────────────────────────────────────────────┐
│                Client Application                    │
│            (Web UI, CLI, Scripts, etc.)              │
└─────────────────┬───────────────────────────────────┘
                  │ HTTP Requests
                  ▼
┌─────────────────────────────────────────────────────┐
│               REST API Layer (ASP.NET Core)          │
│                                                       │
│  Endpoints:                                          │
│  • GET /api/v1/{userId}/analysis/commit-time        │
│  • GET /api/v1/{userId}/metrics/mr-cycle-time       │
│  • GET /api/v1/{userId}/metrics/flow                │
│  • GET /api/v1/{userId}/metrics/collaboration       │
│  • GET /api/v1/{userId}/metrics/quality             │
│  • GET /api/v1/{userId}/metrics/code-characteristics│
│  • GET /api/v1/metrics/pipelines/{projectId}        │
│  • GET /api/v1/{userId}/metrics/advanced            │
│  • GET /api/v1/teams/{teamId}/metrics               │
│  • GET /api/v1/projects/{projectId}/metrics         │
│                                                       │
│  Features:                                           │
│  • OpenAPI/Swagger documentation                     │
│  • Consistent error handling                         │
│  • Time-window filtering (1-365 days)                │
└─────────────────┬───────────────────────────────────┘
                  │ Service Layer Calls
                  ▼
┌─────────────────────────────────────────────────────┐
│            Metrics Services Layer (Business Logic)   │
│                                                       │
│  • CommitTimeAnalysisService                         │
│  • PerDeveloperMetricsService                        │
│  • CollaborationMetricsService                       │
│  • QualityMetricsService                             │
│  • CodeCharacteristicsService                        │
│  • PipelineMetricsService                            │
│  • AdvancedMetricsService                            │
│  • TeamMetricsService (aggregation)                  │
│  • ProjectMetricsService (aggregation)               │
│                                                       │
│  Capabilities:                                       │
│  • Bot account filtering (regex patterns)            │
│  • Commit/branch/file exclusions                     │
│  • MR size categorization                            │
│  • Metric aggregation and calculation                │
└─────────────────┬───────────────────────────────────┘
                  │ GitLab API Calls
                  ▼
┌─────────────────────────────────────────────────────┐
│         GitLab HTTP Client (Infrastructure)          │
│                                                       │
│  • HttpClient with Polly resilience policies         │
│  • Automatic retries (3 attempts, exponential)       │
│  • Circuit breaker (30% failure → 30s break)         │
│  • Request timeout (5 minutes total)                 │
│  • Bearer token authentication                       │
│  • GitLab API v4 integration                         │
└─────────────────┬───────────────────────────────────┘
                  │ HTTPS
                  ▼
            ┌──────────────┐
            │   GitLab     │
            │   Server     │
            │ (On-Premise) │
            └──────────────┘
```

### Data Flow

```
1. Client sends HTTP GET request with userId/projectId + time window
   ↓
2. API endpoint validates parameters (userId, windowDays)
   ↓
3. Metrics service is invoked via dependency injection
   ↓
4. Service makes LIVE API calls to GitLab:
   • GET /projects/:id/repository/commits
   • GET /projects/:id/merge_requests
   • GET /projects/:id/issues
   • GET /projects/:id/pipelines
   • GET /projects/:id/repository/contributors
   ↓
5. Service applies filtering (bots, merge commits, patterns)
   ↓
6. Service calculates metrics in-memory
   ↓
7. Service returns DTO with calculated metrics
   ↓
8. API endpoint returns JSON response to client
```

**Key Characteristics**:
- ⚡ **Synchronous**: Request → Calculate → Respond (no background jobs)
- 🔄 **Stateless**: No data persistence between requests
- 📊 **On-Demand**: Metrics calculated when requested
- 🎯 **Direct**: No caching layer (every request hits GitLab API)

---

## Technology Stack

### Core Framework
- **.NET 9** (latest LTS)
- **ASP.NET Core** (Minimal APIs)
- **C# 13** (file-scoped namespaces, latest features)

### Orchestration
- **.NET Aspire** (local development orchestration)
- **Service Discovery** (Aspire-managed)
- **Health Checks** (GitLab connectivity check)

### API & Documentation
- **OpenAPI 3.0** (`/openapi/internal.json`)
- **Swagger UI** (Development mode only)
- **Minimal APIs** (no controllers)

### HTTP & Resilience
- **HttpClient** with typed client pattern
- **Polly** for resilience:
  - Retry policy (3 attempts, exponential backoff)
  - Circuit breaker (30% failure threshold)
  - Total request timeout (5 minutes)

### Logging & Telemetry
- **Serilog** (structured logging)
- **OpenTelemetry** (distributed tracing)
- **Activity Source**: `Toman.Management.KPIAnalysis.GitLabMetrics`
- **Metrics Meter**: `Toman.Management.KPIAnalysis.GitLabMetrics`

### Configuration
- **IOptions Pattern** for strongly-typed configuration
- **User Secrets** (Development) for sensitive data
- **Environment Variables** for production secrets

### External Integration
- **GitLab API v4** (REST)
- **NGitLab NuGet Package** (GitLab API client library)

### Database
- **No Database** - All previous database infrastructure has been removed
- Application is fully stateless with live API calls only

---

## API Surface

### Endpoint Summary

| Group            | Count  | Base Path                           | Purpose                      |
| ---------------- | ------ | ----------------------------------- | ---------------------------- |
| User Metrics     | 6      | `/api/v1/{userId:long}`             | Per-developer analytics      |
| Pipeline Metrics | 1      | `/api/v1/metrics/pipelines`         | CI/CD analytics              |
| Advanced Metrics | 1      | `/api/v1/{userId:long}`             | Advanced developer analytics |
| Team Metrics     | 1      | `/api/v1/teams/{teamId}`            | Team-level aggregations      |
| Project Metrics  | 1      | `/api/v1/projects/{projectId:long}` | Project-level aggregations   |
| **Total**        | **10** | `/api/v1/`                          | All metrics endpoints        |

### User Metrics Endpoints (6)

#### 1. Commit Time Analysis
```
GET /api/v1/{userId}/analysis/commit-time?lookbackDays=30
```
- **Service**: `CommitTimeAnalysisService`
- **Returns**: Distribution of commits across 24 hours
- **Use Case**: Identify peak coding hours, work-life balance

#### 2. MR Cycle Time
```
GET /api/v1/{userId}/metrics/mr-cycle-time?windowDays=30
```
- **Service**: `PerDeveloperMetricsService`
- **Returns**: P50 median cycle time from first commit to merge
- **Use Case**: Measure delivery speed

#### 3. Flow Metrics
```
GET /api/v1/{userId}/metrics/flow?windowDays=30
```
- **Service**: `PerDeveloperMetricsService`
- **Returns**: 
  - Merged MRs count
  - Lines changed (additions/deletions)
  - Coding time, review time, merge time
  - WIP MRs count
  - Context switching index
- **Use Case**: Throughput analysis, bottleneck identification

#### 4. Collaboration Metrics
```
GET /api/v1/{userId}/metrics/collaboration?windowDays=30
```
- **Service**: `CollaborationMetricsService`
- **Returns**:
  - Review comments given/received
  - Approvals given/received
  - Discussion threads participated
  - Self-merged MRs ratio
  - Review turnaround time
  - Review depth
- **Use Case**: Team collaboration assessment

#### 5. Quality Metrics
```
GET /api/v1/{userId}/metrics/quality?windowDays=30
```
- **Service**: `QualityMetricsService`
- **Returns**:
  - Rework ratio
  - Revert rate
  - CI success rate
  - Pipeline duration
  - Test coverage
  - Hotfix rate
  - Merge conflict frequency
- **Use Case**: Code quality and reliability assessment

#### 6. Code Characteristics
```
GET /api/v1/{userId}/metrics/code-characteristics?windowDays=30
```
- **Service**: `CodeCharacteristicsService`
- **Returns**:
  - Commit frequency
  - Commit size distribution
  - MR size distribution (small/medium/large)
  - File churn (top changed files)
  - Squash merge rate
  - Commit message quality score
  - Branch naming compliance
- **Use Case**: Code quality patterns, best practices adherence

### Pipeline Metrics Endpoint (1)

```
GET /api/v1/metrics/pipelines/{projectId}?windowDays=30
```
- **Service**: `PipelineMetricsService`
- **Returns**:
  1. Failed Job Rate
  2. Pipeline Retry Rate
  3. Pipeline Wait Time
  4. Deployment Frequency
  5. Job Duration Trends
  6. Pipeline Success Rate by Branch Type
  7. Coverage Trend
- **Use Case**: CI/CD pipeline health and efficiency

### Advanced Metrics Endpoint (1)

```
GET /api/v1/{userId}/metrics/advanced?windowDays=30
```
- **Service**: `AdvancedMetricsService`
- **Returns**:
  1. Bus Factor
  2. Response Time Distribution
  3. Batch Size
  4. Draft Duration
  5. Iteration Count
  6. Idle Time in Review
  7. Cross-Team Collaboration Index
- **Use Case**: Team risk assessment, process bottlenecks

### Team Metrics Endpoint (1)

```
GET /api/v1/teams/{teamId}/metrics?windowDays=30
```
- **Service**: `TeamMetricsService`
- **Returns**:
  - Team velocity
  - Cross-project contributions
  - Review coverage
- **Use Case**: Team-level performance tracking

### Project Metrics Endpoint (1)

```
GET /api/v1/projects/{projectId}/metrics?windowDays=30
```
- **Service**: `ProjectMetricsService`
- **Returns**:
  - Activity score
  - Branch lifecycle
  - Label usage
  - Milestone completion
  - Review coverage
- **Use Case**: Project health monitoring

---

## Configuration

### Active Configuration

#### GitLabConfiguration ✅
**File**: `Features/GitLabMetrics/Configuration/GitLabConfiguration.cs`

```json
{
  "GitLab": {
    "BaseUrl": "https://gitlab.qcluster.org/",
    "Token": "<configured-via-env-variable>"
  }
}
```

**Properties**:
- `BaseUrl` (required): GitLab instance URL
- `Token` (required): Personal Access Token (via environment variable `GitLab__Token` or user secret)

**Used By**: `GitLabHttpClient`

#### MetricsConfiguration ✅
**File**: `Configuration/MetricsConfiguration.cs`

```json
{
  "Metrics": {
    "Identity": {
      "BotRegexPatterns": [
        "^.*bot$",
        "^.*\\[bot\\]$",
        "^gitlab-ci$",
        "^dependabot.*",
        "^renovate.*",
        "^.*automation.*$"
      ]
    },
    "Excludes": {
      "CommitPatterns": [
        "^Merge branch.*",
        "^Merge pull request.*",
        "^Merge.*",
        "^Revert.*"
      ],
      "BranchPatterns": [
        "^dependabot/.*",
        "^renovate/.*"
      ],
      "FilePatterns": [
        "^.*\\.min\\.(js|css)$",
        "^.*\\.(png|jpg|jpeg|gif|svg|ico)$",
        "^.*\\.lock$",
        "^package-lock\\.json$",
        "^yarn\\.lock$"
      ]
    }
  }
}
```

**Sub-Configurations**:
- `IdentityConfiguration`: Bot account detection
- `ExclusionConfiguration`: Commit/branch/file filtering
- `CodeCharacteristicsConfiguration`: MR size thresholds, quality patterns
- `TeamMappingConfiguration`: Team member mappings

**Used By**: `AdvancedMetricsService`, `CollaborationMetricsService`, `CodeCharacteristicsService`, `TeamMetricsService`

---

## Project Structure

```
GitlabMetricsAnalyzer/
├── src/
│   ├── Toman.Management.KPIAnalysis.ApiService/    # Main API project
│   │   ├── Program.cs                               # Entry point, DI setup
│   │   ├── appsettings.json                         # Production config
│   │   ├── appsettings.Development.json             # Dev config
│   │   ├── Configuration/                           # Configuration classes
│   │   │   ├── MetricsConfiguration.cs              # ✅ USED
│   │   │   └── ExportsConfiguration.cs              # ❌ UNUSED
│   │   ├── Data/                                    # Database (unused)
│   │   │   └── Extensions/
│   │   │       └── DbContextExtensions.cs           # ❌ UNUSED
│   │   ├── Features/
│   │   │   └── GitLabMetrics/
│   │   │       ├── GitLabMetricsEndpoints.cs        # Endpoint registration
│   │   │       ├── UserMetricsEndpoints.cs          # 6 user endpoints
│   │   │       ├── PipelineMetricsEndpoints.cs      # 1 pipeline endpoint
│   │   │       ├── AdvancedMetricsEndpoints.cs      # 1 advanced endpoint
│   │   │       ├── TeamMetricsEndpoints.cs          # 1 team endpoint
│   │   │       ├── ProjectMetricsEndpoints.cs       # 1 project endpoint
│   │   │       ├── Configuration/
│   │   │       │   ├── GitLabConfiguration.cs       # ✅ USED
│   │   │       │   └── CollectionConfiguration.cs   # ❌ UNUSED
│   │   │       ├── Data/
│   │   │       │   └── GitLabMetricsDbContext.cs    # ❌ UNUSED (20 DbSets)
│   │   │       ├── HealthChecks/
│   │   │       │   └── GitLabHealthCheck.cs         # ✅ USED
│   │   │       ├── Infrastructure/
│   │   │       │   ├── GitLabHttpClient.cs          # ✅ USED (API client)
│   │   │       │   └── Diagnostics.cs               # ✅ USED (telemetry)
│   │   │       ├── Models/                          # Entity models
│   │   │       │   ├── Raw/                         # ❌ UNUSED (5 files)
│   │   │       │   ├── Fact/                        # ❌ UNUSED (3 files)
│   │   │       │   ├── PRD/                         # ❌ UNUSED (8 files)
│   │   │       │   ├── Operational/                 # ❌ UNUSED (2 files)
│   │   │       │   └── Dim/                         # ❌ UNUSED (2 files)
│   │   │       ├── Services/                        # Metrics services
│   │   │       │   ├── CommitTimeAnalysisService.cs           # ✅ USED
│   │   │       │   ├── PerDeveloperMetricsService.cs          # ✅ USED
│   │   │       │   ├── CollaborationMetricsService.cs         # ✅ USED
│   │   │       │   ├── QualityMetricsService.cs               # ✅ USED
│   │   │       │   ├── CodeCharacteristicsService.cs          # ✅ USED
│   │   │       │   ├── PipelineMetricsService.cs              # ✅ USED
│   │   │       │   ├── AdvancedMetricsService.cs              # ✅ USED
│   │   │       │   ├── TeamMetricsService.cs                  # ✅ USED
│   │   │       │   └── ProjectMetricsService.cs               # ✅ USED
│   │   │       └── ServiceCollectionExtensions.cs   # DI registration
│   │   └── Migrations/                              # ❌ UNUSED (30+ files)
│   ├── Toman.Management.KPIAnalysis.AppHost/        # Aspire AppHost
│   │   └── Program.cs                               # Aspire orchestration
│   └── Toman.Management.KPIAnalysis.ServiceDefaults/ # Aspire defaults
│       ├── Extensions.cs                            # Telemetry, health checks
│       └── Constants.cs                             # Resource names
├── test/
│   └── Toman.Management.KPIAnalysis.Tests/          # Unit tests
├── docs/                                            # Documentation
│   ├── CURRENT_STATE.md                             # ✅ Current architecture (this file)
│   ├── ENDPOINT_AUDIT.md                            # ✅ API endpoint audit
│   ├── API_USAGE_GUIDE.md                           # ⚠️ NEEDS UPDATE (PR #3)
│   ├── METRICS_REFERENCE.md                         # ✅ Unified metrics reference
│   ├── DEPLOYMENT_GUIDE.md                          # ⚠️ NEEDS REWRITE (PR #2)
│   ├── CONFIGURATION_GUIDE.md                       # ⚠️ NEEDS CLEANUP (PR #2)
│   ├── OPERATIONS_RUNBOOK.md                        # ⚠️ NEEDS REWRITE (PR #2)
│   ├── EVENTS_API_IMPLEMENTATION.md                 # ✅ Events API approach
│   ├── IDENTITY_MAPPING.md                          # ✅ Identity mapping
│   └── TESTING_COMMIT_TIME_ANALYSIS.md              # ⚠️ NEEDS VERIFICATION (PR #3)
```

---

## Deployment

### Development
```bash
# Start application with Aspire
aspire run

# Application URLs (Aspire-managed):
- API Service: https://localhost:<dynamic-port>
- Aspire Dashboard: https://localhost:17237
- OpenAPI Spec: https://localhost:<api-port>/openapi/internal.json
- Swagger UI: https://localhost:<api-port>/swagger
```

### Production
- **Platform**: Containerized deployment (Docker recommended)
- **Requirements**:
  - GitLab instance accessible via network
  - GitLab Personal Access Token with appropriate permissions
  - Environment variables configured (`GitLab__BaseUrl`, `GitLab__Token`)
- **Database**: NOT REQUIRED (fully stateless application)

---

## Dependencies

### Runtime Dependencies
- .NET 9 Runtime
- Network access to GitLab instance
- GitLab Personal Access Token

### No External Dependencies
- Application is fully self-contained with no database or external storage requirements

---

## What's Working

✅ **All 10 REST API endpoints** are fully functional:
- User metrics (6 endpoints)
- Pipeline metrics (1 endpoint)
- Advanced metrics (1 endpoint)
- Team metrics (1 endpoint)
- Project metrics (1 endpoint)

✅ **All 9 metrics services** are operational:
- CommitTimeAnalysisService
- PerDeveloperMetricsService
- CollaborationMetricsService
- QualityMetricsService
- CodeCharacteristicsService
- PipelineMetricsService
- AdvancedMetricsService
- TeamMetricsService
- ProjectMetricsService

✅ **GitLab API integration** with resilience:
- Automatic retries
- Circuit breaker
- Timeout handling

✅ **Configuration system**:
- GitLab connection settings
- Bot detection patterns
- Exclusion rules
- Code quality thresholds

✅ **Observability**:
- Structured logging (Serilog)
- Distributed tracing (OpenTelemetry)
- Health checks

---

## What's NOT Working / Not Implemented

❌ **Data Collection** (designed but never implemented):
- No batch data collection from GitLab
- No scheduled background jobs
- No data enrichment pipelines

❌ **Data Persistence** (designed but never implemented):
- Database exists but is empty
- No EF Core queries in any service
- All data fetched live from GitLab API

❌ **Exports Feature** (designed but never implemented):
- No CSV/JSON file exports
- No export directory usage

❌ **Developer Management** (designed but never implemented):
- No developer alias mapping UI/API
- No developer identity management

❌ **Historical Analysis** (designed but never implemented):
- No historical data queries
- No trend analysis over time
- All metrics are snapshot-based (time window)

---

## Known Limitations

1. **Performance**: Every request triggers live GitLab API calls (no caching)
2. **Rate Limiting**: No rate limiting on API (could overwhelm GitLab API)
3. **Authentication**: No authentication on endpoints (public API)
4. **Authorization**: No authorization (any user can query any userId/projectId)
5. **GitLab API Limits**: Subject to GitLab instance rate limits
6. **Large Time Windows**: Requesting 365-day windows may timeout or be slow

---

## Future Improvements (Potential)

### Short-Term
1. **Add Response Caching**: Redis/memory cache for frequently requested metrics
2. **Add Authentication**: JWT/OAuth for API security
3. **Add Rate Limiting**: Protect API and GitLab instance
4. **Optimize GitLab Calls**: Batch requests, parallel queries where possible

### Long-Term
1. **Historical Data**: Optionally persist calculated metrics for trend analysis
2. **Webhooks**: React to GitLab events for real-time metrics updates
3. **Export Feature**: CSV/JSON exports for reporting
4. **Dashboard UI**: Web interface for visualizing metrics

---

## Conclusion

The application is a **fully functional live metrics API** that achieves its core purpose: providing on-demand developer productivity insights from GitLab.

The presence of database infrastructure, entity models, and migrations is **architectural debt** from an initial design that evolved away from data persistence toward real-time API-based metrics.

**Current State**: Production-ready API for live metrics  
**Recommended Action**: Remove unused database infrastructure to reflect actual architecture

---

**Document Status**: ✅ Complete and Current  
**Last Updated**: October 18, 2025  
**Phase**: PR #1 - Critical Documentation Cleanup
