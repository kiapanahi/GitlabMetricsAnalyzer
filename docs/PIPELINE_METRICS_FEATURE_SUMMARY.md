# Pipeline & CI/CD Metrics Feature

## Overview

The Pipeline & CI/CD Metrics feature provides comprehensive pipeline and build metrics to identify bottlenecks and reliability issues in build/test infrastructure. This feature helps teams understand their CI/CD performance and make data-driven improvements.

## Implemented Metrics

### 1. Failed Job Rate
- **Description**: Identifies the most frequently failing pipeline jobs
- **GitLab API**: `GET /projects/{id}/pipelines/{pipeline_id}/jobs` (used internally)
- **Unit**: failure count per job
- **Direction**: ↓ good (lower is better)
- **Implementation**: Groups jobs by name, calculates failure rate for each job, returns top 10 most failing jobs
- **Output**: List of failed job summaries with failure counts and rates

### 2. Pipeline Retry Rate
- **Description**: Pipelines requiring manual retry
- **Formula**: `(pipelines_with_multiple_runs_same_sha) / total_unique_shas`
- **Unit**: percentage
- **Direction**: ↓ good (lower is better)
- **Implementation**: Groups pipelines by SHA, identifies retries (multiple pipelines with same SHA)
- **Use Case**: Helps identify flaky tests and unreliable pipeline infrastructure

### 3. Pipeline Wait Time
- **Description**: Queue time before pipeline starts
- **Formula**: `median(started_at - created_at)` and P95
- **GitLab API**: `GET /projects/{id}/pipelines` (uses `started_at` and `created_at` fields)
- **Unit**: minutes (P50 and P95)
- **Direction**: ↓ good (lower is better)
- **Implementation**: Calculates wait time from pipeline creation to start, provides P50 and P95 percentiles
- **Use Case**: Identifies runner capacity issues and resource bottlenecks

### 4. Deployment Frequency
- **Description**: Merges to main/production branches (DORA metric)
- **GitLab API**: `GET /projects/{id}/pipelines` (filters by `ref` field: main, master, production)
- **Unit**: count per period
- **Direction**: ↑ good (higher is better)
- **Implementation**: Counts pipelines on main/master/production branches
- **DORA Mapping**: This is one of the four DORA (DevOps Research and Assessment) metrics
- **Interpretation**:
  - Elite: Multiple deployments per day
  - High: Between once per day and once per week
  - Medium: Between once per week and once per month
  - Low: Fewer than once per month

### 5. Job Duration Trends
- **Description**: Track duration changes over time to detect degradation
- **GitLab API**: `GET /projects/{id}/pipelines/{pipeline_id}/jobs` (uses `duration` field from job response)
- **Unit**: minutes, with trend indicator (improving, stable, degrading)
- **Direction**: ↓ good (lower is better)
- **Implementation**: 
  - Groups jobs by name, calculates average, P50, and P95 duration
  - Compares first half vs second half of time window to determine trend
  - Flags jobs with >10% change as "improving" or "degrading"
  - Returns top 10 longest running jobs
- **Use Case**: Detects test suite degradation early

### 6. Pipeline Success Rate by Branch Type
- **Description**: Success rate on feature vs. main branches
- **GitLab API**: `GET /projects/{id}/pipelines` (uses `ref` and `status` fields from pipeline response)
- **Unit**: percentage by category (main branches vs feature branches)
- **Direction**: ↑ good (higher is better)
- **Implementation**: 
  - Separates pipelines into main branches (main/master/production) and feature branches
  - Calculates success rate for each category
- **Use Case**: Helps understand quality differences between protected and feature branches

### 7. Coverage Trend
- **Description**: Test coverage change over time
- **GitLab API**: `GET /projects/{id}/pipelines` (uses `coverage` field from pipeline response)
- **Unit**: percentage with trend indicator
- **Direction**: ↑ good (higher is better)
- **Implementation**: 
  - Parses coverage from pipeline metadata
  - Calculates average coverage
  - Compares first half vs second half to determine trend (improving, stable, degrading)
  - Requires at least 4 pipelines with coverage data to calculate trend
- **Use Case**: Monitors test coverage evolution

## API Endpoint

### GET `/api/v1/metrics/pipelines/{projectId}`

Calculates comprehensive pipeline metrics for a project.

#### Parameters

- `projectId` (required, path): GitLab project ID
- `windowDays` (optional, query): Number of days to look back (default: 30, max: 365)

#### Example Request

```http
GET /api/v1/metrics/pipelines/123?windowDays=30
```

#### Example Response

