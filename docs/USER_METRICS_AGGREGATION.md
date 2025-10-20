# User Metrics Aggregation API

## Overview

The User Metrics Aggregation API provides a single endpoint to retrieve all available user metrics concurrently, eliminating the need to call multiple endpoints separately.

## Endpoint

```http
GET /api/v1/{userId}/metrics/all
```

### Path Parameters

| Parameter | Type   | Description           | Required |
|-----------|--------|----------------------|----------|
| `userId`  | `long` | GitLab user ID       | Yes      |

### Query Parameters

| Parameter             | Type  | Default | Min | Max | Description                                    |
|----------------------|-------|---------|-----|-----|------------------------------------------------|
| `windowDays`         | `int` | 30      | 1   | 365 | Number of days to look back for metrics        |
| `revertDetectionDays`| `int` | 30      | 1   | 90  | Days to check for reverts in quality metrics   |

## Response Structure

```json
{
  "userId": 12345,
  "username": "john.doe",
  "windowDays": 30,
  "windowStart": "2025-09-18T10:30:00Z",
  "windowEnd": "2025-10-18T10:30:00Z",
  "commitTimeAnalysis": { /* CommitTimeDistributionAnalysis */ },
  "mrCycleTime": { /* MrCycleTimeResult */ },
  "flowMetrics": { /* FlowMetricsResult */ },
  "collaborationMetrics": { /* CollaborationMetricsResult */ },
  "qualityMetrics": { /* QualityMetricsResult */ },
  "codeCharacteristics": { /* CodeCharacteristicsResult */ },
  "advancedMetrics": { /* AdvancedMetricsResult */ },
  "errors": {
    "MetricName": "Error description"
  }
}
```

### Response Fields

| Field                  | Type                               | Description                                           |
|------------------------|-------------------------------------|-------------------------------------------------------|
| `userId`               | `long`                             | GitLab user ID                                        |
| `username`             | `string`                           | Username extracted from available metrics             |
| `windowDays`           | `int`                              | Analysis window in days                               |
| `windowStart`          | `DateTime`                         | Start of analysis period (UTC)                        |
| `windowEnd`            | `DateTime`                         | End of analysis period (UTC)                          |
| `commitTimeAnalysis`   | `CommitTimeDistributionAnalysis?`  | Commit time distribution across 24 hours              |
| `mrCycleTime`          | `MrCycleTimeResult?`              | MR cycle time (P50/median)                            |
| `flowMetrics`          | `FlowMetricsResult?`              | Flow and throughput metrics                           |
| `collaborationMetrics` | `CollaborationMetricsResult?`     | Collaboration and review metrics                      |
| `qualityMetrics`       | `QualityMetricsResult?`           | Quality and reliability metrics                       |
| `codeCharacteristics`  | `CodeCharacteristicsResult?`      | Code characteristics and patterns                     |
| `advancedMetrics`      | `AdvancedMetricsResult?`          | Advanced metrics (Bus Factor, Response Time, etc.)    |
| `errors`               | `Dictionary<string, string>?`      | Errors encountered (null if all metrics succeeded)    |

**Note**: Individual metric fields can be `null` if that specific metric calculation failed. Check the `errors` field for details.

## Example Requests

### Basic Request

```bash
GET /api/v1/12345/metrics/all
```

Returns all metrics for user `12345` with default 30-day window.

### Custom Time Window

```bash
GET /api/v1/12345/metrics/all?windowDays=90&revertDetectionDays=60
```

Returns all metrics for user `12345` with:
- 90-day analysis window
- 60-day revert detection window for quality metrics

## Example Response

```json
{
  "userId": 12345,
  "username": "john.doe",
  "windowDays": 30,
  "windowStart": "2025-09-18T10:30:00Z",
  "windowEnd": "2025-10-18T10:30:00Z",
  "commitTimeAnalysis": {
    "userId": 12345,
    "username": "john.doe",
    "email": "john.doe@example.com",
    "lookbackDays": 30,
    "totalCommits": 142,
    "hourlyDistribution": [
      { "hour": 9, "count": 23, "percentage": 16.2 },
      { "hour": 10, "count": 31, "percentage": 21.8 }
    ],
    "peakCommitHours": [9, 10, 14]
  },
  "mrCycleTime": {
    "userId": 12345,
    "username": "john.doe",
    "windowDays": 30,
    "medianCycleTimeHours": 18.5,
    "mergedMrsCount": 24,
    "totalProjects": 3
  },
  "flowMetrics": {
    "userId": 12345,
    "username": "john.doe",
    "mergedMrsCount": 24,
    "linesChanged": 3542,
    "codingTimeMedianH": 4.2,
    "reviewTimeMedianH": 8.1,
    "contextSwitchingIndex": 1.3
  },
  "collaborationMetrics": {
    "userId": 12345,
    "username": "john.doe",
    "reviewCommentsGiven": 87,
    "reviewCommentsReceived": 52,
    "approvalsGiven": 31,
    "selfMergedMrs": 2,
    "reviewTurnaroundMedianH": 3.7
  },
  "qualityMetrics": {
    "userId": 12345,
    "username": "john.doe",
    "reworkRatio": 0.12,
    "revertRate": 0.04,
    "ciSuccessRate": 0.94,
    "hotfixRate": 0.08
  },
  "codeCharacteristics": {
    "userId": 12345,
    "username": "john.doe",
    "commitFrequency": 4.73,
    "commitSizeDistribution": {
      "small": 67,
      "medium": 52,
      "large": 23
    },
    "squashMergeRate": 0.75
  },
  "advancedMetrics": {
    "userId": 12345,
    "username": "john.doe",
    "busFactor": 0.42,
    "batchSizeP50": 124,
    "draftDurationMedianH": 12.3,
    "crossTeamCollaborationIndex": 2.1
  },
  "errors": null
}
```

