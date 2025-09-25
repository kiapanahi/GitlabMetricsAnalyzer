# User Metrics Collection API

This document describes the User Metrics Collection API endpoints that allow collecting, storing, and comparing user metrics over time for historical analysis and trend monitoring.

## Overview

The User Metrics Collection API provides functionality to:
- Collect and store user metrics snapshots for specific time periods (default: 3 months)
- Retrieve historical metrics for comparison and trend analysis
- Compare metrics between different time periods
- Track productivity trends over time

All metrics are timestamped when collected, allowing for historical comparisons and trend analysis.

## Base URL

All endpoints are prefixed with `/api/users/{userId}/metrics`

## Endpoints

### 1. Collect and Store User Metrics

**POST** `/api/users/{userId}/metrics/collect`

Collects comprehensive user metrics from GitLab API and stores them with a timestamp for historical comparison.

#### Parameters
- `userId` (path, required): The GitLab user ID
- `fromDate` (query, optional): Start date in ISO 8601 format (default: 3 months ago)
- `toDate` (query, optional): End date in ISO 8601 format (default: now)

#### Example Request
```http
POST /api/users/123/metrics/collect?fromDate=2024-01-01T00:00:00Z&toDate=2024-03-31T23:59:59Z
```

#### Response
```json
{
  "userId": 123,
  "username": "john.doe",
  "collectedAt": "2024-04-01T10:30:00Z",
  "fromDate": "2024-01-01T00:00:00Z",
  "toDate": "2024-03-31T23:59:59Z",
  "periodDays": 90,
  "message": "Successfully collected and stored metrics for user john.doe over 90 days",
  "metrics": {
    "id": 456,
    "userId": 123,
    "username": "john.doe",
    "email": "john.doe@company.com",
    "collectedAt": "2024-04-01T10:30:00Z",
    "fromDate": "2024-01-01T00:00:00Z",
    "toDate": "2024-03-31T23:59:59Z",
    "periodDays": 90,
    "totalCommits": 45,
    "totalLinesAdded": 2040,
    "totalLinesDeleted": 560,
    "totalLinesChanged": 2600,
    "averageCommitsPerDay": 0.5,
    "averageLinesChangedPerCommit": 57.8,
    "activeProjects": 8,
    "totalMergeRequestsCreated": 18,
    "totalMergeRequestsMerged": 16,
    "totalMergeRequestsReviewed": 24,
    "averageMergeRequestCycleTimeHours": 26.5,
    "mergeRequestMergeRate": 0.89,
    "totalPipelinesTriggered": 52,
    "successfulPipelines": 48,
    "failedPipelines": 4,
    "pipelineSuccessRate": 0.923,
    "averagePipelineDurationMinutes": 0,
    "totalIssuesCreated": 12,
    "totalIssuesAssigned": 12,
    "totalIssuesClosed": 10,
    "averageIssueResolutionTimeHours": 72.5,
    "totalCommentsOnMergeRequests": 48,
    "totalCommentsOnIssues": 6,
    "collaborationScore": 7.8,
    "productivityScore": 8.2,
    "productivityLevel": "High",
    "codeChurnRate": 0.05,
    "reviewThroughput": 0.85,
    "totalDataPoints": 156,
    "dataQuality": "Excellent"
  }
}
```

### 2. Get Historical User Metrics

**GET** `/api/users/{userId}/metrics/history`

Retrieves stored user metrics snapshots ordered by collection date for historical analysis.

#### Parameters
- `userId` (path, required): The GitLab user ID
- `limit` (query, optional): Maximum number of snapshots to return (default: 10, max: 100)

#### Example Request
```http
GET /api/users/123/metrics/history?limit=5
```

