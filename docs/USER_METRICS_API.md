# User Metrics API

This document describes the User Metrics API endpoints that provide comprehensive analytics and insights for individual developers based on GitLab activity data.

## Overview

The User Metrics API offers detailed productivity, collaboration, and quality metrics for developers, helping organizations understand individual performance patterns and make data-driven decisions.

## Endpoints

### 1. Get Comprehensive User Metrics

**`GET /api/users/{userId}/metrics`**

Returns detailed metrics across all categories for a specific user.

#### Parameters
- `userId` (path, required): The GitLab user ID
- `fromDate` (query, optional): Start date in ISO 8601 format (default: 30 days ago)
- `toDate` (query, optional): End date in ISO 8601 format (default: now)

#### Response
```json
{
  "userId": 123,
  "userName": "john.doe",
  "email": "john.doe@company.com",
  "fromDate": "2024-01-01T00:00:00Z",
  "toDate": "2024-01-31T23:59:59Z",
  "codeContribution": {
    "totalCommits": 45,
    "commitsPerDay": 1.5,
    "totalLinesAdded": 2150,
    "totalLinesDeleted": 890,
    "totalLinesChanged": 3040,
    "averageCommitSize": 67.6,
    "filesModified": 156,
    "topLanguages": ["C#", "TypeScript", "SQL"],
    "weekendCommits": 3,
    "eveningCommits": 12
  },
  "codeReview": {
    "mergeRequestsCreated": 18,
    "mergeRequestsReviewed": 25,
    "averageMRSize": 145.2,
    "averageMRCycleTime": "2.14:30:00",
    "averageTimeToFirstReview": "0.06:15:00",
    "averageTimeInReview": "1.12:45:00",
    "reviewParticipationRate": 0.78,
    "approvalsGiven": 23,
    "approvalsReceived": 16,
    "selfMergeRate": 0.11
  },
  "issueManagement": {
    "issuesCreated": 8,
    "issuesResolved": 12,
    "averageIssueResolutionTime": "3.08:30:00",
    "issueResolutionRate": 0.92,
    "reopenedIssues": 1
  },
  "collaboration": {
    "uniqueReviewers": 6,
    "uniqueReviewees": 8,
    "crossTeamCollaborations": 4,
    "knowledgeSharingScore": 7.8,
    "mentorshipActivities": 2
  },
  "quality": {
    "pipelineSuccessRate": 0.94,
    "pipelineFailures": 3,
    "codeRevertRate": 0.02,
    "bugFixRatio": 0.15,
    "testCoverage": 0.85,
    "securityIssues": 0
  },
  "productivity": {
    "velocityScore": 8.2,
    "efficiencyScore": 7.9,
    "impactScore": 8.5,
    "productivityTrend": "Increasing",
    "focusTimeHours": 120
  },
  "metadata": {
    "calculatedAt": "2024-02-01T10:30:00Z",
    "dataSource": "GitLab API",
    "totalDataPoints": 156,
    "lastDataUpdate": "2024-01-31T23:45:00Z"
  }
}
```

### 2. Get User Metrics Summary

**`GET /api/users/{userId}/metrics/summary`**

Returns a high-level overview of key performance indicators.

#### Parameters
- `userId` (path, required): The GitLab user ID
- `fromDate` (query, optional): Start date in ISO 8601 format (default: 30 days ago)
- `toDate` (query, optional): End date in ISO 8601 format (default: now)

#### Response
```json
{
  "userId": 123,
  "userName": "john.doe",
  "fromDate": "2024-01-01T00:00:00Z",
  "toDate": "2024-01-31T23:59:59Z",
  "totalCommits": 45,
  "totalMergeRequests": 18,
  "averageCommitsPerDay": 1.5,
  "pipelineSuccessRate": 0.94,
  "averageMRCycleTime": "2.14:30:00",
  "totalLinesChanged": 3040,
  "productivityScore": "High",
  "metadata": {
    "calculatedAt": "2024-02-01T10:30:00Z",
    "dataSource": "GitLab API",
    "totalDataPoints": 63,
    "lastDataUpdate": null
  }
}
```

### 3. Get User Metrics Trends

**`GET /api/users/{userId}/metrics/trends`**

Returns time-series data showing how metrics have changed over time.

#### Parameters
- `userId` (path, required): The GitLab user ID
- `fromDate` (query, optional): Start date in ISO 8601 format (default: 90 days ago)
- `toDate` (query, optional): End date in ISO 8601 format (default: now)
- `period` (query, optional): Trend period - "Daily", "Weekly", or "Monthly" (default: "Weekly")

#### Response
```json
{
  "userId": 123,
  "userName": "john.doe",
  "fromDate": "2023-11-01T00:00:00Z",
  "toDate": "2024-01-31T23:59:59Z",
  "period": "Weekly",
  "trendPoints": [
    {
      "date": "2023-11-01T00:00:00Z",
      "commits": 8,
      "mergeRequests": 3,
      "pipelineSuccessRate": 0.92,
      "linesChanged": 450,
      "productivityScore": 7.8
    },
    {
      "date": "2023-11-08T00:00:00Z",
      "commits": 12,
      "mergeRequests": 4,
      "pipelineSuccessRate": 0.95,
      "linesChanged": 680,
      "productivityScore": 8.2
    }
  ],
  "metadata": {
    "calculatedAt": "2024-02-01T10:30:00Z",
    "dataSource": "GitLab API",
    "totalDataPoints": 156,
    "lastDataUpdate": null
  }
}
```

