# Configuration Guide

This guide provides detailed configuration instructions for setting up and customizing the GitLab Metrics Analyzer for your environment.

## Table of Contents
- [Overview](#overview)
- [Core Configuration](#core-configuration)
- [GitLab Integration](#gitlab-integration)
- [Database Configuration](#database-configuration)
- [Metrics Configuration](#metrics-configuration)
- [Processing Configuration](#processing-configuration)
- [Export Configuration](#export-configuration)
- [Logging Configuration](#logging-configuration)
- [Environment-Specific Settings](#environment-specific-settings)
- [Security Considerations](#security-considerations)
- [Performance Tuning](#performance-tuning)

## Overview

The GitLab Metrics Analyzer uses a hierarchical configuration system based on .NET's configuration providers:

1. **appsettings.json** - Base configuration
2. **appsettings.{Environment}.json** - Environment-specific overrides
3. **Environment Variables** - Runtime overrides
4. **Command Line Arguments** - Highest priority overrides

## Core Configuration

### Base Configuration File (appsettings.json)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "GitLab": {
    "BaseUrl": "https://gitlab.qcluster.org/",
    "Token": ""
  },
  "Processing": {
    "MaxDegreeOfParallelism": 8,
    "BackfillDays": 180
  },
  "Exports": {
    "Directory": "/data/exports"
  },
  "Timezone": "Asia/Tehran",
  "Metrics": {
    "Identity": {
      "BotRegexPatterns": [
        "^.*bot$",
        "^.*\\[bot\\]$",
        "^gitlab-ci$",
        "^dependabot.*",
        "^renovate.*",
        "^.*automation.*$"
      ]
    },
    "Excludes": {
      "CommitPatterns": [
        "^Merge branch.*",
        "^Merge pull request.*",
        "^Merge.*",
        "^Revert.*"
      ],
      "BranchPatterns": [
        "^dependabot/.*",
        "^renovate/.*"
      ],
      "FilePatterns": [
        "^.*\\.min\\.(js|css)$",
        "^.*\\.(png|jpg|jpeg|gif|svg|ico)$",
        "^.*\\.lock$",
        "^package-lock\\.json$",
        "^yarn\\.lock$"
      ]
    }
  }
}
```

### Development Configuration (appsettings.Development.json)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics": "Debug"
    }
  },
  "Processing": {
    "MaxDegreeOfParallelism": 2,
    "BackfillDays": 30
  },
  "Exports": {
    "Directory": "./exports"
  }
}
```

## GitLab Integration

### GitLab Configuration Section

```json
{
  "GitLab": {
    "BaseUrl": "https://your-gitlab-instance.com/",
    "Token": "glpat-xxxxxxxxxxxxxxxxxxxx",
    "ApiVersion": "v4",
    "TimeoutSeconds": 30,
    "RetryCount": 3,
    "RetryDelaySeconds": 5,
    "MaxPageSize": 100,
    "RateLimitPerSecond": 10
  }
}
```

#### Configuration Options

| Setting | Description | Required | Default | Example |
|---------|-------------|----------|---------|---------|
| `BaseUrl` | GitLab instance URL | ✅ | - | `https://gitlab.company.com/` |
| `Token` | Personal Access Token | ✅ | - | `glpat-xxxxxxxxxxxxxxxxxxxx` |
| `ApiVersion` | GitLab API version | ❌ | `v4` | `v4` |
| `TimeoutSeconds` | Request timeout | ❌ | `30` | `60` |
| `RetryCount` | Retry attempts | ❌ | `3` | `5` |
| `RetryDelaySeconds` | Delay between retries | ❌ | `5` | `10` |
| `MaxPageSize` | API pagination size | ❌ | `100` | `50` |
| `RateLimitPerSecond` | Client-side rate limiting | ❌ | `10` | `5` |

### Creating GitLab Personal Access Token

1. **Navigate to GitLab Profile Settings**:
   - Go to `https://your-gitlab-instance.com/-/profile/personal_access_tokens`

2. **Create New Token**:
   - Name: `GitLab Metrics Analyzer`
   - Expiration: Set appropriate expiration date
   - Scopes: Select `api` (full API access)

3. **Required Permissions**:
   The token needs access to:
   - Read repository data (commits, merge requests, pipelines)
   - Read user information
   - Read project information
   - Read CI/CD pipeline data

### Environment Variable Override

```bash
# Set GitLab token via environment variable
export GitLab__Token="glpat-xxxxxxxxxxxxxxxxxxxx"
export GitLab__BaseUrl="https://gitlab.company.com/"

# Docker environment variables
docker run -e GitLab__Token="glpat-xxx" -e GitLab__BaseUrl="https://gitlab.company.com/" gitlab-metrics-analyzer
```

## Database Configuration

### Connection String Configuration

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=GitLabMetrics;Username=metrics_user;Password=secure_password;Include Error Detail=true"
  }
}
```

### PostgreSQL Setup

#### Create Database and User

```sql
-- Create database
CREATE DATABASE GitLabMetrics;

-- Create user with appropriate permissions
CREATE USER metrics_user WITH PASSWORD 'secure_password';

-- Grant necessary permissions
GRANT ALL PRIVILEGES ON DATABASE GitLabMetrics TO metrics_user;

-- Connect to the database
\c GitLabMetrics

-- Grant schema permissions
GRANT ALL ON SCHEMA public TO metrics_user;
GRANT ALL ON ALL TABLES IN SCHEMA public TO metrics_user;
GRANT ALL ON ALL SEQUENCES IN SCHEMA public TO metrics_user;
```

#### Connection String Parameters

| Parameter | Description | Example |
|-----------|-------------|---------|
| `Host` | Database server hostname | `localhost` |
| `Port` | Database server port | `5432` |
| `Database` | Database name | `GitLabMetrics` |
| `Username` | Database user | `metrics_user` |
| `Password` | Database password | `secure_password` |
| `Include Error Detail` | Include detailed errors | `true` (dev only) |
| `Pooling` | Enable connection pooling | `true` |
| `MinPoolSize` | Minimum connections | `0` |
| `MaxPoolSize` | Maximum connections | `100` |
| `CommandTimeout` | Query timeout (seconds) | `30` |

### Environment Variable Override

```bash
# Set connection string via environment variable
export ConnectionStrings__DefaultConnection="Host=db.company.com;Database=GitLabMetrics;Username=metrics_user;Password=secure_password"
```

### Docker/Kubernetes Configuration

```yaml
# docker-compose.yml
version: '3.8'
services:
  postgres:
    image: postgres:15
    environment:
      POSTGRES_DB: GitLabMetrics
      POSTGRES_USER: metrics_user
      POSTGRES_PASSWORD: secure_password
    volumes:
      - postgres_data:/var/lib/postgresql/data
    ports:
      - "5432:5432"

  app:
    image: gitlab-metrics-analyzer
    environment:
      ConnectionStrings__DefaultConnection: "Host=postgres;Database=GitLabMetrics;Username=metrics_user;Password=secure_password"
      GitLab__Token: "glpat-xxxxxxxxxxxxxxxxxxxx"
    depends_on:
      - postgres

volumes:
  postgres_data:
```

## Metrics Configuration

### Identity Configuration

Configure developer identity mapping and bot detection:

```json
{
  "Metrics": {
    "Identity": {
      "BotRegexPatterns": [
        "^.*bot$",           // Matches: "deploybot", "testbot"
        "^.*\\[bot\\]$",     // Matches: "dependabot[bot]"
        "^gitlab-ci$",       // Exact match: "gitlab-ci"
        "^dependabot.*",     // Matches: "dependabot*"
        "^renovate.*",       // Matches: "renovate*" 
        "^.*automation.*$",  // Matches: "*automation*"
        "^jenkins$",         // Exact match: "jenkins"
        "^github-actions$"   // Exact match: "github-actions"
      ]
    }
  }
}
```

### Exclusion Configuration

Configure what data to exclude from metrics:

```json
{
  "Metrics": {
    "Excludes": {
      "CommitPatterns": [
        "^Merge branch.*",         // Merge commits
        "^Merge pull request.*",   // GitHub-style merges
        "^Merge.*",               // General merge commits
        "^Revert.*",              // Revert commits
        "^Initial commit$",        // Initial repository commits
        "^Update README.*",        // Documentation updates
        "^Bump version.*",         // Version bump commits
        "^\\[skip ci\\].*"        // CI skip commits
      ],
      "BranchPatterns": [
        "^dependabot/.*",         // Dependabot branches
        "^renovate/.*",          // Renovate branches  
        "^hotfix/.*",            // Hotfix branches (optional)
        "^feature/temp-.*",      // Temporary feature branches
        "^experiment/.*"         // Experimental branches
      ],
      "FilePatterns": [
        "^.*\\.min\\.(js|css)$",     // Minified files
        "^.*\\.(png|jpg|jpeg|gif|svg|ico)$", // Image files
        "^.*\\.lock$",               // Lock files (generic)
        "^package-lock\\.json$",     // NPM lock file
        "^yarn\\.lock$",             // Yarn lock file
        "^composer\\.lock$",         // Composer lock file
        "^Gemfile\\.lock$",          // Ruby lock file
        "^.*\\.generated\\.(cs|js|ts)$", // Generated code files
        "^dist/.*$",                 // Distribution/build artifacts
        "^build/.*$",                // Build artifacts
        "^node_modules/.*$",         // Node.js dependencies
        "^vendor/.*$"                // Vendor dependencies
      ]
    }
  }
}
```

### Project Scope Configuration (Optional)

Filter metrics collection to specific projects:

```json
{
  "Metrics": {
    "ProjectScope": {
      "IncludeProjects": [
        {
          "Id": 123,
          "Name": "core-api",
          "PathWithNamespace": "company/core-api"
        },
        {
          "Id": 456, 
          "Name": "web-frontend",
          "PathWithNamespace": "company/web-frontend"
        }
      ],
      "ExcludeProjects": [
        {
          "Id": 789,
          "Name": "legacy-system",
          "PathWithNamespace": "archive/legacy-system"
        }
      ],
      "IncludeGroups": [
        {
          "Id": 10,
          "Name": "core-team",
          "FullPath": "company/core-team"
        }
      ],
      "ExcludeGroups": [
        {
          "Id": 20,
          "Name": "archived",
          "FullPath": "archive"
        }
      ]
    }
  }
}
```

## Processing Configuration

Configure data collection and processing behavior:

```json
{
  "Processing": {
    "MaxDegreeOfParallelism": 8,
    "BackfillDays": 180,
    "IncrementalWindowHours": 24,
    "BatchSize": 100,
    "MemoryLimitMB": 1024,
    "EnableWindowedCollection": true,
    "WindowSizeHours": 4,
    "MaxWindowRetries": 3
  }
}
```

### Configuration Options

| Setting | Description | Default | Recommendations |
|---------|-------------|---------|-----------------|
| `MaxDegreeOfParallelism` | Concurrent processing threads | `8` | CPU cores × 2 |
| `BackfillDays` | Days to collect in backfill | `180` | 30-365 based on needs |
| `IncrementalWindowHours` | Incremental collection window | `24` | 24-72 for overlap |
| `BatchSize` | Records per batch | `100` | 50-500 based on memory |
| `MemoryLimitMB` | Memory limit per process | `1024` | Adjust based on system |
| `EnableWindowedCollection` | Use windowed incremental | `true` | Recommended for large datasets |
| `WindowSizeHours` | Window size for windowed collection | `4` | 2-8 hours |
| `MaxWindowRetries` | Retries per window | `3` | 3-5 retries |

### Environment-Specific Tuning

#### Development Environment
```json
{
  "Processing": {
    "MaxDegreeOfParallelism": 2,
    "BackfillDays": 30,
    "BatchSize": 50
  }
}
```

#### Production Environment
```json
{
  "Processing": {
    "MaxDegreeOfParallelism": 16,
    "BackfillDays": 365,
    "BatchSize": 200,
    "MemoryLimitMB": 4096
  }
}
```

## Export Configuration

Configure data export functionality:

```json
{
  "Exports": {
    "Directory": "/data/exports",
    "MaxFileSizeMB": 100,
    "RetentionDays": 90,
    "EnableCompression": true,
    "DefaultFormat": "json",
    "SupportedFormats": ["json", "csv", "excel"],
    "ExcelTemplate": {
      "IncludeCharts": true,
      "IncludeSummarySheet": true,
      "MaxRowsPerSheet": 65536
    }
  }
}
```

### Directory Structure
```
/data/exports/
├── developers/           # Developer metrics exports
│   ├── daily/           # Daily automated exports
│   ├── weekly/          # Weekly reports
│   └── adhoc/           # On-demand exports
├── projects/            # Project-level exports
├── archive/             # Archived exports
└── templates/           # Export templates
```

## Logging Configuration

### Structured Logging with Serilog

```json
{
  "Serilog": {
    "Using": ["Serilog.Sinks.Console", "Serilog.Sinks.File"],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.AspNetCore": "Warning",
        "Microsoft.EntityFrameworkCore": "Information",
        "Toman.Management.KPIAnalysis": "Debug"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] [{SourceContext}] {Message:lj} {Properties:j}{NewLine}{Exception}"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/app-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 7,
          "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] [{SourceContext}] {Message:lj} {Properties:j}{NewLine}{Exception}"
        }
      }
    ],
    "Enrich": ["FromLogContext", "WithMachineName", "WithThreadId"],
    "Properties": {
      "Application": "GitLabMetricsAnalyzer"
    }
  }
}
```

### Log Levels by Component

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning",
      "Microsoft.EntityFrameworkCore.Database.Command": "Information",
      "Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services": "Debug",
      "Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure": "Information"
    }
  }
}
```

## Environment-Specific Settings

### Development Environment
```json
{
  "Environment": "Development",
  "GitLab": {
    "BaseUrl": "https://gitlab-dev.company.com/",
    "Token": "glpat-dev-token"
  },
  "Processing": {
    "MaxDegreeOfParallelism": 2,
    "BackfillDays": 7
  },
  "Exports": {
    "Directory": "./dev-exports"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Debug"
    }
  }
}
```

### Staging Environment
```json
{
  "Environment": "Staging",
  "GitLab": {
    "BaseUrl": "https://gitlab-staging.company.com/",
    "Token": "glpat-staging-token"
  },
  "Processing": {
    "MaxDegreeOfParallelism": 4,
    "BackfillDays": 30
  },
  "Exports": {
    "Directory": "/staging/exports"
  }
}
```

### Production Environment
```json
{
  "Environment": "Production",
  "GitLab": {
    "BaseUrl": "https://gitlab.company.com/",
    "TimeoutSeconds": 60,
    "RetryCount": 5
  },
  "Processing": {
    "MaxDegreeOfParallelism": 8,
    "BackfillDays": 365,
    "MemoryLimitMB": 4096
  },
  "Exports": {
    "Directory": "/data/exports",
    "RetentionDays": 90
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

## Security Considerations

### Sensitive Data Protection

1. **Never commit secrets to source control**:
   ```bash
   # Use environment variables for sensitive data
   export GitLab__Token="glpat-xxxxxxxxxxxxxxxxxxxx"
   export ConnectionStrings__DefaultConnection="Host=...;Password=secure_password"
   ```

2. **Use Azure Key Vault, AWS Secrets Manager, or similar**:
   ```json
   {
     "Azure": {
       "KeyVault": {
         "VaultUri": "https://your-vault.vault.azure.net/",
         "ClientId": "your-client-id"
       }
     }
   }
   ```

3. **Configure HTTPS only in production**:
   ```json
   {
     "Kestrel": {
       "Endpoints": {
         "Https": {
           "Url": "https://localhost:5001"
         }
       }
     }
   }
   ```

### Network Security

```json
{
  "AllowedHosts": "gitlab-metrics.company.com",
  "ForwardedHeaders": {
    "ForwardedProtoHeaderName": "X-Forwarded-Proto",
    "ForwardedHostHeaderName": "X-Forwarded-Host"
  },
  "Cors": {
    "AllowedOrigins": [
      "https://dashboard.company.com",
      "https://reports.company.com"
    ]
  }
}
```

## Performance Tuning

### Database Performance
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=db.company.com;Database=GitLabMetrics;Username=metrics_user;Password=secure_password;Pooling=true;MinPoolSize=5;MaxPoolSize=100;CommandTimeout=60"
  }
}
```

### Memory Management
```json
{
  "Processing": {
    "MaxDegreeOfParallelism": 8,
    "BatchSize": 100,
    "MemoryLimitMB": 2048,
    "GCSettings": {
      "ServerGC": true,
      "ConcurrentGC": true
    }
  }
}
```

### HTTP Client Configuration
```json
{
  "GitLab": {
    "TimeoutSeconds": 30,
    "MaxConnectionsPerServer": 10,
    "PooledConnectionLifetime": "00:05:00",
    "PooledConnectionIdleTimeout": "00:02:00"
  }
}
```

### Caching Configuration
```json
{
  "Caching": {
    "Memory": {
      "SizeLimit": "100MB",
      "CompactionPercentage": 0.2
    },
    "Distributed": {
      "ConnectionString": "localhost:6379",
      "InstanceName": "GitLabMetrics"
    }
  }
}
```

## Configuration Validation

The application includes built-in configuration validation. Invalid configurations will prevent startup with descriptive error messages:

```csharp
// Example validation errors
Configuration validation failed:
- GitLab.Token is required
- GitLab.BaseUrl must be a valid URI
- Processing.MaxDegreeOfParallelism must be between 1 and 32
- Exports.Directory must be an existing directory path
```

## Configuration Management Best Practices

1. **Use environment-specific configuration files**
2. **Store secrets in secure secret management systems**
3. **Validate configuration on application startup**
4. **Document configuration changes in version control**
5. **Use infrastructure as code for production deployments**
6. **Monitor configuration drift in production**
7. **Implement configuration change approval processes**

This configuration guide should be updated as new configuration options are added to the system.