#### Response
```json
{
  "userId": 123,
  "username": "john.doe",
  "totalSnapshots": 5,
  "earliestSnapshot": "2024-01-01T10:00:00Z",
  "latestSnapshot": "2024-04-01T10:30:00Z",
  "snapshots": [
    {
      "id": 456,
      "userId": 123,
      "username": "john.doe",
      "collectedAt": "2024-04-01T10:30:00Z",
      "fromDate": "2024-01-01T00:00:00Z",
      "toDate": "2024-03-31T23:59:59Z",
      "periodDays": 90,
      "totalCommits": 45,
      "productivityScore": 8.2,
      "productivityLevel": "High",
      // ... other metrics
    },
    {
      "id": 455,
      "userId": 123,
      "username": "john.doe",
      "collectedAt": "2024-03-01T10:30:00Z",
      "fromDate": "2023-12-01T00:00:00Z",
      "toDate": "2024-02-29T23:59:59Z",
      "periodDays": 90,
      "totalCommits": 38,
      "productivityScore": 7.8,
      "productivityLevel": "High",
      // ... other metrics
    }
    // ... more snapshots
  ]
}
```

### 3. Get User Metrics in Date Range

**GET** `/api/users/{userId}/metrics/history/range`

Retrieves stored user metrics snapshots collected within a specific date range.

#### Parameters
- `userId` (path, required): The GitLab user ID
- `fromDate` (query, required): Start date for collection period in ISO 8601 format
- `toDate` (query, required): End date for collection period in ISO 8601 format

#### Example Request
```http
GET /api/users/123/metrics/history/range?fromDate=2024-01-01T00:00:00Z&toDate=2024-03-31T23:59:59Z
```

#### Response
Same structure as the history endpoint, but filtered by collection date range.

### 4. Compare User Metrics

**GET** `/api/users/{userId}/metrics/compare`

Compares user metrics between two specific collection dates to show trends and changes over time.

#### Parameters
- `userId` (path, required): The GitLab user ID
- `baselineDate` (query, required): Collection date for baseline metrics in ISO 8601 format
- `currentDate` (query, required): Collection date for current metrics in ISO 8601 format

#### Example Request
```http
GET /api/users/123/metrics/compare?baselineDate=2024-01-01T00:00:00Z&currentDate=2024-04-01T00:00:00Z
```

#### Response
```json
{
  "userId": 123,
  "username": "john.doe",
  "baselineMetrics": {
    "id": 420,
    "collectedAt": "2024-01-01T10:00:00Z",
    "totalCommits": 35,
    "productivityScore": 7.5,
    "pipelineSuccessRate": 0.89,
    // ... other baseline metrics
  },
  "currentMetrics": {
    "id": 456,
    "collectedAt": "2024-04-01T10:30:00Z",
    "totalCommits": 45,
    "productivityScore": 8.2,
    "pipelineSuccessRate": 0.923,
    // ... other current metrics
  },
  "changes": {
    "commitsChange": 10,
    "commitsChangePercent": 28.57,
    "linesChangedChange": 520,
    "linesChangedChangePercent": 25.0,
    "commitsPerDayChange": 0.11,
    "commitsPerDayChangePercent": 28.2,
    "mergeRequestsCreatedChange": 5,
    "mergeRequestsCreatedChangePercent": 38.46,
    "cycleTimeChange": -4.5,
    "cycleTimeChangePercent": -14.5,
    "mergeRateChange": 0.05,
    "mergeRateChangePercent": 5.95,
    "pipelineSuccessRateChange": 0.033,
    "pipelineSuccessRateChangePercent": 3.71,
    "pipelinesTriggeredChange": 8,
    "pipelinesTriggeredChangePercent": 18.18,
    "productivityScoreChange": 0.7,
    "productivityScoreChangePercent": 9.33,
    "productivityLevelChange": null,
    "overallTrend": "Improving",
    "keyImprovements": [
      "Increased commit activity",
      "Faster merge request cycle time",
      "Better pipeline success rate",
      "Higher productivity score"
    ],
    "areasOfConcern": []
  }
}
```

## Data Model

### FactUserMetrics

The stored user metrics contain the following key fields:

