# Toman Engineering Metrics (TEM) - GitLab Analytics

This project implements the GitLab-only baseline for Toman Engineering Metrics as specified in the PRD. It provides collection, storage, analysis, and export of engineering metrics from GitLab.

## Overview

The system implements a comprehensive GitLab metrics collection and analysis platform that covers:

- **Flow & Throughput Metrics**: MR cycle time, throughput, WIP tracking
- **CI/CD Health Metrics**: Pipeline success rates, mean time to green, deployment frequency
- **Git/GitOps Hygiene**: Direct pushes, approval bypasses, commit signing
- **Issue/Quality Signals**: SLA breaches, defect tracking

## Architecture

### Components

1. **GitLab API Service** (`IGitLabApiService`)
   - Handles API communication with GitLab
   - Implements rate limiting and retry logic
   - Discovers projects, collects MRs, commits, pipelines, and issues

2. **Collector Service** (`IGitLabCollectorService`)
   - Orchestrates data collection from GitLab
   - Supports incremental and backfill modes
   - Uses channels for concurrent processing

3. **Metrics Processor** (`IMetricsProcessorService`)
   - Transforms raw data into computed facts
   - Implements metric calculations per PRD specifications
   - Handles production deployment inference

4. **Export Service** (`IMetricsExportService`)
   - Generates JSON and CSV exports
   - Provides API endpoints for data access
   - Organizes data by business lines and teams

5. **Scheduler** (Quartz Jobs)
   - **Nightly Processing**: Full collection at 02:00 Europe/Amsterdam
   - **Incremental Collection**: Hourly updates at :15

### Data Model

The system uses PostgreSQL with the following key tables:

**Dimensions**
- `dim_project`: Project metadata
- `dim_user`: User information (with email hashing for PII protection)
- `dim_branch`: Branch metadata
- `dim_release`: Release information

**Raw Data**
- `raw_commit`: Commit details with statistics
- `raw_mr`: Merge request information
- `raw_pipeline`: Pipeline execution data
- `raw_job`: Individual job details
- `raw_issue`: Issue tracking data

**Computed Facts**
- `fact_mr`: Processed merge request metrics
- `fact_pipeline`: Pipeline analysis results
- `fact_git_hygiene`: Git hygiene metrics by day
- `fact_release`: Release cadence analysis

**Operational**
- `ingestion_state`: Tracking last run timestamps

## API Endpoints

### Health & Status
- `GET /healthz` - Service health check
- `GET /readyz` - Readiness check (includes DB connectivity)
- `GET /status` - Comprehensive status including coverage and lag metrics

### Run Controls
- `POST /runs/backfill?days={days}` - Trigger backfill collection
- `POST /runs/incremental` - Trigger incremental collection

### Data Export
- `GET /exports/daily/{yyyy-MM-dd}.json` - Daily metrics in JSON format
- `GET /exports/daily/{yyyy-MM-dd}.csv` - Daily metrics in CSV format

## Configuration

### Environment Variables
- `GITLAB_API_URL`: GitLab instance base URL
- `GITLAB_API_TOKEN`: GitLab API token with read permissions
- `ConnectionStrings__metricsdb`: PostgreSQL connection string

### appsettings.json
```json
{
  "GitLab": {
    "BaseUrl": "https://gitlab.example.com",
    "Token": "your-gitlab-token-here",
    "RootGroups": [
      "toman/core",
      "toman/corporate-services",
      "toman/exchange", 
      "toman/c-side"
    ]
  },
  "Processing": {
    "MaxDegreeOfParallelism": 8,
    "BackfillDays": 180
  },
  "Exports": {
    "Directory": "/data/exports"
  }
}
```

## Business Line Mapping

The system maps GitLab group structures to Toman's business lines:

- **Corporate Services**: `toman/corporate-services/*`
- **Exchange**: `toman/exchange/*`
- **C-Side**: `toman/c-side/*`
- **Platform/Core**: `toman/core/*`, `toman/platform/*`

## Key Metrics Implementation

### Flow & Throughput
- **MR Cycle Time**: `created_at` → `merged_at`
- **Time to First Review**: `created_at` → first reviewer comment
- **Throughput**: Count of merged MRs per period

### CI/CD Health
- **Pipeline Success Rate**: Successful vs total pipelines
- **Mean Time to Green**: Failed pipeline → next success on same ref
- **Deployment Frequency**: Production pipelines per week

### Production Deployment Inference
A pipeline is considered production if:
- Pipeline on default branch, OR
- Environment contains "production", OR  
- Associated with a release tag

### Git/GitOps Hygiene
- **Direct Pushes**: Non-merge commits on default branch
- **Approval Bypass**: Merged with fewer approvals than required
- **Signed Commit Ratio**: GPG/SSH signed commits percentage

## Security & Privacy

- **Email Protection**: User emails are SHA-256 hashed
- **API Security**: GitLab PAT stored securely
- **PII Minimization**: Only necessary data is stored

## Performance Targets

- **Coverage**: ≥95% of active projects (MR in last 90 days)
- **Freshness**: Daily aggregates by 03:00 Europe/Amsterdam
- **Accuracy**: ≥98% parity vs GitLab UI counts
- **Performance**: ≤60 min nightly run for ≤300 projects

## Running the Application

### Development
```bash
# Start with Aspire
aspire run

# Or run API service directly
dotnet run --project src/Toman.Management.KPIAnalysis.ApiService
```

### Production
The application is designed to run in Kubernetes with:
- PostgreSQL database
- Persistent volume for exports
- Secrets for GitLab tokens
- ConfigMaps for configuration

## Monitoring & Observability

The application includes:
- **OpenTelemetry**: Distributed tracing for API calls
- **Structured Logging**: JSON logs with correlation IDs
- **Health Checks**: Kubernetes readiness/liveness probes
- **Metrics**: Self-reporting on collection stats and performance

## Future Phases

This V1 implementation provides the foundation for:
- **V2**: SonarQube integration for code quality metrics
- **V3**: Prometheus/Grafana integration for observability metrics
- **V4**: Advanced analytics and ML-based insights

## Support

For issues, feature requests, or questions about metrics definitions, refer to the full PRD documentation or contact the VP of Engineering team.