### 4. Get User Metrics Comparison

**`GET /api/users/{userId}/metrics/comparison`**

Returns comparative analysis showing how the user's performance compares to peers.

#### Parameters
- `userId` (path, required): The GitLab user ID
- `fromDate` (query, optional): Start date in ISO 8601 format (default: 30 days ago)
- `toDate` (query, optional): End date in ISO 8601 format (default: now)
- `compareWith` (query, optional): Comma-separated list of user IDs to compare with

#### Response
```json
{
  "userId": 123,
  "userName": "john.doe",
  "fromDate": "2024-01-01T00:00:00Z",
  "toDate": "2024-01-31T23:59:59Z",
  "userMetrics": {
    "userId": 123,
    "name": "john.doe",
    "totalCommits": 45,
    "totalMergeRequests": 18,
    "pipelineSuccessRate": 0.94,
    "averageMRCycleTime": "2.14:30:00",
    "totalLinesChanged": 3040,
    "productivityScore": 8.2
  },
  "teamAverage": {
    "userId": null,
    "name": "Team Average",
    "totalCommits": 38,
    "totalMergeRequests": 15,
    "pipelineSuccessRate": 0.91,
    "averageMRCycleTime": "2.18:45:00",
    "totalLinesChanged": 2850,
    "productivityScore": 7.6
  },
  "peerMetrics": [
    {
      "userId": 124,
      "name": "jane.smith",
      "totalCommits": 52,
      "totalMergeRequests": 20,
      "pipelineSuccessRate": 0.96,
      "averageMRCycleTime": "1.22:15:00",
      "totalLinesChanged": 3420,
      "productivityScore": 8.8
    }
  ],
  "metadata": {
    "calculatedAt": "2024-02-01T10:30:00Z",
    "dataSource": "GitLab API",
    "totalDataPoints": 3,
    "lastDataUpdate": null
  }
}
```

## Metrics Explained

### Code Contribution Metrics
- **Total Commits**: Number of commits authored by the user
- **Commits Per Day**: Average daily commit frequency
- **Lines Added/Deleted/Changed**: Code volume metrics
- **Average Commit Size**: Average lines changed per commit
- **Weekend/Evening Commits**: Work pattern indicators

### Code Review Metrics
- **MR Cycle Time**: Time from MR creation to merge
- **Time to First Review**: Speed of initial review response
- **Review Participation Rate**: Percentage of team MRs reviewed
- **Self Merge Rate**: Percentage of MRs merged without external review

### Quality Metrics
- **Pipeline Success Rate**: Percentage of successful CI/CD runs
- **Code Revert Rate**: Frequency of code reversions detected through commit message analysis (revert, rollback, undo patterns)
- **Bug Fix Ratio**: Proportion of work dedicated to bug fixes, analyzed from commit messages and MR titles with keywords like fix, bug, hotfix, patch
- **Test Coverage**: Estimated coverage based on test-related commits and pipeline success rates (capped at 85% as it's an estimate)
- **Security Issues**: Count of security-related work items detected from commit messages and MR titles with security keywords

### Productivity Scoring
The productivity score is calculated using a weighted algorithm considering:
- Commit frequency (weight: 2)
- MR throughput (weight: 3)
- Pipeline success rate (weight: 5)

Scores are normalized to a 0-10 scale:
- **High**: 7.5-10
- **Medium**: 5.0-7.4
- **Low**: 0-4.9

## Error Handling

### Common Error Responses

#### 400 Bad Request
```json
{
  "error": "Invalid date format: 2024-13-01. Use ISO 8601 format (e.g., 2024-01-01T00:00:00Z)."
}
```

#### 404 Not Found
```json
{
  "error": "User with ID 999 not found"
}
```

#### 500 Internal Server Error
```json
{
  "title": "Internal Server Error",
  "detail": "An error occurred while calculating metrics",
  "statusCode": 500
}
```

## Usage Examples

### Basic Usage
```bash
# Get comprehensive metrics for user 123
curl "http://localhost:5000/api/users/123/metrics"

# Get summary for last 7 days
curl "http://localhost:5000/api/users/123/metrics/summary?fromDate=2024-01-25T00:00:00Z&toDate=2024-02-01T00:00:00Z"
```

### Advanced Usage
```bash
# Get daily trends for last month
curl "http://localhost:5000/api/users/123/metrics/trends?period=Daily&fromDate=2024-01-01T00:00:00Z"

# Compare with specific peers
curl "http://localhost:5000/api/users/123/metrics/comparison?compareWith=124,125,126"
```

## Rate Limiting

The API implements standard rate limiting:
- **Rate**: 100 requests per minute per IP
- **Burst**: 20 requests in 10 seconds

Exceeded limits return HTTP 429 with retry information in headers.

## Authentication

Currently, the API operates without authentication in development mode. Production deployments should implement appropriate authentication mechanisms.

## Data Freshness

Metrics are calculated in real-time from the most recent GitLab data ingestion. Data freshness information is included in the `metadata.lastDataUpdate` field of all responses.