#### Identification
- `id`: Unique identifier for the metrics snapshot
- `userId`: GitLab user ID
- `username`: GitLab username
- `email`: User's email address
- `collectedAt`: When this snapshot was collected
- `fromDate`: Start date of the metrics period
- `toDate`: End date of the metrics period
- `periodDays`: Duration of the period in days

#### Code Contribution Metrics
- `totalCommits`: Total number of commits
- `totalLinesAdded`: Total lines of code added
- `totalLinesDeleted`: Total lines of code deleted
- `totalLinesChanged`: Total lines of code changed
- `averageCommitsPerDay`: Average commits per day
- `averageLinesChangedPerCommit`: Average lines changed per commit
- `activeProjects`: Number of distinct projects where the user has commits

#### Code Review Metrics
- `totalMergeRequestsCreated`: Total merge requests created
- `totalMergeRequestsMerged`: Total merge requests merged
- `totalMergeRequestsReviewed`: Total merge requests reviewed
- `averageMergeRequestCycleTimeHours`: Average cycle time in hours
- `mergeRequestMergeRate`: Percentage of MRs that get merged

#### Quality Metrics
- `totalPipelinesTriggered`: Total pipelines triggered
- `successfulPipelines`: Number of successful pipelines
- `failedPipelines`: Number of failed pipelines
- `pipelineSuccessRate`: Pipeline success rate (0-1)
- `averagePipelineDurationMinutes`: Average pipeline duration

#### Issue Management Metrics
- `totalIssuesCreated`: Total issues created
- `totalIssuesAssigned`: Total issues assigned
- `totalIssuesClosed`: Total issues closed
- `averageIssueResolutionTimeHours`: Average resolution time

#### Collaboration Metrics
- `totalCommentsOnMergeRequests`: Total MR comments
- `totalCommentsOnIssues`: Total issue comments
- `collaborationScore`: Overall collaboration score

#### Productivity Metrics
- `productivityScore`: Overall productivity score
- `productivityLevel`: Productivity level (Low, Medium, High)
- `codeChurnRate`: Rate of code churn
- `reviewThroughput`: Review throughput rate

#### Metadata
- `totalDataPoints`: Number of data points used in calculation
- `dataQuality`: Quality assessment (Poor, Fair, Good, Excellent)

## Error Handling

All endpoints return appropriate HTTP status codes:

- **200 OK**: Successful retrieval
- **201 Created**: Successful metrics collection and storage
- **400 Bad Request**: Invalid parameters or date formats
- **404 Not Found**: User or metrics not found
- **500 Internal Server Error**: Server-side errors

### Error Response Format
```json
{
  "error": "Error description"
}
```

## Usage Examples

### Collecting Metrics for Last 3 Months (Default)
```bash
curl -X POST "https://api.company.com/api/users/123/metrics/collect"
```

### Collecting Metrics for Custom Period
```bash
curl -X POST "https://api.company.com/api/users/123/metrics/collect?fromDate=2024-01-01T00:00:00Z&toDate=2024-03-31T23:59:59Z"
```

### Getting Recent History
```bash
curl "https://api.company.com/api/users/123/metrics/history?limit=5"
```

### Comparing Two Time Periods
```bash
curl "https://api.company.com/api/users/123/metrics/compare?baselineDate=2024-01-01T00:00:00Z&currentDate=2024-04-01T00:00:00Z"
```

## Best Practices

1. **Regular Collection**: Collect metrics regularly (e.g., monthly) to build a meaningful history
2. **Consistent Periods**: Use consistent time periods for better comparisons
3. **Data Quality**: Monitor the `dataQuality` field to ensure reliable metrics
4. **Trend Analysis**: Use the comparison endpoint to identify trends and patterns
5. **Performance**: The system prevents duplicate collections within 1 hour for the same period

## Rate Limiting

The API may be subject to rate limiting to ensure system stability. If you encounter rate limits, wait and retry your request.

## Authentication

Authentication is required for all endpoints. Ensure you have proper API access tokens configured.

## Data Freshness

Metrics are calculated on-demand from the current GitLab data when collected. This ensures the most up-to-date information but may take longer for users with extensive activity.