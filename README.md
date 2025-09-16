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
- **Job Scheduling**: Quartz.NET for background data collection
- **Resilience**: Retry policies and circuit breakers

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
   Update `appsettings.Development.json`:
   ```json
   {
     "GitLab": {
       "BaseUrl": "https://your-gitlab-instance.com",
       "PersonalAccessToken": "your-token-here",
       "RetryCount": 3,
       "RetryDelaySeconds": 5,
       "TimeoutSeconds": 30
     },
     "ConnectionStrings": {
       "DefaultConnection": "Host=localhost;Database=GitLabMetrics;Username=your-user;Password=your-password"
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

## API Endpoints

### Health Check
- `GET /health` - Application health status

### GitLab Integration
- `GET /api/gitlab/test-connection` - Test GitLab API connectivity
- `GET /api/gitlab/projects` - List accessible GitLab projects

### Metrics
- `GET /api/metrics/developers` - Developer productivity metrics
- `GET /api/metrics/projects` - Project-level metrics
- `GET /api/metrics/team` - Team-wide analytics

## Data Models

### Raw Data
- **RawCommit**: Individual commit data with stats
- **RawMergeRequest**: Merge request lifecycle data
- **RawPipeline**: CI/CD pipeline execution data

### Calculated Metrics
- **Developer Metrics**: Commit frequency, review participation, pipeline success
- **Project Metrics**: Activity levels, cycle times, quality indicators
- **Team Metrics**: Collaboration patterns, delivery velocity

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

The application includes comprehensive telemetry:
- **Logs**: Structured logging with Serilog
- **Metrics**: Custom metrics for data collection and processing
- **Health Checks**: Database and GitLab API connectivity

## Deployment

The application is designed for containerized deployment with .NET Aspire. It includes:
- Service discovery and configuration
- Distributed tracing
- Health checks
- Graceful shutdown handling

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
