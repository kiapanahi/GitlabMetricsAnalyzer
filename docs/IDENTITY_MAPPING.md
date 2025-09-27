# Identity Mapping and Configuration

This document describes the simplified identity mapping service for GitLab metrics collection.

## Overview

The system provides:

1. **Identity Mapping Service** - Filter bot accounts using regex patterns
2. **Basic Configuration** - Standard ASP.NET Core configuration support
3. **Optional Project Scoping** - Projects are determined by GitLab PAT access by default

## Configuration Structure

### Standard ASP.NET Core Configuration

Configuration uses standard ASP.NET Core patterns via `appsettings.json`, environment variables, and user secrets.

#### Environment Variables
- **GitLab__Token** - GitLab Personal Access Token (environment variable format)
- **GitLab__BaseUrl** - GitLab instance base URL

#### User Secrets (Development)
- **GitLab:Token** - GitLab Personal Access Token (user secrets format)
- **GitLab:BaseUrl** - GitLab instance base URL

### Configuration Sections

#### Identity
Manages bot detection using regex patterns:
- `BotRegexPatterns` - Regex patterns to identify bot accounts

#### Excludes  
Defines patterns for excluding data from metrics:
- `CommitPatterns` - Commit messages to exclude (e.g., merge commits)
- `BranchPatterns` - Branch names to exclude from metrics
- `FilePatterns` - File paths to exclude from line count metrics

#### ProjectScope (Optional)
When provided, controls which projects are included in metrics collection.
If not configured, all projects accessible by the GitLab PAT are included:
- `IncludeProjects` - Specific project IDs to include
- `ExcludeProjects` - Specific project IDs to exclude  
- `IncludeProjectPatterns` - Regex patterns for project names to include
- `ExcludeProjectPatterns` - Regex patterns for project names to exclude

## Identity Mapping Service

### Features

The `IIdentityMappingService` provides:

1. **Bot Detection** - Identifies bot accounts using configurable regex patterns

### Usage

```csharp
// Inject the service
public class MyService
{
    private readonly IIdentityMappingService _identityService;
    
    public MyService(IIdentityMappingService identityService)
    {
        _identityService = identityService;
    }
    
    public void ProcessCommit(string authorEmail, string authorUsername)
    {
        // Check if this is a bot
        if (_identityService.IsBot(authorUsername, authorEmail))
        {
            // Skip bot commits
            return;
        }
        
        // Process regular developer commit
    }
}
```

### Bot Detection Patterns

Default bot patterns include:
- `^.*bot$` - Matches usernames ending with "bot"
- `^.*\\[bot\\]$` - Matches GitHub-style bot names
- `^gitlab-ci$` - GitLab CI user
- `^dependabot.*` - Dependabot variants
- `^renovate.*` - Renovate bot variants

## Service Registration

The services are automatically registered in dependency injection:

```csharp
// In ServiceCollectionExtensions.cs
builder.Services.Configure<MetricsConfiguration>(
    builder.Configuration.GetSection(MetricsConfiguration.SectionName));
    
builder.Services.AddScoped<IIdentityMappingService, IdentityMappingService>();
```

## Project Detection

By default, the system will collect metrics from all projects that the configured GitLab Personal Access Token has access to. This provides the most straightforward approach where the GitLab PAT permissions determine the project scope.

Optional project scoping can be configured if needed to further filter the accessible projects.

## Testing

Unit tests validate:
- Bot detection with various patterns
- Configuration loading

See `IdentityMappingServiceTests.cs` for examples.