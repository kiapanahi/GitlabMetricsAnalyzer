# GitLab Metrics Analyzer

A .NET 9 application that collects developer productivity metrics from GitLab and stores them in PostgreSQL for analysis. Built with .NET Aspire for cloud-native development.

## Features

- **Data Collection**: Automatically collects commits, merge requests, and pipeline data from GitLab API
- **Incremental Sync**: Only fetches new data since last collection to optimize performance
- **Resilient Design**: Built-in retry logic and error handling for API failures
- **Analytics**: Calculates developer productivity metrics like commit frequency, merge request cycle time, and pipeline success rates
- **Real-time Monitoring**: Comprehensive logging and telemetry with Serilog

## Architecture

- **Tech Stack**: .NET 9, Entity Framework Core, PostgreSQL, .NET Aspire
- **Design Pattern**: Vertical slice architecture for each feature
- **API Integration**: NGitLab client for GitLab API interaction
- **Data Collection**: Manual trigger workflows for on-demand collection
- **Resilience**: Built-in retry policies and error handling for API failures
- **Export System**: Configurable metrics export with multiple formats

## Getting Started

### Prerequisites

- .NET 9 SDK
- PostgreSQL database
- GitLab instance with API access
- Visual Studio 2022 or VS Code with C# extensions

### Configuration

1. **GitLab API Setup**:
   - Create a GitLab Personal Access Token with `api` scope
   - Note your GitLab instance URL

2. **Database Setup**:
   - Ensure PostgreSQL is running
   - Create a database for the application

3. **Application Configuration**:
   Update `appsettings.json`:
   ```json
   {
     "GitLab": {
       "BaseUrl": "https://your-gitlab-instance.com",
       "Token": "your-token-here"
     },
     "ConnectionStrings": {
       "DefaultConnection": "Host=localhost;Database=GitLabMetrics;Username=your-user;Password=your-password"
     },
     "Processing": {
       "MaxDegreeOfParallelism": 8,
       "BackfillDays": 180
     },
     "Exports": {
       "Directory": "/data/exports"
     },
     "Metrics": {
       "Identity": {
         "BotRegexPatterns": [
           "^.*bot$",
           "^.*\\[bot\\]$",
           "^gitlab-ci$",
           "^dependabot.*"
         ]
       },
       "Excludes": {
         "CommitPatterns": [
           "^Merge branch.*",
           "^Merge pull request.*"
         ],
         "BranchPatterns": [
           "^dependabot/.*"
         ],
         "FilePatterns": [
           "^.*\\.min\\.(js|css)$",
           "^.*\\.(png|jpg|jpeg|gif|svg|ico)$"
         ]
       }
     }
   }
   ```

### Running the Application

#### Option 1: .NET Aspire (Recommended)
```bash
aspire run
```

#### Option 2: Direct Run
```bash
cd src/Toman.Management.KPIAnalysis.ApiService
dotnet run
```

### Database Migration

The application will automatically apply migrations on startup. To manually run migrations:

```bash
cd src/Toman.Management.KPIAnalysis.ApiService
dotnet ef database update
```

## Manual Data Collection

The system operates through manual trigger workflows instead of automatic scheduling:

### Incremental Collection
Collect only new/updated data since the last collection:

```bash
curl -X POST "http://localhost:5000/gitlab-metrics/collect/incremental" \
  -H "Content-Type: application/json" \
  -d '{"triggerSource": "manual"}'
```

### Backfill Collection
Perform a complete data backfill (last 180 days by default):

```bash
curl -X POST "http://localhost:5000/gitlab-metrics/collect/backfill" \
  -H "Content-Type: application/json" \
  -d '{"triggerSource": "manual"}'
```

### Monitor Collection Status
Check the status of a running collection:

```bash
# Get specific run status
curl "http://localhost:5000/gitlab-metrics/collect/runs/{runId}"

# List recent runs
curl "http://localhost:5000/gitlab-metrics/collect/runs?limit=10"
```

## API Endpoints

### Health & Status
- `GET /health` - Application health status
- `GET /alive` - Liveness check

### Data Collection (Manual Triggers)
- `POST /gitlab-metrics/collect/incremental` - Run incremental collection
- `POST /gitlab-metrics/collect/backfill` - Run full backfill collection
- `GET /gitlab-metrics/collect/runs/{runId}` - Check collection run status
- `GET /gitlab-metrics/collect/runs` - List recent collection runs

### Developer Metrics API (v1)
- `GET /api/v1/metrics/developers` - Paginated developer metrics with filtering
- `GET /api/v1/metrics/developers/{id}` - Individual developer metrics with history
- `GET /api/v1/catalog` - Available metrics catalog with schema version

### Data Quality & Exports
- `GET /api/data-quality/reports` - Data quality assessment reports
- `GET /api/exports/developers` - Export developer metrics to various formats
- `GET /api/exports/runs/{runId}/download` - Download specific export run results

### Legacy Endpoints (Deprecated)
- `GET /api/users/{userId}/metrics` - Legacy user metrics endpoint

## Data Models

### Core Entities (PRD Aligned)
- **Developers**: Central developer identity with aliases support
- **Projects**: GitLab project information
- **CommitFacts**: Individual commit data with metrics
- **MergeRequestFacts**: MR lifecycle data with timelines
- **PipelineFacts**: CI/CD pipeline execution data
- **ReviewEvents**: Code review activity tracking
- **DeveloperMetricsAggregates**: Pre-calculated time-series metrics

