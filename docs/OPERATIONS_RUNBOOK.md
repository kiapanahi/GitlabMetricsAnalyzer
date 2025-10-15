# ⚠️ DOCUMENT UNDER REVISION ⚠️

**This document contains outdated information about database operations and data collection workflows that were never implemented.**

**Current Architecture**: The application calculates metrics via **live GitLab API calls** with no data storage. See [CURRENT_STATE.md](CURRENT_STATE.md) for accurate architecture.

**Sections marked [OBSOLETE]** describe features that do not exist in the current implementation.

---

# Operations Runbook

This document provides operational guidance for managing the GitLab Metrics Analyzer system in production.

## Table of Contents
- [System Overview](#system-overview)
- [Manual Operations](#manual-operations)
- [Monitoring Guide](#monitoring-guide)
- [Data Quality Review](#data-quality-review)
- [Export Management](#export-management)
- [Troubleshooting](#troubleshooting)
- [Maintenance Procedures](#maintenance-procedures)

## System Overview

The GitLab Metrics Analyzer is a **live metrics API** that:
- ✅ Calculates developer productivity metrics from GitLab API in real-time
- ❌ ~~Stores data in PostgreSQL~~ **[OBSOLETE - No database]**
- ✅ Provides REST APIs for metrics consumption (10 endpoints)
- ❌ ~~Generates exportable reports~~ **[OBSOLETE - No exports feature]**

**Key Characteristics:**
- ✅ On-demand calculation (request → GitLab API → calculate → respond)
- ❌ ~~Manual trigger workflow~~ **[OBSOLETE - No collection triggers]**
- ❌ ~~Incremental collection with windowing~~ **[OBSOLETE - No data collection]**
- ✅ Built-in resilience (Polly: retry, circuit breaker, timeout)
- ✅ Comprehensive logging and monitoring (Serilog, OpenTelemetry)

## ❌ [OBSOLETE] Manual Operations

**This entire section describes data collection operations that were never implemented.**

The current system has **no manual operations** - it's a stateless API that responds to HTTP requests.

**For current operations**, see:
- API endpoint usage: [ENDPOINT_AUDIT.md](ENDPOINT_AUDIT.md)
- Configuration: [CONFIGURATION_REVIEW.md](CONFIGURATION_REVIEW.md)

---

<details>
<summary>Click to expand obsolete content (for historical reference)</summary>

### Daily Operations

#### 1. Trigger Incremental Collection
Run this daily to collect new/updated data:

```bash
# Start incremental collection
curl -X POST "http://localhost:5000/gitlab-metrics/collect/incremental" \
  -H "Content-Type: application/json" \
  -d '{
    "triggerSource": "daily-ops",
    "runType": "incremental"
  }'
```

Expected response:
```json
{
  "runId": "550e8400-e29b-41d4-a716-446655440000",
  "status": "Running",
  "runType": "incremental",
  "startedAt": "2024-01-15T10:30:00Z",
  "message": "Collection run started successfully"
}
```

#### 2. Monitor Collection Status
Check the status of running collections:

```bash
# Get specific run status
curl "http://localhost:5000/gitlab-metrics/collect/runs/550e8400-e29b-41d4-a716-446655440000"

# List recent runs (last 10)
curl "http://localhost:5000/gitlab-metrics/collect/runs?limit=10"
```

#### 3. Verify Data Quality
Check data quality after each collection:

```bash
curl "http://localhost:5000/api/data-quality/reports"
```

### Weekly Operations

#### 1. Review System Health
```bash
# Check application health
curl "http://localhost:5000/health"

# Check GitLab connectivity
curl "http://localhost:5000/health" | jq '.gitlabApi'

# Review recent collection success rates
curl "http://localhost:5000/gitlab-metrics/collect/runs?limit=50" | \
  jq '[.[] | select(.status == "Completed")] | length'
```

#### 2. Generate Weekly Reports
```bash
# Export comprehensive developer metrics for the week
curl "http://localhost:5000/api/exports/developers?windowDays=7&format=excel" \
  -o "weekly_metrics_$(date +%Y%m%d).xlsx"
```

### Monthly Operations

#### 1. Full Backfill (if needed)
Run monthly or when data gaps are detected:

```bash
curl -X POST "http://localhost:5000/gitlab-metrics/collect/backfill" \
  -H "Content-Type: application/json" \
  -d '{
    "triggerSource": "monthly-backfill",
    "runType": "backfill"
  }'
```

#### 2. Archive Old Exports
Clean up export directory:

```bash
# Archive exports older than 90 days
find /data/exports -name "*.json" -mtime +90 -exec mv {} /data/archives/ \;
```

## Monitoring Guide

### Key Performance Indicators (KPIs)

#### Collection Health
- **Collection Success Rate**: > 95% over 7 days
- **Data Freshness**: Last successful collection < 24 hours
- **Average Collection Duration**: Incremental < 10 minutes, Backfill < 60 minutes

#### API Performance  
- **Response Time**: P95 < 2 seconds for metrics endpoints
- **Error Rate**: < 1% over 24 hours
- **Throughput**: Handle concurrent requests without degradation

#### Data Quality
- **Completeness Score**: > 90% for active developers
- **Consistency Score**: > 95% across time periods
- **Accuracy Score**: > 98% when spot-checked against GitLab

### Monitoring Endpoints

#### Health Checks
```bash
# Application readiness
GET /health
# Response: {"status": "Healthy", "gitlabApi": "Healthy", "database": "Healthy"}

# Application liveness
GET /alive
# Response: {"status": "Healthy"}
```

#### Collection Status
```bash
# Recent collection runs
GET /gitlab-metrics/collect/runs?limit=10

# Specific run details
GET /gitlab-metrics/collect/runs/{runId}
```

#### Data Quality Reports
```bash
# Latest data quality assessment
GET /api/data-quality/reports

# Historical quality trends
GET /api/data-quality/reports/trends?days=30
```

### Alert Thresholds

#### Critical Alerts (Page Immediately)
- Application health check fails for > 5 minutes
- No successful collection in > 48 hours
- Database connectivity lost
- GitLab API authentication failures

#### Warning Alerts (Review During Business Hours)
- Collection duration > 2x baseline average
- Data quality score drops below 90%
- Export generation failures
- High memory usage (> 80% for > 30 minutes)

### Log Analysis

#### Key Log Patterns
```bash
# Collection run status
grep "CollectionRun" /var/logs/app.log | jq '.status'

# API errors
grep "ERROR" /var/logs/app.log | grep -E "(API|HTTP)"

# Performance issues
grep "WARN.*slow" /var/logs/app.log
```

#### Structured Log Fields
- `TraceId`: Distributed tracing correlation
- `UserId`: For user-specific operations
- `CollectionRunId`: For data collection operations
- `Duration`: Operation timing
- `ErrorCode`: Standardized error classification

## Data Quality Review

### Automated Quality Checks

#### Data Completeness
- **Developer Coverage**: All active GitLab users represented
- **Time Coverage**: No missing days in date ranges
- **Project Coverage**: All accessible projects included

#### Data Consistency  
- **Metric Totals**: Aggregate sums match detail records
- **Time Alignment**: Timestamps consistent across related records
- **Reference Integrity**: All foreign keys resolve correctly

#### Data Accuracy
- **Spot Checks**: Random sampling against GitLab API
- **Business Rules**: Logical validation (e.g., no negative metrics)
- **Trend Analysis**: Sudden changes flagged for review

### Manual Quality Review Process

#### Weekly Quality Review (15 minutes)
1. Check data quality dashboard: `GET /api/data-quality/reports`
2. Review any flagged anomalies or completeness issues
3. Validate top 5 most active developers against GitLab
4. Confirm recent collection runs completed successfully

#### Monthly Deep Review (60 minutes)
1. Generate comprehensive quality report for the month
2. Cross-reference metrics with business stakeholder feedback
3. Validate new developer onboarding detection
4. Review and update bot detection patterns if needed
5. Analyze trends in data quality scores

### Quality Issue Resolution

#### Common Issues and Resolutions

**Missing Developer Data**:
```bash
# Check developer aliases mapping
GET /api/v1/developers/{id}/aliases

# Trigger targeted re-collection
POST /gitlab-metrics/collect/incremental
{"projectIds": [123, 456], "triggerSource": "quality-fix"}
```

**Inconsistent Metrics**:
```bash
# Re-calculate aggregates for specific period
POST /api/metrics/recalculate
{"fromDate": "2024-01-01", "toDate": "2024-01-31"}
```

**API Rate Limiting**:
- Check GitLab rate limit headers in logs
- Adjust collection parallelism in configuration
- Implement exponential backoff in collection logic

## Export Management

### Export Operations

#### Generate Developer Metrics Export
```bash
# JSON format (API consumption)
curl "http://localhost:5000/api/exports/developers?windowDays=30&format=json" \
  -o "dev_metrics_$(date +%Y%m%d).json"

# Excel format (stakeholder reporting)  
curl "http://localhost:5000/api/exports/developers?windowDays=90&format=excel" \
  -o "quarterly_metrics_$(date +%Y%m%d).xlsx"

# CSV format (data analysis)
curl "http://localhost:5000/api/exports/developers?windowDays=7&format=csv" \
  -o "weekly_metrics_$(date +%Y%m%d).csv"
```

#### Export Status Monitoring
```bash
# Check export generation status
GET /api/exports/status

# Download completed export
GET /api/exports/runs/{runId}/download
```

### Export Configuration

#### Export Directory Setup
```json
{
  "Exports": {
    "Directory": "/data/exports",
    "RetentionDays": 90,
    "MaxFileSizeMB": 100
  }
}
```

#### Export Formats Available
- **JSON**: Structured data with full metadata
- **CSV**: Tabular format, suitable for Excel import
- **Excel**: Multi-sheet workbook with charts and summaries

### Export Automation

#### Scheduled Export Generation
```bash
#!/bin/bash
# weekly-export.sh - Run weekly to generate stakeholder reports

DATE=$(date +%Y%m%d)
EXPORT_DIR="/data/exports/scheduled"

# Generate weekly executive summary
curl -s "http://localhost:5000/api/exports/developers?windowDays=7&format=excel" \
  -o "$EXPORT_DIR/weekly_summary_$DATE.xlsx"

# Generate monthly trend analysis  
curl -s "http://localhost:5000/api/exports/developers?windowDays=30&format=json" \
  -o "$EXPORT_DIR/monthly_trends_$DATE.json"

# Notify stakeholders
echo "Weekly metrics export completed: $DATE" | mail -s "Metrics Report Ready" stakeholders@company.com
```

## Troubleshooting

### Common Issues

#### Collection Runs Failing

**Symptoms:**
- Collection runs show "Failed" status
- Error messages in logs about GitLab API

**Diagnosis:**
```bash
# Check recent failed runs
curl "http://localhost:5000/gitlab-metrics/collect/runs?limit=20" | jq '.[] | select(.status == "Failed")'

# Review error details
curl "http://localhost:5000/gitlab-metrics/collect/runs/{failedRunId}" | jq '.errorMessage'
```

**Resolution:**
1. Verify GitLab token permissions and expiration
2. Check network connectivity to GitLab instance
3. Review rate limiting in GitLab API headers
4. Restart collection with reduced parallelism

#### Slow API Response Times

**Symptoms:**
- API endpoints taking > 5 seconds to respond
- Timeout errors from client applications

**Diagnosis:**
```bash
# Check database query performance
grep "slow query" /var/logs/postgresql.log

# Review API endpoint timing
grep "HTTP.*[5-9][0-9][0-9][0-9]ms" /var/logs/app.log
```

**Resolution:**
1. Check database index usage and performance
2. Review query plans for slow endpoints
3. Consider adding database partitioning
4. Implement response caching for frequently accessed data

#### Missing Developer Data

**Symptoms:**
- Developers not appearing in metrics
- Zero metrics for active developers

**Diagnosis:**
```bash
# Check developer identity mapping
GET /api/v1/developers | jq '.[] | select(.isActive == false)'

# Review identity configuration
grep "BotRegexPatterns" appsettings.json
```

**Resolution:**
1. Update developer aliases mapping
2. Review bot detection patterns
3. Re-run collection for affected time periods
4. Verify GitLab project access permissions

### Emergency Procedures

#### System Outage Response
1. **Check Health Endpoints**: Verify application and dependency status
2. **Review Recent Changes**: Check deployment logs and configuration changes
3. **Check Resource Usage**: Monitor CPU, memory, and disk usage
4. **Database Connectivity**: Verify PostgreSQL connection and query performance
5. **GitLab API Status**: Check GitLab instance availability and API limits

#### Data Corruption Recovery
1. **Stop Collection**: Halt all running collection processes
2. **Backup Database**: Create point-in-time backup before recovery
3. **Identify Scope**: Determine affected time range and entities
4. **Restore from Backup**: If recent backup available, restore affected data
5. **Re-collect Data**: Trigger backfill collection for affected period
6. **Validate Recovery**: Run data quality checks on restored data

## Maintenance Procedures

### Database Maintenance

#### Weekly Tasks
```sql
-- Update table statistics
ANALYZE;

-- Check partition health
SELECT schemaname, tablename, pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename)) 
FROM pg_tables 
WHERE tablename LIKE '%_facts_%';
```

#### Monthly Tasks
```sql
-- Create new monthly partitions (3 months ahead)
CREATE TABLE commit_facts_2024_04 PARTITION OF commit_facts
    FOR VALUES FROM ('2024-04-01') TO ('2024-05-01');

-- Drop old partitions (keep 2 years)
DROP TABLE IF EXISTS commit_facts_2022_01;
```

### Application Maintenance

#### Configuration Updates
```bash
# Backup current configuration
cp appsettings.json appsettings.json.backup.$(date +%Y%m%d)

# Update bot detection patterns
jq '.Metrics.Identity.BotRegexPatterns += ["^newbot.*"]' appsettings.json > appsettings.json.tmp
mv appsettings.json.tmp appsettings.json

# Restart application to pick up changes
systemctl restart gitlab-metrics-analyzer
```

#### Log Rotation
```bash
# Configure logrotate for application logs
cat > /etc/logrotate.d/gitlab-metrics << EOF
/var/logs/gitlab-metrics/*.log {
    daily
    rotate 30
    compress
    delaycompress
    missingok
    create 644 app app
    postrotate
        systemctl reload gitlab-metrics-analyzer
    endscript
}
EOF
```

### Performance Tuning

#### Database Optimization
```sql
-- Monitor query performance
SELECT query, mean_exec_time, calls 
FROM pg_stat_statements 
WHERE query LIKE '%commit_facts%'
ORDER BY mean_exec_time DESC 
LIMIT 10;

-- Add missing indexes based on query patterns
CREATE INDEX CONCURRENTLY idx_commit_facts_developer_date 
ON commit_facts(developer_id, committed_at) 
WHERE committed_at >= '2024-01-01';
```

#### Application Tuning
```json
{
  "Processing": {
    "MaxDegreeOfParallelism": 4,  // Adjust based on GitLab API limits
    "BackfillDays": 90           // Reduce for faster backfill
  }
}
```

This runbook should be reviewed and updated quarterly to reflect operational experience and system evolution.