## Example Response with Partial Failure

If some metrics fail to calculate, the response includes those that succeeded and reports errors:

```json
{
  "userId": 12345,
  "username": "john.doe",
  "windowDays": 30,
  "windowStart": "2025-09-18T10:30:00Z",
  "windowEnd": "2025-10-18T10:30:00Z",
  "commitTimeAnalysis": { /* ... successful ... */ },
  "mrCycleTime": { /* ... successful ... */ },
  "flowMetrics": null,
  "collaborationMetrics": { /* ... successful ... */ },
  "qualityMetrics": null,
  "codeCharacteristics": { /* ... successful ... */ },
  "advancedMetrics": { /* ... successful ... */ },
  "errors": {
    "FlowMetrics": "User not found in any project",
    "QualityMetrics": "No merge requests found in the specified window"
  }
}
```

## Error Responses

### 400 Bad Request

Invalid query parameters:

```json
{
  "error": "windowDays must be greater than 0"
}
```

```json
{
  "error": "windowDays cannot exceed 365 days"
}
```

```json
{
  "error": "revertDetectionDays cannot exceed 90 days"
}
```

### 404 Not Found

User not found:

```json
{
  "error": "User with ID 12345 not found"
}
```

### 500 Internal Server Error

Unexpected error:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.6.1",
  "title": "Error gathering all user metrics",
  "status": 500,
  "detail": "An unexpected error occurred while processing the request"
}
```

## Performance Characteristics

- **Concurrent Execution**: All 7 metric calculations run in parallel
- **Isolated Failures**: If one metric fails, others continue executing
- **Response Time**: Typically 2-5 seconds (depends on GitLab API response times and data volume)
- **Graceful Degradation**: Partial results returned even if some metrics fail

## Use Cases

1. **Developer Dashboards**: Single API call to populate comprehensive developer profile
2. **Performance Reviews**: Gather all metrics for evaluation periods
3. **Team Analytics**: Batch retrieve metrics for multiple team members
4. **Reporting**: Simplified data gathering for periodic reports
5. **Data Export**: Single endpoint for complete metric extraction

## Comparison with Individual Endpoints

### Before (Multiple Calls Required)

```bash
GET /api/v1/{userId}/analysis/commit-time
GET /api/v1/{userId}/metrics/mr-cycle-time
GET /api/v1/{userId}/metrics/flow
GET /api/v1/{userId}/metrics/collaboration
GET /api/v1/{userId}/metrics/quality
GET /api/v1/{userId}/metrics/code-characteristics
GET /api/v1/{userId}/metrics/advanced
```

- 7 separate API calls
- Sequential execution: ~10-15 seconds total
- Complex client-side orchestration

### After (Single Aggregated Call)

```bash
GET /api/v1/{userId}/metrics/all
```

- 1 API call
- Concurrent execution: ~2-5 seconds total
- Simple client implementation

## Related Endpoints

Individual metric endpoints are still available for granular access:

- `GET /api/v1/{userId}/analysis/commit-time` - Commit time distribution
- `GET /api/v1/{userId}/metrics/mr-cycle-time` - MR cycle time
- `GET /api/v1/{userId}/metrics/flow` - Flow metrics
- `GET /api/v1/{userId}/metrics/collaboration` - Collaboration metrics
- `GET /api/v1/{userId}/metrics/quality` - Quality metrics
- `GET /api/v1/{userId}/metrics/code-characteristics` - Code characteristics
- `GET /api/v1/{userId}/metrics/advanced` - Advanced metrics

## Implementation Details

The aggregation service uses the following architecture:

```csharp
IUserMetricsAggregationService
└── UserMetricsAggregationService
    ├── ICommitTimeAnalysisService
    ├── IPerDeveloperMetricsService
    ├── ICollaborationMetricsService
    ├── IQualityMetricsService
    ├── ICodeCharacteristicsService
    └── IAdvancedMetricsService
```

Each metric calculation:
- Runs concurrently via `Task.WhenAll`
- Has isolated error handling
- Logs progress at DEBUG level
- Respects cancellation tokens
- Returns null on failure with error details in response

## Best Practices

1. **Time Windows**: Start with 30-day windows, increase only when needed
2. **Error Handling**: Always check the `errors` field and handle partial results
3. **Caching**: Consider caching responses on client side for dashboard views
4. **Rate Limiting**: Be mindful of GitLab API rate limits when calling frequently
5. **Monitoring**: Monitor response times and partial failure rates
