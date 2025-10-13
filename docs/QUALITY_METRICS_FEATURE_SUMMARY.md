# Quality & Reliability Metrics Feature

## Overview

The Quality & Reliability Metrics feature provides comprehensive quality and reliability indicators for developers based on their GitLab activity. This feature helps identify code health patterns and improvement opportunities.

## Implemented Metrics

### 1. Rework Ratio
- **Description**: Measures MRs that required additional commits after review started
- **Formula**: `(count(MRs with commits_after_first_review > 0)) / merged_mrs`
- **Unit**: ratio [0..1]
- **Direction**: ↓ good (lower is better)
- **Implementation**: Fetches commits and notes for each MR to compare commit timestamps with first review timestamp

### 2. Revert Rate
- **Description**: Measures merged MRs that were later reverted
- **Formula**: `(count(MRs with "revert" in title)) / merged_mrs`
- **Unit**: ratio [0..1]
- **Direction**: ↓ good (lower is better)
- **Implementation**: Checks MR title for "revert" or "Revert" patterns
- **Configurable**: Revert detection window (default: 30 days)

### 3. CI Success Rate
- **Description**: First-time pipeline success rate
- **Formula**: `(successful_pipelines_first_run) / total_pipelines`
- **Unit**: percentage
- **Direction**: ↑ good (higher is better)
- **Implementation**: Groups pipelines by SHA to identify first runs, calculates success rate

### 4. Pipeline Duration (P50, P95)
- **Description**: Build/test time percentiles
- **Unit**: minutes
- **Direction**: ↓ good (lower is better)
- **Implementation**: Calculates from pipeline created_at to updated_at timestamps

### 5. Test Coverage
- **Description**: Coverage percentage from pipeline reports
- **Unit**: percentage
- **Direction**: ↑ good (higher is better)
- **Status**: Placeholder implementation (returns null)
- **Notes**: Coverage field added to DTO but requires pipeline details API integration

### 6. Hotfix Rate
- **Description**: MRs labeled or identified as hotfixes
- **Formula**: `(count(MRs with 'hotfix' label/title/branch)) / merged_mrs`
- **Unit**: ratio [0..1]
- **Direction**: ↓ good (lower is better)
- **Heuristics**:
  - Labels contain "hotfix" (case-insensitive)
  - Title contains "hotfix" (case-insensitive)
  - Branch name contains "hotfix" or "hot-fix" (case-insensitive)

### 7. Merge Conflicts Frequency
- **Description**: Frequency of conflict resolution needed
- **Unit**: ratio [0..1]
- **Direction**: ↓ good (lower is better)
- **Implementation**: Uses GitLab API `has_conflicts` field

## API Endpoint

### GET `/api/v1/{userId}/metrics/quality`

Calculates quality and reliability metrics for a developer across all contributed projects.

#### Parameters

- `userId` (required, path): GitLab user ID
- `windowDays` (optional, query): Number of days to look back (default: 30, max: 365)
- `revertDetectionDays` (optional, query): Number of days to check for reverts (default: 30, max: 90)

#### Example Request

```http
GET /api/v1/123/metrics/quality?windowDays=30&revertDetectionDays=30
```

#### Example Response

```json
{
  "userId": 123,
  "username": "developer",
  "windowDays": 30,
  "windowStart": "2024-09-12T10:20:41.021Z",
  "windowEnd": "2024-10-12T10:20:41.021Z",
  "mergedMrCount": 10,
  "reworkRatio": 0.3,
  "reworkMrCount": 3,
  "revertRate": 0.1,
  "revertedMrCount": 1,
  "revertDetectionDays": 30,
  "ciSuccessRate": 0.75,
  "successfulPipelinesFirstRun": 15,
  "totalFirstRunPipelines": 20,
  "pipelineDurationP50Min": 8.5,
  "pipelineDurationP95Min": 15.2,
  "pipelinesWithDurationCount": 20,
  "testCoveragePercent": null,
  "pipelinesWithCoverageCount": 0,
  "hotfixRate": 0.2,
  "hotfixMrCount": 2,
  "conflictRate": 0.1,
  "conflictMrCount": 1,
  "projects": [
    {
      "projectId": 456,
      "projectName": "web-app",
      "mergedMrCount": 7,
      "pipelineCount": 15
    },
    {
      "projectId": 789,
      "projectName": "api-service",
      "mergedMrCount": 3,
      "pipelineCount": 5
    }
  ]
}
```

