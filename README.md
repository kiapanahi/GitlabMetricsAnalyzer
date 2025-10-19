# GitLab Metrics Analyzer

A .NET 9 REST API that calculates developer productivity metrics from GitLab via live API calls. Built with .NET Aspire for cloud-native development.

## Features

- **Live Metrics Calculation**: Real-time metrics calculated on-demand from GitLab API
- **Comprehensive Analytics**: 10 REST endpoints covering user, team, project, and pipeline metrics
- **Resilient Design**: Built-in retry logic, circuit breaker, and timeout handling (Polly)
- **Developer Insights**: Commit patterns, MR cycle time, collaboration, quality, and code characteristics
- **Real-time Monitoring**: Comprehensive logging and distributed tracing with OpenTelemetry
- **Flexible Time Windows**: Query metrics for any time period (1-365 days)

## Architecture

- **Tech Stack**: .NET 9, ASP.NET Core Minimal APIs, .NET Aspire
- **Design Pattern**: Vertical slice architecture for each feature
- **API Integration**: Live GitLab API v4 integration with NGitLab client
- **Metrics Approach**: Real-time calculation (no data storage)
- **Resilience**: Polly policies (retry, circuit breaker, timeout)
- **Observability**: Serilog structured logging, OpenTelemetry tracing

## Getting Started

### Prerequisites

- .NET 9 SDK
- GitLab instance with API access
- GitLab Personal Access Token with `api` scope
- Visual Studio 2022 or VS Code with C# extensions

### Configuration

1. **GitLab API Setup**:
   - Create a GitLab Personal Access Token with `api` scope
   - Note your GitLab instance URL

2. **Application Configuration**:
   Update `appsettings.json` or use environment variables:
   ```json
   {
     "GitLab": {
       "BaseUrl": "https://your-gitlab-instance.com",
       "Token": "your-token-here"  // Or via env: GitLab__Token
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

   **Environment Variables** (recommended for secrets):
   ```bash
   export GitLab__Token="your-gitlab-token"
   export GitLab__BaseUrl="https://your-gitlab-instance.com"
   ```

### Running the Application

#### Option 1: .NET Aspire (Recommended)
```bash
aspire run
```

#### Option 2: Direct Run
```bash
cd src/KuriousLabs.Management.KPIAnalysis.ApiService
dotnet run
```



## API Endpoints

All endpoints accept optional `windowDays` or `lookbackDays` query parameter (1-365 days, default: 30).

### User Metrics (6 endpoints)
- `GET /api/v1/{userId}/analysis/commit-time` - Commit time distribution across 24 hours
- `GET /api/v1/{userId}/metrics/mr-cycle-time` - Median MR cycle time (P50)
- `GET /api/v1/{userId}/metrics/flow` - Throughput, WIP, context switching
- `GET /api/v1/{userId}/metrics/collaboration` - Reviews, approvals, discussions
- `GET /api/v1/{userId}/metrics/quality` - Rework, reverts, CI success
- `GET /api/v1/{userId}/metrics/code-characteristics` - Commit size, MR size, file churn

### Pipeline Metrics
- `GET /api/v1/metrics/pipelines/{projectId}` - 7 pipeline metrics (failed job rate, retry rate, wait time, deployment frequency, duration trends, success rate by branch, coverage trend)

### Advanced Metrics
- `GET /api/v1/metrics/advanced/{userId}` - Bus factor, response time, batch size, draft duration, iteration count, idle time, cross-team collaboration

### Team Metrics
- `GET /api/v1/teams/{teamId}/metrics` - Team velocity, cross-project contributions, review coverage

### Project Metrics
- `GET /api/v1/projects/{projectId}/metrics` - Activity score, branch lifecycle, label usage, milestone completion

**Complete metrics and API documentation**: See [docs/METRICS_REFERENCE.md](docs/METRICS_REFERENCE.md)  
**Complete endpoint inventory**: See [docs/ENDPOINT_AUDIT.md](docs/ENDPOINT_AUDIT.md)

## Metrics Calculated

### Developer Metrics
- **Commit Time Analysis**: Distribution across 24 hours, peak coding times
- **MR Cycle Time**: Median time from first commit to merge
- **Flow Metrics**: Throughput, WIP, coding time, review time, context switching
- **Collaboration Metrics**: Review comments, approvals, discussion threads, review turnaround time
- **Quality Metrics**: Rework ratio, revert rate, CI success rate, hotfix rate
- **Code Characteristics**: Commit frequency, size distribution, file churn, message quality

### Team & Project Metrics
- **Team Velocity**: Cross-project contributions, review coverage
- **Project Health**: Activity score, branch lifecycle, milestone completion
- **Pipeline Metrics**: Failed job rate, retry rate, wait time, deployment frequency

### Advanced Metrics
- **Bus Factor**: Knowledge distribution risk assessment
- **Response Time Distribution**: Review responsiveness patterns
- **Batch Size**: Work batch size analysis
- **Draft Duration**: Time spent in draft state
- **Iteration Count**: Number of review iterations
- **Idle Time**: Time waiting in review
- **Cross-Team Collaboration**: Inter-team collaboration index

## Development

### Project Structure
```
src/
├── KuriousLabs.Management.KPIAnalysis.ApiService/     # Main API application
│   ├── Features/GitLabMetrics/                  # GitLab integration feature
│   │   ├── Infrastructure/                     # GitLab API client (HTTP)
│   │   ├── Services/                          # Metrics calculation services
│   │   ├── *Endpoints.cs                      # Minimal API endpoints
│   │   └── Configuration/                     # Feature configuration
│   └── Configuration/                          # App-level configuration
├── KuriousLabs.Management.KPIAnalysis.AppHost/       # Aspire orchestration
└── KuriousLabs.Management.KPIAnalysis.ServiceDefaults/ # Shared service defaults

