# API Usage Guide

This guide provides comprehensive examples for using the GitLab Metrics Analyzer API v1, including request/response patterns, versioning, and best practices.

## Table of Contents
- [API Overview](#api-overview)
- [Authentication](#authentication)
- [Versioning Strategy](#versioning-strategy)
- [Developer Metrics APIs](#developer-metrics-apis)
- [Export APIs](#export-apis)
- [Data Quality APIs](#data-quality-apis)
- [Error Handling](#error-handling)
- [Rate Limiting](#rate-limiting)
- [Best Practices](#best-practices)

## API Overview

The GitLab Metrics Analyzer exposes RESTful APIs for:
- **Manual data collection triggers**
- **Developer productivity metrics retrieval**
- **Data export generation**
- **System health monitoring**
- **Data quality assessment**

**Base URL**: `http://localhost:5000` (adjust for your deployment)

**API Versions**: 
- `v1` (current): `/api/v1/*` - Stable, production-ready endpoints

## Authentication

Currently, the API uses internal authentication suitable for trusted network environments. For production deployments, implement proper authentication:

```bash
# Example with API key (when implemented)
curl -H "Authorization: Bearer your-api-key" "http://localhost:5000/api/v1/metrics/developers"
```

## Versioning Strategy

### Schema Versioning
All API responses include schema version information:

```json
{
  "schemaVersion": "1.0.0",
  "data": {...},
  "metadata": {...}
}
```

### API Evolution
- **v1**: Current stable version
- **v2**: Future version (backward compatibility maintained)
- **Legacy**: Deprecated endpoints (marked for removal)

### Version Headers
```bash
# Request specific API version
curl -H "Accept: application/vnd.gitlab-metrics.v1+json" \
     "http://localhost:5000/api/v1/metrics/developers"
```

## Developer Metrics APIs

### Get All Developers Metrics

**Endpoint**: `GET /api/v1/metrics/developers`

Retrieves paginated developer metrics with filtering capabilities.

```bash
# Basic request - last 30 days, page 1
curl "http://localhost:5000/api/v1/metrics/developers"

# Custom time window and pagination
curl "http://localhost:5000/api/v1/metrics/developers?windowDays=90&page=2&pageSize=50"

# Filter by specific projects
curl "http://localhost:5000/api/v1/metrics/developers?projectIds[]=123&projectIds[]=456&windowDays=30"
```

**Query Parameters**:
- `windowDays` (optional): Number of days to include (default: 30, max: 365)
- `projectIds[]` (optional): Array of project IDs to filter by
- `page` (optional): Page number (default: 1)
- `pageSize` (optional): Items per page (default: 20, max: 100)

**Response**:
```json
{
  "schemaVersion": "1.0.0",
  "data": [
    {
      "developerId": 123,
      "username": "john.doe",
      "displayName": "John Doe",
      "email": "john.doe@company.com",
      "isActive": true,
      "metrics": {
        "window": {
          "startDate": "2024-01-01T00:00:00Z",
          "endDate": "2024-01-31T23:59:59Z", 
          "durationDays": 31
        },
        "codeContribution": {
          "totalCommits": 45,
          "linesAdded": 2040,
          "linesDeleted": 560,
          "linesChanged": 2600,
          "averageCommitsPerDay": 1.45,
          "averageCommitSize": 57.8,
          "activeProjects": 8,
          "filesChanged": 156
        },
        "codeReview": {
          "mergeRequestsCreated": 18,
          "mergeRequestsMerged": 16, 
          "mergeRequestsReviewed": 24,
          "averageCycleTimeHours": 26.5,
          "averageReviewTimeHours": 4.2,
          "mergeRate": 0.89,
          "reviewParticipationRate": 0.75
        },
        "quality": {
          "pipelineSuccessRate": 0.923,
          "pipelineFailures": 4,
          "codeRevertRate": 0.02,
          "testCoverage": 0.85
        },
        "collaboration": {
          "reviewsGiven": 24,
          "commentsOnMergeRequests": 48,
          "uniqueCollaborators": 12,
          "knowledgeSharingScore": 7.8
        },
        "productivity": {
          "velocityScore": 8.2,
          "efficiencyScore": 7.9,
          "impactScore": 8.5,
          "overallProductivityLevel": "High"
        }
      },
      "metadata": {
        "lastUpdated": "2024-01-15T10:34:22Z",
        "dataPoints": 156,
        "dataQuality": "Excellent"
      }
    }
  ],
  "pagination": {
    "page": 1,
    "pageSize": 20,
    "totalCount": 120,
    "totalPages": 6
  },
  "filterApplied": {
    "windowDays": 30,
    "projectIds": [],
    "windowEnd": "2024-01-31T23:59:59Z"
  }
}
```

### Get Individual Developer Metrics

**Endpoint**: `GET /api/v1/metrics/developers/{developer_id}`

Retrieves detailed metrics for a specific developer including historical sparkline data.

```bash
# Basic developer metrics
curl "http://localhost:5000/api/v1/metrics/developers/123"

# With custom window and sparkline data
curl "http://localhost:5000/api/v1/metrics/developers/123?windowDays=90&includeSparkline=true"

# Filter by specific projects
curl "http://localhost:5000/api/v1/metrics/developers/123?projectIds[]=456&projectIds[]=789"
```

**Query Parameters**:
- `windowDays` (optional): Number of days to include (default: 30)
- `projectIds[]` (optional): Array of project IDs to filter by
- `includeSparkline` (optional): Include daily trend data (default: false)

**Response**:
```json
{
  "schemaVersion": "1.0.0", 
  "data": {
    "developerId": 123,
    "username": "john.doe",
    "displayName": "John Doe",
    "email": "john.doe@company.com",
    "isActive": true,
    "aliases": [
      {
        "type": "email",
        "value": "j.doe@company.com"
      },
      {
        "type": "username", 
        "value": "johnd"
      }
    ],
    "metrics": {
      "window": {
        "startDate": "2024-01-01T00:00:00Z",
        "endDate": "2024-01-31T23:59:59Z",
        "durationDays": 31
      },
      "codeContribution": {
        "totalCommits": 45,
        "linesAdded": 2040,
        "linesDeleted": 560, 
        "linesChanged": 2600,
        "averageCommitsPerDay": 1.45,
        "averageCommitSize": 57.8,
        "activeProjects": 8,
        "weekendCommits": 3,
        "eveningCommits": 12,
        "commitDistribution": {
          "monday": 8,
          "tuesday": 7,
          "wednesday": 9,
          "thursday": 8,
          "friday": 6,
          "saturday": 2,
          "sunday": 1
        }
      },
      "codeReview": {
        "mergeRequestsCreated": 18,
        "mergeRequestsMerged": 16,
        "mergeRequestsReviewed": 24,
        "averageCycleTimeHours": 26.5,
        "averageReviewTimeHours": 4.2,
        "mergeRate": 0.89,
        "reviewParticipationRate": 0.75,
        "selfMergeRate": 0.11,
        "timeToFirstReviewHours": 2.1
      },
      "quality": {
        "pipelineSuccessRate": 0.923,
        "pipelineFailures": 4,
        "codeRevertRate": 0.02,
        "bugFixRatio": 0.15,
        "testCoverage": 0.85,
        "securityIssues": 0
      },
      "collaboration": {
        "reviewsGiven": 24,
        "commentsOnMergeRequests": 48,
        "commentsOnIssues": 6,
        "uniqueCollaborators": 12,
        "knowledgeSharingScore": 7.8,
        "mentorshipActivities": 2
      },
      "productivity": {
        "velocityScore": 8.2,
        "efficiencyScore": 7.9,
        "impactScore": 8.5,
        "overallProductivityLevel": "High",
        "productivityTrend": "Increasing",
        "focusTimeHours": 120
      }
    },
    "sparklineData": {
      "commitsPerDay": [2, 1, 3, 0, 2, 4, 1, 2, 3, 2],
      "linesChangedPerDay": [120, 45, 203, 0, 89, 256, 67, 134, 178, 98],
      "mergeRequestsPerWeek": [3, 4, 2, 5],
      "pipelineSuccessRatePerWeek": [0.90, 0.95, 0.88, 0.96]
    },
    "metadata": {
      "lastUpdated": "2024-01-15T10:34:22Z",
      "dataPoints": 156,
      "dataQuality": "Excellent",
      "calculatedAt": "2024-01-15T10:34:22Z"
    }
  }
}
```

### Get Metrics Catalog

**Endpoint**: `GET /api/v1/catalog`

Retrieves the complete catalog of available metrics with their definitions and current schema version.

```bash
curl "http://localhost:5000/api/v1/catalog"
```

**Response**:
```json
{
  "schemaVersion": "1.0.0",
  "catalog": {
    "codeContribution": {
      "totalCommits": {
        "description": "Total number of commits authored by the developer",
        "dataType": "integer",
        "unit": "count",
        "aggregation": "sum"
      },
      "linesAdded": {
        "description": "Total lines of code added",
        "dataType": "integer", 
        "unit": "lines",
        "aggregation": "sum"
      },
      "averageCommitsPerDay": {
        "description": "Average number of commits per day",
        "dataType": "decimal",
        "unit": "commits/day",
        "aggregation": "average"
      }
    },
    "codeReview": {
      "mergeRequestsCreated": {
        "description": "Number of merge requests created by the developer",
        "dataType": "integer",
        "unit": "count", 
        "aggregation": "sum"
      },
      "averageCycleTimeHours": {
        "description": "Average time from MR creation to merge in hours",
        "dataType": "decimal",
        "unit": "hours",
        "aggregation": "average"
      }
    },
    "quality": {
      "pipelineSuccessRate": {
        "description": "Percentage of successful CI/CD pipeline runs",
        "dataType": "decimal",
        "unit": "percentage",
        "range": [0, 1],
        "aggregation": "average"
      }
    },
    "collaboration": {
      "reviewsGiven": {
        "description": "Number of code reviews provided to other developers",
        "dataType": "integer",
        "unit": "count",
        "aggregation": "sum"
      },
      "knowledgeSharingScore": {
        "description": "Composite score measuring knowledge sharing activities",
        "dataType": "decimal",
        "unit": "score",
        "range": [0, 10],
        "aggregation": "weighted_average"
      }
    },
    "productivity": {
      "velocityScore": {
        "description": "Overall velocity score based on delivery speed and consistency",
        "dataType": "decimal", 
        "unit": "score",
        "range": [0, 10],
        "aggregation": "weighted_average"
      },
      "overallProductivityLevel": {
        "description": "Categorical productivity assessment",
        "dataType": "string",
        "allowedValues": ["Low", "Medium", "High", "Exceptional"],
        "aggregation": "mode"
      }
    }
  },
  "metadata": {
    "version": "1.0.0",
    "lastUpdated": "2024-01-15T00:00:00Z",
    "totalMetrics": 24,
    "deprecatedMetrics": [],
    "newMetrics": []
  }
}
```

## Export APIs

### Generate Developer Metrics Export

**Endpoint**: `GET /api/exports/developers`

Generates comprehensive developer metrics export in various formats.

```bash
# JSON export (default)
curl "http://localhost:5000/api/exports/developers?windowDays=30" \
  -H "Accept: application/json" \
  -o developer_metrics.json

# Excel export for stakeholder reporting
curl "http://localhost:5000/api/exports/developers?windowDays=90&format=excel" \
  -H "Accept: application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" \
  -o quarterly_metrics.xlsx

# CSV export for data analysis
curl "http://localhost:5000/api/exports/developers?windowDays=7&format=csv" \
  -H "Accept: text/csv" \
  -o weekly_metrics.csv
```

**Query Parameters**:
- `windowDays` (optional): Time window for metrics (default: 30)
- `format` (optional): Export format - json, csv, excel (default: json)
- `projectIds[]` (optional): Filter by specific projects
- `includeSparklines` (optional): Include trend data (default: false)

**JSON Response**:
```json
{
  "exportMetadata": {
    "generatedAt": "2024-01-15T10:30:00Z",
    "format": "json",
    "schemaVersion": "1.0.0",
    "window": {
      "startDate": "2024-01-01T00:00:00Z",
      "endDate": "2024-01-31T23:59:59Z",
      "durationDays": 31
    },
    "filters": {
      "projectIds": [],
      "includeSparklines": false
    },
    "summary": {
      "totalDevelopers": 25,
      "activeDevelopers": 23,
      "totalCommits": 1250,
      "totalMergeRequests": 340
    }
  },
  "developers": [
    {
      "developerId": 123,
      "username": "john.doe", 
      "displayName": "John Doe",
      "metrics": {
        // ... full metrics structure as in individual developer response
      }
    }
  ]
}
```

### Check Export Status

**Endpoint**: `GET /api/exports/status`

Check the status of export generation operations.

```bash
curl "http://localhost:5000/api/exports/status"
```

**Response**:
```json
{
  "activeExports": [
    {
      "exportId": "export-123-456",
      "status": "Generating", 
      "format": "excel",
      "startedAt": "2024-01-15T10:25:00Z",
      "estimatedCompletion": "2024-01-15T10:27:00Z",
      "progress": "60%"
    }
  ],
  "recentExports": [
    {
      "exportId": "export-123-455",
      "status": "Completed",
      "format": "json", 
      "completedAt": "2024-01-15T10:20:00Z",
      "downloadUrl": "/api/exports/download/export-123-455"
    }
  ]
}
```

## Data Quality APIs

### Get Data Quality Reports

**Endpoint**: `GET /api/data-quality/reports`

Retrieves comprehensive data quality assessment reports.

```bash
# Current data quality status
curl "http://localhost:5000/api/data-quality/reports"

# Historical quality trends
curl "http://localhost:5000/api/data-quality/reports/trends?days=30"
```

**Response**:
```json
{
  "reportGeneratedAt": "2024-01-15T10:30:00Z",
  "overall": {
    "completenessScore": 0.95,
    "consistencyScore": 0.98,
    "accuracyScore": 0.97,
    "overallGrade": "A"
  },
  "completeness": {
    "developerCoverage": 0.96,
    "projectCoverage": 0.94,
    "timeCoverage": 0.98,
    "missingDataPoints": 42,
    "issues": [
      {
        "type": "missing_developer_data",
        "description": "3 developers have no activity in the last 30 days",
        "severity": "low",
        "affectedDevelopers": [125, 126, 127]
      }
    ]
  },
  "consistency": {
    "crossEntityConsistency": 0.99,
    "temporalConsistency": 0.97,
    "aggregationConsistency": 0.98,
    "issues": [
      {
        "type": "aggregation_mismatch", 
        "description": "Minor discrepancies in weekly rollups vs daily sums",
        "severity": "low",
        "affectedPeriods": ["2024-01-15"]
      }
    ]
  },
  "accuracy": {
    "spotCheckResults": 0.97,
    "businessRuleValidation": 0.98,
    "issues": [
      {
        "type": "negative_metrics",
        "description": "2 records with negative line counts detected",
        "severity": "medium",
        "affectedRecords": ["commit-123", "commit-456"]
      }
    ]
  },
  "recommendations": [
    "Consider running incremental collection to fill data gaps",
    "Review bot detection patterns for user 125-127",
    "Investigate negative line count records"
  ]
}
```

## Error Handling

### Standard Error Response Format

All API endpoints return consistent error responses:

```json
{
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Invalid query parameter value", 
    "details": "windowDays must be between 1 and 365",
    "timestamp": "2024-01-15T10:30:00Z",
    "traceId": "550e8400-e29b-41d4-a716-446655440000"
  }
}
```

### Common Error Codes

#### Client Errors (4xx)
- `VALIDATION_ERROR` (400): Invalid request parameters
- `DEVELOPER_NOT_FOUND` (404): Developer ID does not exist
- `RESOURCE_NOT_FOUND` (404): Requested resource not found
- `INVALID_DATE_RANGE` (400): Date parameters are invalid
- `PAGE_OUT_OF_RANGE` (400): Pagination parameters exceed limits

#### Server Errors (5xx)
- `INTERNAL_ERROR` (500): General server error
- `DATABASE_ERROR` (500): Database connectivity issues
- `COLLECTION_ERROR` (500): Data collection failures
- `EXPORT_GENERATION_ERROR` (500): Export creation failures
- `GITLAB_API_ERROR` (502): GitLab API connectivity issues

### Example Error Responses

```bash
# Invalid developer ID
curl "http://localhost:5000/api/v1/metrics/developers/999999"
```

```json
{
  "error": {
    "code": "DEVELOPER_NOT_FOUND",
    "message": "Developer with ID 999999 not found",
    "timestamp": "2024-01-15T10:30:00Z",
    "traceId": "550e8400-e29b-41d4-a716-446655440000"
  }
}
```

```bash
# Invalid window parameter
curl "http://localhost:5000/api/v1/metrics/developers?windowDays=400"
```

```json
{
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Invalid windowDays parameter",
    "details": "windowDays must be between 1 and 365, received: 400",
    "timestamp": "2024-01-15T10:30:00Z",
    "traceId": "660f8500-f39c-52e5-b827-557766551111"
  }
}
```

## Rate Limiting

### Current Implementation
The v1 system does not implement rate limiting, but it's planned for future versions.

### Future Rate Limiting (vNext)
```http
HTTP/1.1 429 Too Many Requests
X-RateLimit-Limit: 1000
X-RateLimit-Remaining: 0
X-RateLimit-Reset: 1642248600

{
  "error": {
    "code": "RATE_LIMIT_EXCEEDED",
    "message": "API rate limit exceeded",
    "details": "Limit: 1000 requests per hour. Reset at: 2024-01-15T11:30:00Z"
  }
}
```

## Best Practices

### Request Patterns

#### Efficient Pagination
```bash
# Use reasonable page sizes
curl "http://localhost:5000/api/v1/metrics/developers?pageSize=20&page=1"

# Cache results when possible
curl -H "If-None-Match: \"abc123\"" "http://localhost:5000/api/v1/metrics/developers"
```

#### Filtering for Performance
```bash
# Filter by specific projects when possible
curl "http://localhost:5000/api/v1/metrics/developers?projectIds[]=123&projectIds[]=456"

# Use appropriate time windows
curl "http://localhost:5000/api/v1/metrics/developers?windowDays=30" # Good
curl "http://localhost:5000/api/v1/metrics/developers?windowDays=365" # Heavy
```

### Response Handling

#### Check Schema Version
```javascript
const response = await fetch('/api/v1/metrics/developers');
const data = await response.json();

if (data.schemaVersion !== '1.0.0') {
  console.warn('API schema version mismatch, update client');
}
```

#### Handle Async Operations
```bash
# Example: Get developer metrics with pagination
PAGE=1
while true; do
  RESPONSE=$(curl -s "http://localhost:5000/api/v1/metrics/developers?page=$PAGE&pageSize=50")
  
  # Check if we have more data
  HAS_MORE=$(echo "$RESPONSE" | jq -r '.pagination.hasMore')
  
  # Process the data
  echo "$RESPONSE" | jq -r '.data[]'
  
  if [ "$HAS_MORE" = "false" ]; then
    break
  fi
  
  PAGE=$((PAGE + 1))
done
```

### Error Recovery

#### Retry Logic
```javascript
async function fetchWithRetry(url, options = {}, maxRetries = 3) {
  for (let attempt = 1; attempt <= maxRetries; attempt++) {
    try {
      const response = await fetch(url, options);
      
      if (response.ok) {
        return response;
      }
      
      if (response.status >= 400 && response.status < 500) {
        // Client error, don't retry
        throw new Error(`Client error: ${response.status}`);
      }
      
      if (attempt === maxRetries) {
        throw new Error(`Max retries exceeded: ${response.status}`);
      }
      
      // Exponential backoff
      await new Promise(resolve => setTimeout(resolve, 1000 * Math.pow(2, attempt - 1)));
      
    } catch (error) {
      if (attempt === maxRetries) {
        throw error;
      }
    }
  }
}
```

### Monitoring Integration

#### Health Check Integration
```bash
#!/bin/bash
# health-check.sh - Monitor API health

HEALTH=$(curl -s "http://localhost:5000/health" | jq -r .status)

if [ "$HEALTH" != "Healthy" ]; then
  echo "API health check failed: $HEALTH"
  exit 1
fi

echo "API is healthy"
```

#### API Monitoring
```bash
#!/bin/bash
# monitor-api.sh - Check API endpoint health

# Check catalog endpoint
CATALOG_STATUS=$(curl -s -w "%{http_code}" "http://localhost:5000/api/v1/catalog" -o /dev/null)

if [ "$CATALOG_STATUS" != "200" ]; then
  echo "Warning: API catalog endpoint returned $CATALOG_STATUS"
  # Send alert notification
fi

# Check developer metrics endpoint
METRICS_STATUS=$(curl -s -w "%{http_code}" "http://localhost:5000/api/v1/metrics/developers?pageSize=1" -o /dev/null)

if [ "$METRICS_STATUS" != "200" ]; then
  echo "Warning: Developer metrics endpoint returned $METRICS_STATUS"
  # Send alert notification
fi

echo "API endpoints are responding correctly"
```

This guide should be updated as the API evolves and new features are added.