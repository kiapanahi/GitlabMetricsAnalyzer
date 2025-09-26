# Configuration and Identity Mapping

This document describes the JSON configuration system and identity mapping features implemented for GitLab metrics collection.

## Overview

The system now supports:

1. **JSON Configuration Loading** - Load settings from `config.json` with environment variable expansion
2. **Identity Mapping Service** - Consolidate developer identities and filter bot accounts
3. **Project Scoping** - Include/exclude projects based on patterns and IDs
4. **Environment Variable Support** - Use `${VAR_NAME}` syntax in configuration files

## Configuration Structure

### config.json Example

```json
{
  "GitLab": {
    "BaseUrl": "https://your-gitlab-instance.com",
    "Token": "${GITLAB_TOKEN}"
  },
  "Metrics": {
    "ProjectScope": {
      "IncludeProjects": [123, 456, 789],
      "ExcludeProjects": [999],
      "IncludeProjectPatterns": ["^company/.*", "^team/.*"],
      "ExcludeProjectPatterns": ["^.*-archive$", "^test-.*"]
    },
    "Identity": {
      "BotRegexPatterns": [
        "^.*bot$",
        "^.*\\[bot\\]$",
        "^gitlab-ci$",
        "^dependabot.*",
        "^renovate.*"
      ],
      "IdentityOverrides": {
        "john.doe": {
          "DisplayName": "John Doe",
          "PrimaryEmail": "john.doe@company.com",
          "PrimaryUsername": "john.doe",
          "AliasEmails": ["j.doe@company.com", "johndoe@oldcompany.com"],
          "AliasUsernames": ["jdoe", "john_doe"]
        }
      }
    },
    "Excludes": {
      "CommitPatterns": [
        "^Merge branch.*",
        "^Merge pull request.*"
      ],
      "BranchPatterns": [
        "^dependabot/.*",
        "^renovate/.*"
      ],
      "FilePatterns": [
        "^.*\\.min\\.(js|css)$",
        "^.*\\.(png|jpg|jpeg|gif)$"
      ]
    }
  }
}
```

### Environment Variables

The system supports environment variable expansion using `${VARIABLE_NAME}` syntax:

- **GITLAB_TOKEN** - GitLab Personal Access Token
- Any other environment variable can be referenced in configuration

### Configuration Sections

#### ProjectScope
Controls which projects are included in metrics collection:
- `IncludeProjects` - Specific project IDs to include (empty = include all)
- `ExcludeProjects` - Specific project IDs to exclude  
- `IncludeProjectPatterns` - Regex patterns for project names to include
- `ExcludeProjectPatterns` - Regex patterns for project names to exclude

#### Identity
Manages developer identity mapping and bot detection:
- `BotRegexPatterns` - Regex patterns to identify bot accounts
- `IdentityOverrides` - Manual mapping of aliases to canonical developers

#### Excludes  
Defines patterns for excluding data from metrics:
- `CommitPatterns` - Commit messages to exclude (e.g., merge commits)
- `BranchPatterns` - Branch names to exclude from metrics
- `FilePatterns` - File paths to exclude from line count metrics

## Identity Mapping Service

### Features

The `IIdentityMappingService` provides:

1. **Bot Detection** - Identifies bot accounts using regex patterns
2. **Canonical Developer Resolution** - Maps multiple identities to single developer
3. **Alias Consolidation** - Collects all known aliases for a developer

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
        
        // Get canonical developer
        var canonical = _identityService.GetCanonicalDeveloper(authorEmail);
        if (canonical != null)
        {
            // Use canonical identity
            var displayName = canonical.DisplayName;
            var primaryEmail = canonical.PrimaryEmail;
        }
        
        // Get all aliases for consolidation
        var aliases = _identityService.ConsolidateAliases(authorEmail, authorUsername);
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

### Identity Override Example

```json
{
  "john.doe": {
    "DisplayName": "John Doe",
    "PrimaryEmail": "john.doe@company.com", 
    "PrimaryUsername": "john.doe",
    "AliasEmails": [
      "j.doe@company.com",
      "john.doe@old-company.com"
    ],
    "AliasUsernames": [
      "jdoe", 
      "john_doe",
      "jdoe-old"
    ]
  }
}
```

This configuration maps all the alias emails and usernames to the canonical "john.doe" identity.

## Service Registration

The services are automatically registered in dependency injection:

```csharp
// In ServiceCollectionExtensions.cs
builder.Services.Configure<MetricsConfiguration>(
    builder.Configuration.GetSection(MetricsConfiguration.SectionName));
    
builder.Services.AddScoped<IIdentityMappingService, IdentityMappingService>();
```

## Configuration Loading

The `config.json` file is loaded automatically during application startup:

```csharp
// In Program.cs
builder.Configuration.AddConfigJson();
```

This extension method:
1. Looks for `config.json` in the current directory
2. Expands environment variables using `${VAR}` syntax
3. Adds the configuration to the configuration system
4. Gracefully handles missing files or errors

## Testing

Unit tests validate:
- Bot detection with various patterns
- Identity mapping and canonical resolution
- Alias consolidation logic
- Configuration loading and environment variable expansion

See `IdentityMappingServiceTests.cs` and `ConfigurationExtensionsTests.cs` for examples.