#### Error Responses

**404 Not Found** - User not found
```json
{
  "error": "User with ID 999 not found"
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
  "title": "Error calculating quality metrics",
  "detail": "Failed to fetch data from GitLab API",
  "status": 500
}
```

## Technical Architecture

### Files Created

1. **Service Interface**: `IQualityMetricsService.cs`
   - Defines the contract for quality metrics calculation
   - Includes result model definitions

2. **Service Implementation**: `QualityMetricsService.cs`
   - Implements all 7 metrics calculations
   - Handles data fetching and aggregation
   - Includes error handling and graceful degradation

3. **Endpoint**: Updated `UserMetricsEndpoints.cs`
   - Added `/metrics/quality` endpoint mapping
   - Input validation and error handling

4. **Unit Tests**: `QualityMetricsServiceTests.cs`
   - 7 comprehensive test cases
   - Edge case coverage (null values, empty data, invalid inputs)

### Service Registration

Added to `ServiceCollectionExtensions.cs`:
```csharp
builder.Services.AddScoped<IQualityMetricsService, QualityMetricsService>();
```

## DTO Enhancements

Extended GitLab API DTOs to support additional fields:

### GitLabMergeRequest DTO
- Added `Title`: string? - MR title for revert detection
- Added `Labels`: List<string>? - Labels for hotfix detection
- Added `HasConflicts`: bool - Conflict status

### GitLabPipeline DTO
- Added `Coverage`: string? - Test coverage percentage (for future use)

### GitLabMergeRequest Model
- Added `Labels`: List<string>? - Labels collection

## Data Flow

1. **User Validation**: Verify user exists in GitLab
2. **Project Discovery**: Get all projects user has contributed to
3. **Data Collection**: Fetch MRs and pipelines in parallel for each project
4. **Data Filtering**: Filter by author, date range, and merge status
5. **Metrics Calculation**: Calculate each metric independently
6. **Result Aggregation**: Combine metrics and project summaries
7. **Response**: Return comprehensive quality metrics result

## Performance Considerations

- Parallel data fetching for multiple projects
- Efficient filtering to reduce data processing
- Graceful handling of API failures per project
- Rework calculation requires additional API calls (commits + notes per MR)

## Edge Cases Handled

1. **User not found**: Throws InvalidOperationException
2. **No contributed projects**: Returns empty result with zero values
3. **No pipelines**: Returns null for CI and duration metrics
4. **No coverage data**: Returns null for coverage metric
5. **Invalid window days**: Throws ArgumentOutOfRangeException
6. **API failures**: Logs warnings and continues with other projects

## Testing

### Test Coverage
- 7 unit tests covering all major scenarios
- All tests passing (33 total tests in suite)
- Mock-based testing using Moq

### Test Scenarios
1. Invalid user ID handling
2. No projects scenario
3. Full metrics calculation with MRs and pipelines
4. No pipelines scenario
5. Invalid parameter validation (zero/negative days)
6. Hotfix detection patterns

## Future Enhancements

1. **Test Coverage Implementation**: Requires individual pipeline details API calls to fetch coverage data
2. **Rework Optimization**: Consider caching commit/note data to reduce API calls
3. **Revert Detection Enhancement**: Consider using MR relationships API when available
4. **Trending Analysis**: Store historical data for trend visualization
5. **Benchmarking**: Add team/project averages for comparison

## Related Documentation

- Issue: "Implement Quality & Reliability Metrics"
- PRD: `prds/gitlab-developer-productivity-metrics.md` section 6.2
- API Documentation: `/docs/API_V1_ENDPOINTS.md`
