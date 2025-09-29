# GitLab Data Re-seeding and Re-hydration Guide

This document explains how to re-seed or re-hydrate the raw data fetched from GitLab when the database is out of sync with GitLab's actual data.

## Overview

The GitLab Metrics Analyzer provides several mechanisms to refresh and resynchronize data:

1. **Incremental Collection** - Updates data since last collection
2. **Backfill Collection** - Collects historical data for specific date ranges
3. **Data Reset** - Clears all raw data for complete re-seeding
4. **Incremental State Reset** - Forces re-collection of recent data

## Available Endpoints

### 1. Incremental Collection

Fetches new/updated data since the last successful collection:

```bash
curl -X POST "http://localhost:5000/gitlab-metrics/collect/incremental" \
  -H "Content-Type: application/json" \
  -d '{
    "triggerSource": "manual",
    "windowSizeHours": 24
  }'
```

**Parameters:**
- `triggerSource` (optional): Source identifier (e.g., "manual", "scheduled")
- `windowSizeHours` (optional): Time window in hours for collection

### 2. Backfill Collection

Performs complete data collection for a specified date range:

```bash
curl -X POST "http://localhost:5000/gitlab-metrics/collect/backfill" \
  -H "Content-Type: application/json" \
  -d '{
    "triggerSource": "manual",
    "backfillStartDate": "2024-01-01T00:00:00Z",
    "backfillEndDate": "2024-12-31T23:59:59Z"
  }'
```

**Parameters:**
- `triggerSource` (optional): Source identifier
- `backfillStartDate` (optional): Start date for collection (defaults to beginning of time)
- `backfillEndDate` (optional): End date for collection (defaults to now)

### 3. Reset All Raw Data (⚠️ Destructive)

Clears all raw data tables and resets ingestion state for complete re-seeding:

```bash
curl -X POST "http://localhost:5000/gitlab-metrics/collect/reset" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -d '{
    "triggerSource": "maintenance",
    "confirmReset": true
  }'
```

**⚠️ Warning:** This operation is destructive and requires authorization.

### 4. Reset Incremental State

Resets the incremental collection timestamp to force re-collection of recent data:

```bash
curl -X POST "http://localhost:5000/gitlab-metrics/collect/reset-incremental-state" \
  -H "Content-Type: application/json" \
  -d '{
    "triggerSource": "manual"
  }'
```

### 5. Monitor Collection Status

Check the status of collection operations:

```bash
# Get specific run status
curl "http://localhost:5000/gitlab-metrics/collect/runs/{runId}"

# List recent runs
curl "http://localhost:5000/gitlab-metrics/collect/runs?limit=10&runType=incremental"
```

## Common Re-seeding Scenarios

### Scenario 1: Database is Slightly Out of Date

**Solution:** Use incremental collection
```bash
curl -X POST "http://localhost:5000/gitlab-metrics/collect/incremental" \
  -H "Content-Type: application/json" \
  -d '{"triggerSource": "sync-fix"}'
```

### Scenario 2: Database is Missing Historical Data

**Solution:** Use backfill collection for specific date range
```bash
curl -X POST "http://localhost:5000/gitlab-metrics/collect/backfill" \
  -H "Content-Type: application/json" \
  -d '{
    "triggerSource": "historical-sync",
    "backfillStartDate": "2024-06-01T00:00:00Z",
    "backfillEndDate": "2024-08-31T23:59:59Z"
  }'
```

### Scenario 3: Complete Data Corruption or Schema Changes

**Solution:** Full reset and re-seed
```bash
# Step 1: Reset all data (requires authorization)
curl -X POST "http://localhost:5000/gitlab-metrics/collect/reset" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -d '{"triggerSource": "corruption-fix", "confirmReset": true}'

# Step 2: Backfill all historical data
curl -X POST "http://localhost:5000/gitlab-metrics/collect/backfill" \
  -H "Content-Type: application/json" \
  -d '{
    "triggerSource": "full-reseed",
    "backfillStartDate": "2024-01-01T00:00:00Z"
  }'
```

### Scenario 4: Incremental Collection Stuck or Missing Recent Data

**Solution:** Reset incremental state and re-run
```bash
# Step 1: Reset incremental state
curl -X POST "http://localhost:5000/gitlab-metrics/collect/reset-incremental-state" \
  -H "Content-Type: application/json" \
  -d '{"triggerSource": "incremental-fix"}'

# Step 2: Run incremental collection
curl -X POST "http://localhost:5000/gitlab-metrics/collect/incremental" \
  -H "Content-Type: application/json" \
  -d '{"triggerSource": "incremental-fix", "windowSizeHours": 168}'
```

## Data Flow and Processing

1. **Raw Data Collection** → GitLab API data is stored in `raw_*` tables
2. **Data Enrichment** → Raw data is processed and enriched with additional context
3. **Fact Generation** → Enriched data is transformed into fact tables
4. **Metrics Calculation** → Facts are aggregated into developer metrics
5. **Export/API** → Processed metrics are available via API endpoints

## Monitoring and Troubleshooting

### Check Collection Run Status

```bash
# Get recent failed runs
curl "http://localhost:5000/gitlab-metrics/collect/runs?limit=20" | \
  jq '.[] | select(.status == "Failed")'

# Get error details for a specific run
curl "http://localhost:5000/gitlab-metrics/collect/runs/{runId}" | \
  jq '.errorMessage'
```

### Data Quality Checks

```bash
# Check data quality after collection
curl "http://localhost:5000/data-quality/reports"
```

### Collection Logs

Check application logs for detailed collection information:
- Collection progress and statistics
- GitLab API rate limiting
- Data validation errors
- Network connectivity issues

## Performance Considerations

- **Incremental Collection**: Typically completes in minutes
- **Backfill Collection**: Can take hours depending on date range and data volume
- **Full Reset**: Immediate, but requires subsequent backfill
- **API Rate Limits**: GitLab API limits may affect collection speed

## Security and Authorization

- **Reset operations** require proper authorization
- **Collection operations** use configured GitLab API tokens
- **Audit logging** tracks all collection and reset operations

## Advanced Configuration

Collection behavior can be configured via `appsettings.json`:

```json
{
  "Collection": {
    "MaxParallelProjects": 5,
    "RetryDelayMs": 1000,
    "MaxRetries": 3,
    "CollectReviewEvents": true,
    "CollectCommitStats": true
  }
}
```

For more detailed configuration options, see [CONFIGURATION_GUIDE.md](./CONFIGURATION_GUIDE.md).