docs/
├── CURRENT_STATE.md                             # Current architecture (✅ accurate)
├── ENDPOINT_AUDIT.md                            # All endpoints documented
├── METRICS_REFERENCE.md                         # Complete metrics reference
├── API_USAGE_GUIDE.md                           # API usage patterns
├── CONFIGURATION_GUIDE.md                       # Configuration options
├── DEPLOYMENT_GUIDE.md                          # Deployment instructions
└── OPERATIONS_RUNBOOK.md                        # Operations guide
```

### Key Services
- **GitLabHttpClient**: GitLab API v4 client with Polly resilience (retry, circuit breaker, timeout)
- **CommitTimeAnalysisService**: Commit time distribution analysis
- **PerDeveloperMetricsService**: MR cycle time and flow metrics
- **CollaborationMetricsService**: Review and collaboration metrics
- **QualityMetricsService**: Code quality and CI metrics
- **CodeCharacteristicsService**: Code patterns and characteristics
- **PipelineMetricsService**: CI/CD pipeline metrics
- **AdvancedMetricsService**: Advanced developer analytics
- **TeamMetricsService**: Team-level aggregations
- **ProjectMetricsService**: Project-level aggregations

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
- **GitLab Connectivity**: Automatic GitLab API health validation
- **Aspire Dashboard**: Real-time telemetry and health monitoring

### Logging & Telemetry
- **Structured Logging**: Serilog with JSON formatting and correlation IDs
- **Distributed Tracing**: OpenTelemetry with activity tracking
- **Metrics Collection**: API request timing, GitLab API call metrics
- **Error Tracking**: Detailed error reporting with context

### Key Metrics to Monitor
- GitLab API response times
- API rate limit consumption (GitLab quotas)
- Endpoint response times
- Error rates per endpoint
- Circuit breaker state (Polly resilience)

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
docker run -p 5000:8080 \
  -e GitLab__Token="your-gitlab-token" \
  -e GitLab__BaseUrl="https://your-gitlab-instance.com" \
  gitlab-metrics-analyzer
```

## Future Roadmap

### Performance Improvements
- **Response Caching**: Redis/memory cache for frequently requested metrics
- **Batch GitLab API Calls**: Parallel queries to reduce latency
- **Query Optimization**: Efficient GitLab API usage patterns

### Feature Enhancements
- **Authentication & Authorization**: JWT/OAuth for API security
- **Rate Limiting**: Protect API and GitLab from excessive requests
- **Webhooks**: Real-time updates from GitLab events
- **Historical Data Storage** (optional): Persist metrics for trend analysis
- **Custom Dashboards**: Interactive visualization components
- **Export Feature**: CSV/JSON/Excel exports for reporting

## Troubleshooting

### Common Issues

1. **GitLab API Connection Failures**:
   - Verify token permissions (`api` scope required)
   - Check network connectivity to GitLab instance
   - Review GitLab instance rate limiting
   - Verify BaseUrl is correct (no trailing slash issues)

2. **Slow Response Times**:
   - Large time windows (>90 days) may be slow
   - GitLab API may be under load
   - Check circuit breaker state (may be open)

3. **Empty or Missing Metrics**:
   - Verify user/project IDs exist in GitLab
   - Check GitLab permissions for the token
   - Review bot filtering patterns (may be excluding too much)
   - Check time window (data may not exist in range)

### Logging

Logs are structured and include:
- Request/response details for GitLab API calls
- Polly resilience policy actions (retries, circuit breaker)
- Error details with correlation IDs
- Performance metrics

View logs in:
- **Development**: Console output
- **Aspire Dashboard**: https://localhost:17237
- **Production**: Configure external log aggregation (e.g., Seq, Elasticsearch)

## Documentation

For more detailed information:
- **[METRICS_REFERENCE.md](docs/METRICS_REFERENCE.md)** - Complete metrics and API reference
- **[CURRENT_STATE.md](docs/CURRENT_STATE.md)** - Current architecture overview
- **[ENDPOINT_AUDIT.md](docs/ENDPOINT_AUDIT.md)** - All endpoints documented with examples
- **[CONFIGURATION_GUIDE.md](docs/CONFIGURATION_GUIDE.md)** - Configuration options and setup
- **[API_USAGE_GUIDE.md](docs/API_USAGE_GUIDE.md)** - API usage patterns and examples
- **[DEPLOYMENT_GUIDE.md](docs/DEPLOYMENT_GUIDE.md)** - Deployment instructions
- **[OPERATIONS_RUNBOOK.md](docs/OPERATIONS_RUNBOOK.md)** - Operations and maintenance

## Contributing

See [.github/copilot-instructions.md](.github/copilot-instructions.md) for coding standards and practices:
- Vertical slice architecture
- Async/await patterns
- File-scoped namespaces
- Nullable reference types
- Latest C# features

## License

This project is licensed under the MIT License - see the LICENSE file for details.
