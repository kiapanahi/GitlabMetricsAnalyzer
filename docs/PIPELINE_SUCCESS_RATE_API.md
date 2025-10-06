# Pipeline Success Rate Metrics API

## Overview

The Pipeline Success Rate feature provides metrics about CI/CD pipeline execution success for individual developers. This feature fetches pipeline data directly from GitLab (not from the database) and calculates the success rate as a ratio (0.0-1.0) representing the percentage of successful pipeline runs.

## Endpoint

### GET `/api/metrics/per-developer/{userId}/pipeline-success-rate`

Calculates the pipeline success rate for a specific developer by analyzing their pipeline executions.

**Path Parameters:**
- `userId` (long, required): The GitLab user ID to analyze

**Query Parameters:**
- `lookbackDays` (int, optional): Number of days to look back. Default: 30, Min: 1, Max: 365

**Response:** `PipelineSuccessRateResult` object

## Response Schema

```json
{
  "userId": 1,
  "username": "alice.dev",
  "email": "alice@example.com",
  "lookbackDays": 30,
  "analysisStartDate": "2025-09-05T00:00:00Z",
  "analysisEndDate": "2025-10-05T00:00:00Z",
  "totalPipelines": 45,
  "successfulPipelines": 38,
  "failedPipelines": 5,
  "otherStatusPipelines": 2,
  "pipelineSuccessRate": 0.844,
  "projects": [
    {
      "projectId": 101,
      "projectName": "Backend API",
      "totalPipelines": 25,
      "successfulPipelines": 22,
      "failedPipelines": 3
    },
    {
      "projectId": 102,
      "projectName": "Frontend App",
      "totalPipelines": 20,
      "successfulPipelines": 16,
      "failedPipelines": 2
    }
  ]
}
```

## Field Descriptions

### Root Level
- `userId`: The GitLab user ID analyzed
- `username`: The GitLab username
- `email`: The user's email address
- `lookbackDays`: Number of days analyzed
- `analysisStartDate`: Start date of the analysis period (UTC)
- `analysisEndDate`: End date of the analysis period (UTC)
- `totalPipelines`: Total number of pipelines triggered by the developer
- `successfulPipelines`: Number of pipelines with "success" status
- `failedPipelines`: Number of pipelines with "failed" status
- `otherStatusPipelines`: Number of pipelines with other statuses (running, pending, canceled, etc.)
- `pipelineSuccessRate`: Success rate as a ratio (0.0-1.0). Returns `null` if no pipelines found.

### Projects Array
- `projectId`: GitLab project ID
- `projectName`: Name of the project
- `totalPipelines`: Total pipelines for this project
- `successfulPipelines`: Successful pipelines for this project
- `failedPipelines`: Failed pipelines for this project

## Usage Examples

### Example 1: Basic Request (Default 30 Days)
```bash
curl -X GET "http://localhost:5000/api/metrics/per-developer/1/pipeline-success-rate"
```

### Example 2: Custom Lookback Period (90 Days)
```bash
curl -X GET "http://localhost:5000/api/metrics/per-developer/1/pipeline-success-rate?lookbackDays=90"
```

### Example 3: Using jq to Extract Success Rate
```bash
curl -s "http://localhost:5000/api/metrics/per-developer/1/pipeline-success-rate" | \
  jq '.pipelineSuccessRate'
```

### Example 4: Get Project Breakdown
```bash
curl -s "http://localhost:5000/api/metrics/per-developer/1/pipeline-success-rate" | \
  jq '.projects[] | {name: .projectName, rate: (.successfulPipelines / .totalPipelines)}'
```

## Response Status Codes

- **200 OK**: Success - Returns pipeline success rate metrics
- **400 Bad Request**: Invalid parameters (e.g., lookbackDays out of range)
- **404 Not Found**: User not found
- **500 Internal Server Error**: Server error occurred

## Error Response Format

```json
{
  "error": "User with ID 999 not found"
}
```

## Implementation Details

### Data Source
- **Live Data**: Fetches pipeline data directly from GitLab API using `GetPipelinesAsync()`
- **User Projects**: Queries all projects where the user has contributed
- **Pipeline Filtering**: Only includes pipelines where the user is the author (triggered by their commits/MRs)

### Calculation Formula
```
Pipeline Success Rate = Successful Pipelines / Total Pipelines
```
where:
- Successful Pipelines = count of pipelines with status "success" (case-insensitive)
- Total Pipelines = all pipelines triggered by the developer in the time window

### Edge Cases Handled
1. **No Projects**: Returns empty result with null success rate
2. **No Pipelines**: Returns result with 0 total pipelines and null success rate
3. **Mixed Statuses**: Correctly categorizes success, failed, and other statuses
4. **User Not Found**: Returns 404 with error message
5. **Invalid Parameters**: Returns 400 with validation error

## Performance Considerations

- The endpoint fetches data from all projects where the user has contributed
- For users with many projects or pipelines, response time may vary
- Consider caching results for frequently accessed metrics
- Use appropriate lookback periods to balance data freshness and performance

## Related Endpoints

- **Commit Time Analysis**: `/api/analysis/commit-time/{userId}` - Analyzes commit timing patterns
- Future endpoints for other per-developer metrics will follow similar patterns

## Testing

### Using Mock Client
The service includes a mock GitLab client for testing without actual GitLab API access. Configure in `appsettings.Development.json`:

```json
{
  "GitLab": {
    "UseMockClient": true
  }
}
```

The mock client provides:
- 5 test users (alice.dev, bob.smith, charlie.jones, diana.prince, eve.wilson)
- Multiple projects with realistic pipeline data
- Various pipeline statuses for comprehensive testing

### Example Test Users
- User ID 1: alice.dev - Has pipelines across multiple projects
- User ID 2: bob.smith - Has pipelines with mixed success/failure rates

## Changelog

### Version 1.0.0 (2025-10-05)
- Initial implementation of pipeline success rate endpoint
- Support for live data fetching from GitLab API
- Per-project breakdown of pipeline metrics
- Comprehensive unit and integration tests