```json
{
  "projectId": 123,
  "projectName": "web-app",
  "windowDays": 30,
  "windowStart": "2024-09-12T10:20:41.021Z",
  "windowEnd": "2024-10-12T10:20:41.021Z",
  "failedJobs": [
    {
      "jobName": "integration-tests",
      "failureCount": 15,
      "totalRuns": 50,
      "failureRate": 0.3
    },
    {
      "jobName": "e2e-tests",
      "failureCount": 8,
      "totalRuns": 45,
      "failureRate": 0.178
    }
  ],
  "pipelineRetryRate": 0.12,
  "retriedPipelineCount": 6,
  "totalPipelineCount": 50,
  "pipelineWaitTimeP50Min": 2.5,
  "pipelineWaitTimeP95Min": 8.2,
  "pipelinesWithWaitTimeCount": 48,
  "deploymentFrequency": 10,
  "jobDurationTrends": [
    {
      "jobName": "integration-tests",
      "averageDurationMin": 12.5,
      "durationP50Min": 11.2,
      "durationP95Min": 18.5,
      "trend": "degrading",
      "runCount": 50
    },
    {
      "jobName": "build",
      "averageDurationMin": 8.3,
      "durationP50Min": 8.0,
      "durationP95Min": 10.5,
      "trend": "stable",
      "runCount": 50
    }
  ],
  "branchTypeMetrics": {
    "mainBranchSuccessRate": 0.95,
    "mainBranchSuccessCount": 19,
    "mainBranchTotalCount": 20,
    "featureBranchSuccessRate": 0.87,
    "featureBranchSuccessCount": 26,
    "featureBranchTotalCount": 30
  },
  "averageCoveragePercent": 82.5,
  "coverageTrend": "improving",
  "pipelinesWithCoverageCount": 48
}
```

#### Error Responses

**404 Not Found** - Project not found
```json
{
  "error": "Project with ID 999 not found"
}
```

**400 Bad Request** - Invalid parameters
```json
{
  "error": "windowDays must be greater than 0"
}
```

**500 Internal Server Error** - Processing error
```json
{
  "title": "Error calculating pipeline metrics",
  "detail": "Failed to fetch data from GitLab API",
  "status": 500
}
```

## Technical Architecture

### Files Created

1. **Service Interface**: `IPipelineMetricsService.cs`
   - Defines the contract for pipeline metrics calculation
   - Includes result model definitions for all metrics

2. **Service Implementation**: `PipelineMetricsService.cs`
   - Implements all 7 metrics calculations
   - Handles data fetching and aggregation
   - Includes error handling and graceful degradation

3. **Endpoint**: `PipelineMetricsEndpoints.cs`
   - Added `/metrics/pipelines/{projectId}` endpoint mapping
   - Input validation and error handling

4. **API Client Methods**: Updated `GitLabHttpClient.cs`
   - Added `GetPipelineJobsAsync` method to fetch pipeline jobs

5. **DTOs**: `GitLabPipelineJob.cs`
   - Data transfer object for GitLab API responses

6. **Domain Models**: `GitLabPipelineJob.cs` (Models/Raw)
   - Domain model for pipeline jobs

7. **Unit Tests**: `PipelineMetricsServiceTests.cs`
   - 6 comprehensive test cases
   - Edge case coverage (null values, empty data, invalid inputs, failures)

### Service Registration

Added to `ServiceCollectionExtensions.cs`:
```csharp
builder.Services.AddScoped<IPipelineMetricsService, PipelineMetricsService>();
```

Added to `GitLabMetricsEndpoints.cs`:
```csharp
app.MapPipelineMetricsEndpoints();
```

## DORA Metrics Mapping

This feature implements 1 of the 4 DORA (DevOps Research and Assessment) metrics:

### Deployment Frequency
- **DORA Metric**: Deployment Frequency
- **Our Implementation**: `deploymentFrequency` field in the response
- **Interpretation**: Count of pipelines on main/production branches
- **Benchmarks**:
  - Elite performers: Multiple deployments per day
  - High performers: Between once per day and once per week
  - Medium performers: Between once per week and once per month
  - Low performers: Fewer than once per month

### Related DORA Metrics (Not in this feature)
- **Lead Time for Changes**: Time from commit to production (could be calculated from MR metrics + pipeline metrics)
- **Change Failure Rate**: Percentage of deployments causing failures (could use revert rate from Quality Metrics)
- **Time to Restore Service**: Time to recover from incidents (requires incident tracking)

## Data Flow