### Calculated Metrics
- **Developer Metrics**: Commit frequency, review participation, pipeline success
- **Collaboration Metrics**: Review patterns, knowledge sharing indicators
- **Quality Metrics**: Pipeline success rates, code revert patterns
- **Productivity Metrics**: Velocity scores, efficiency indicators

## Data Export System

### Export Developer Metrics
Generate comprehensive developer metrics export:

```bash
curl "http://localhost:5000/api/exports/developers?windowDays=90&format=json" \
  -H "Accept: application/json" \
  -o developer_metrics.json
```

### Available Export Formats
- **JSON**: Structured data for API consumption
- **CSV**: Tabular format for spreadsheet analysis  
- **Excel**: Multi-sheet workbooks with charts and summaries

### Export Configuration
Configure export settings in `appsettings.json`:
```json
{
  "Exports": {
    "Directory": "/data/exports"
  }
}
```

## Development

### Project Structure
```
src/
├── Toman.Management.KPIAnalysis.ApiService/     # Main application
│   ├── Features/GitLabMetrics/                  # GitLab integration feature
│   │   ├── Data/                               # EF Core models and context
│   │   ├── Infrastructure/                     # External service clients
│   │   └── Services/                          # Business logic services
│   └── Configuration/                          # App configuration
├── Toman.Management.KPIAnalysis.AppHost/       # Aspire orchestration
└── Toman.Management.KPIAnalysis.ServiceDefaults/ # Shared service configuration
```

### Key Services
- **IGitLabService**: GitLab API client with health checks
- **IMetricsCalculationService**: Calculates productivity metrics from raw data
- **GitLabMetricsDbContext**: EF Core database context with automatic migrations

### Building
```bash
dotnet build
```

### Testing
```bash
dotnet test
```

## Monitoring

The application includes comprehensive observability:

### Health Checks
- **Application Health**: `GET /health` - Overall system health
- **Liveness Check**: `GET /alive` - Container liveness probe
- **GitLab Connectivity**: Automatic GitLab API health validation
- **Database Connectivity**: PostgreSQL connection health

### Data Quality Monitoring
Monitor data quality and collection health:

```bash
# Get data quality reports
curl "http://localhost:5000/api/data-quality/reports"

# Check recent collection runs
curl "http://localhost:5000/gitlab-metrics/collect/runs"
```

### Logging & Telemetry
- **Structured Logging**: JSON-formatted logs with correlation IDs
- **OpenTelemetry**: Distributed tracing and metrics collection
- **Performance Metrics**: Collection timing and throughput statistics
- **Error Tracking**: Detailed error reporting with context

### Key Metrics to Monitor
- Collection run success rates
- Data freshness (last successful collection time)
- API rate limit consumption
- Database query performance
- Export generation success rates

## Deployment

The application is designed for containerized deployment with .NET Aspire. It includes:
- Service discovery and configuration
- Distributed tracing
- Health checks
- Graceful shutdown handling

### Container Deployment
```bash
# Build and run with Docker
docker build -t gitlab-metrics-analyzer .
docker run -p 5000:8080 -e ConnectionStrings__DefaultConnection="..." gitlab-metrics-analyzer
```

### Kubernetes Deployment
The application includes health check endpoints suitable for Kubernetes:
- Readiness probe: `GET /health`
- Liveness probe: `GET /alive`

## Future Roadmap (vNext)

### Planned Scheduling Improvements
The current v1 system uses manual triggers. Future versions will include:

- **Hangfire Integration**: Automated background job scheduling
- **Aspire Scheduling**: Cloud-native job orchestration
- **Configurable Schedules**: Flexible collection timing (daily, weekly, custom)
- **Smart Incremental Windows**: Dynamic window sizing based on activity patterns
- **Rate Limit Optimization**: Intelligent throttling based on GitLab API limits

### Advanced Features in Development
- **Team Metrics Aggregation**: Cross-developer collaboration analysis
- **Project Health Scoring**: Automated project quality assessments  
- **Anomaly Detection**: Statistical outlier identification
- **Custom Dashboards**: Interactive visualization components
- **API Rate Limiting**: Built-in rate limiting for external consumers

## Troubleshooting

### Common Issues

1. **GitLab API Connection Failures**:
   - Verify token permissions (`api` scope required)
   - Check network connectivity to GitLab instance
   - Review rate limiting settings

2. **Database Connection Issues**:
   - Ensure PostgreSQL is running
   - Verify connection string format
   - Check database permissions

3. **Missing Data**:
   - Verify project access permissions in GitLab
   - Check incremental sync date ranges
   - Review application logs for API errors

### Logging

Logs are structured and include:
- Request/response details for GitLab API calls
- Database operation timing
- Error details with correlation IDs
- Performance metrics

View logs in the application output or configure external log aggregation.

## Contributing

1. Follow vertical slice architecture patterns
2. Use async/await for all I/O operations
3. Include appropriate error handling and logging
4. Write tests for complex business logic
5. Follow C# coding conventions and nullable reference types

## License

This project is licensed under the MIT License - see the LICENSE file for details.
