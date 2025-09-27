# API v1 Developer Metrics Endpoints

## Overview

The v1 API provides versioned, stable endpoints for accessing developer metrics with enhanced filtering, pagination, and schema versioning support.

## Authentication & Versioning

All v1 API endpoints require proper versioning headers:

### Required Headers

- `X-Api-Version: 1.0.0` (preferred)
- OR `Accept: application/json;version=1.0.0`

### Error Responses

Missing or invalid version headers will return:

```json
{
  "error": "API_VERSION_REQUIRED",
  "message": "API version must be specified for v1 endpoints",
  "supportedVersions": ["1.0.0"]
}
```

## Endpoints

### GET /api/v1/metrics/developers

Returns paginated list of all developers with their latest metrics.

**Query Parameters:**
- `windowDays` (optional): Metrics calculation window in days (default: 30)
- `projectIds` (optional): Array of project IDs to filter by (e.g., `?projectIds=1,2,3`)
- `page` (optional): Page number starting from 1 (default: 1)
- `pageSize` (optional): Items per page, max 100 (default: 20)

**Response Schema:**
```json
{
  "schemaVersion": "1.0.0",
  "data": [
    {
      "schemaVersion": "1.0.0",
      "developerId": 123,
      "developerName": "John Doe",
      "developerEmail": "john@example.com",
      "computationDate": "2024-01-15T10:30:00Z",
      "windowStart": "2023-12-16T10:30:00Z",
      "windowEnd": "2024-01-15T10:30:00Z",
      "windowDays": 30,
      "metrics": { ... },
      "audit": { ... }
    }
  ],
  "pagination": {
    "page": 1,
    "pageSize": 20,
    "totalCount": 150,
    "totalPages": 8
  },
  "filterApplied": {
    "windowDays": 30,
    "projectIds": [1, 2, 3],
    "windowEnd": "2024-01-15T10:30:00Z"
  }
}
```

### GET /api/v1/metrics/developers/{developer_id}

Returns detailed metrics for a specific developer with optional sparkline data.

**Path Parameters:**
- `developer_id`: Long integer developer ID

**Query Parameters:**
- `windowDays` (optional): Metrics calculation window in days (default: 30)
- `projectIds` (optional): Array of project IDs to filter by
- `includeSparkline` (optional): Include historical sparkline data (default: true)

**Response Schema:**
```json
{
  "schemaVersion": "1.0.0",
  "data": {
    "schemaVersion": "1.0.0",
    "developerId": 123,
    "developerName": "John Doe",
    "developerEmail": "john@example.com",
    "computationDate": "2024-01-15T10:30:00Z",
    "windowStart": "2023-12-16T10:30:00Z",
    "windowEnd": "2024-01-15T10:30:00Z",
    "windowDays": 30,
    "metrics": { ... },
    "audit": { ... }
  },
  "sparklineData": [
    {
      "date": "2024-01-14T00:00:00Z",
      "value": 15.5,
      "metricName": "commits_count"
    }
  ],
  "filterApplied": {
    "windowDays": 30,
    "projectIds": [],
    "windowEnd": "2024-01-15T10:30:00Z"
  }
}
```

### GET /api/v1/catalog

Returns the complete metric catalog with definitions and schema version.

**Response Schema:**
```json
{
  "schemaVersion": "1.0.0",
  "data": {
    "version": "1.0.0",
    "generatedAt": "2024-01-15T10:30:00Z",
    "description": "GitLab Developer Productivity Metrics Catalog",
    "metrics": [
      {
        "name": "commits_count",
        "displayName": "Total Commits",
        "description": "Number of commits made by the developer",
        "dataType": "integer",
        "unit": "count",
        "category": "productivity",
        "isNullable": false,
        "metadata": { ... }
      }
    ]
  }
}
```

## Error Handling

All endpoints follow consistent error response format:

```json
{
  "error": "ERROR_CODE",
  "message": "Human-readable error message",
  "details": "Additional context"
}
```

Common error codes:
- `API_VERSION_REQUIRED`: Missing version header
- `INVALID_API_VERSION`: Unsupported version
- `DEVELOPER_NOT_FOUND`: Developer ID not found
- `VALIDATION_ERROR`: Invalid query parameters

## Migration from Legacy APIs

**From `/gitlab-metrics/metrics/developer/{userId}`:**
- Use `/api/v1/metrics/developers/{developer_id}` with required version header
- Add `includeSparkline=false` if sparkline data not needed

**From `/api/users/{userId}/metrics`:**
- Use `/api/v1/metrics/developers/{developer_id}` with required version header
- Similar response structure but with enhanced schema versioning

## Examples

### Get all developers with pagination
```bash
curl -H "X-Api-Version: 1.0.0" \
     "http://localhost:5000/api/v1/metrics/developers?page=1&pageSize=10"
```

### Get specific developer with project filtering
```bash
curl -H "X-Api-Version: 1.0.0" \
     "http://localhost:5000/api/v1/metrics/developers/123?projectIds=1,2,3&windowDays=60"
```

### Get metric catalog
```bash
curl -H "X-Api-Version: 1.0.0" \
     "http://localhost:5000/api/v1/catalog"
```