1. **Input Validation**: Validates project ID and time window parameters
2. **Project Lookup**: Fetches project details from GitLab
3. **Pipeline Retrieval**: Fetches all pipelines in time window
4. **Job Fetching**: Fetches jobs for each pipeline (parallel execution)
5. **Metrics Calculation**: 
   - Failed Job Rate: Groups jobs by name, calculates failure rates
   - Pipeline Retry Rate: Groups pipelines by SHA, identifies retries
   - Wait Time: Calculates P50 and P95 from creation to start time
   - Deployment Frequency: Counts main branch pipelines
   - Job Duration Trends: Calculates duration statistics with trend analysis
   - Branch Type Metrics: Separates and analyzes by branch type
   - Coverage Trend: Parses and tracks coverage changes
6. **Response Assembly**: Constructs comprehensive result object

## Performance Considerations

### Parallel Job Fetching
- Jobs for all pipelines are fetched in parallel using `Task.WhenAll`
- Each pipeline's jobs are fetched independently
- Failures in fetching jobs for one pipeline don't affect others

### Error Handling
- Gracefully handles missing pipeline jobs
- Returns partial results when some data is unavailable
- Logs warnings for fetch failures

### Query Optimization
- Filters pipelines by date at API level using `updated_after` parameter
- Only fetches pipelines within the specified time window
- Pagination handled by `IGitLabHttpClient`

### Caching Opportunities (Future Enhancement)
- Pipeline data could be cached for frequently accessed projects
- Job data could be cached with short TTL
- Metric results could be cached for recent time windows

## Edge Cases Handled

1. **Missing Project**: Returns 404 with appropriate error message
2. **No Pipelines**: Returns empty result with all metrics as null/empty
3. **Failed Job Fetches**: Continues processing with available data
4. **Missing Coverage Data**: Returns null for coverage metrics
5. **Missing Wait Time Data**: Returns null for wait time metrics
6. **Insufficient Data for Trends**: Returns null trend when < 4 data points
7. **Jobs with No Duration**: Excluded from duration calculations
8. **Invalid Coverage Values**: Gracefully skipped during parsing

## Testing

### Unit Tests Coverage

1. **Invalid Project ID**: Verifies exception thrown for non-existent projects
2. **No Pipelines**: Verifies empty result handling
3. **Complete Metrics**: Verifies all metrics calculated correctly with valid data
4. **Invalid Window Days**: Verifies parameter validation
5. **Job Fetch Failures**: Verifies graceful degradation when job fetching fails
6. **No Coverage Data**: Verifies null coverage handling

All tests use mocked `IGitLabHttpClient` to avoid external dependencies.

## Usage Examples

### Dashboard Query
```bash
# Get pipeline metrics for last 30 days
curl -X GET "http://localhost:5000/api/v1/metrics/pipelines/123?windowDays=30"
```

### Grafana Query
```bash
# Get pipeline metrics for custom time range
curl -X GET "http://localhost:5000/api/v1/metrics/pipelines/123?windowDays=90"
```

### Identifying Issues

**High Pipeline Retry Rate**
- Indicates flaky tests or unreliable infrastructure
- Action: Investigate failed jobs list and job duration trends

**Increasing Wait Times**
- Indicates runner capacity issues
- Action: Add more runners or optimize job scheduling

**Degrading Job Duration Trends**
- Indicates test suite growth or performance regression
- Action: Review and optimize slow tests

**Low Deployment Frequency**
- May indicate release process bottlenecks
- Action: Review deployment pipeline and approval processes

## Future Enhancements

1. **Pipeline Stage Metrics**: Break down metrics by pipeline stage
2. **Runner Utilization**: Track runner usage and capacity
3. **Cost Analysis**: Calculate pipeline execution costs
4. **Historical Trending**: Store metrics over time for long-term analysis
5. **Alerting**: Configure thresholds and alerts for degrading metrics
6. **Job Artifacts Analysis**: Track artifact sizes and download patterns
7. **Pipeline Comparison**: Compare metrics across projects or time periods
8. **Bottleneck Detection**: Automatically identify pipeline bottlenecks
9. **Lead Time Calculation**: Full DORA metrics by combining with MR data
10. **SLA Tracking**: Monitor pipeline SLA compliance

## Related Documentation

- [Quality Metrics Feature](./QUALITY_METRICS_FEATURE_SUMMARY.md) - Related code quality metrics
- [GitLab Pipelines API](https://docs.gitlab.com/api/pipelines.html) - Official GitLab API documentation
- [GitLab Jobs API](https://docs.gitlab.com/api/jobs.html) - Official GitLab Jobs API documentation
- [DORA Metrics](https://cloud.google.com/blog/products/devops-sre/using-the-four-keys-to-measure-your-devops-performance) - DevOps Research and Assessment metrics
