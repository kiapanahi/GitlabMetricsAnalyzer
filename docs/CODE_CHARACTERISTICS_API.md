# Code Characteristics Metrics API

## Overview

The Code Characteristics API provides insights into code change patterns and developer working styles. It analyzes commit frequency, commit sizes, merge request sizes, squash merge usage, commit message quality, and branch naming conventions.

## Endpoint

```
GET /api/v1/{userId}/metrics/code-characteristics
```

### Path Parameters

| Parameter | Type   | Required | Description                    |
|-----------|--------|----------|--------------------------------|
| userId    | long   | Yes      | The GitLab user ID to analyze |

### Query Parameters

| Parameter   | Type | Required | Default | Description                                      |
|-------------|------|----------|---------|--------------------------------------------------|
| windowDays  | int  | No       | 30      | Number of days to look back (max: 365)          |

### Response Schema

```json
{
  "userId": 1,
  "username": "john.doe",
  "windowDays": 30,
  "windowStart": "2025-09-13T00:00:00Z",
  "windowEnd": "2025-10-13T00:00:00Z",
  
  // Metric 1: Commit Frequency
  "commitsPerDay": 2.5,
  "commitsPerWeek": 17.5,
  "totalCommits": 75,
  "commitDaysCount": 22,
  
  // Metric 2: Commit Size Distribution
  "commitSizeMedian": 45.0,
  "commitSizeP95": 320.0,
  "commitSizeAverage": 85.5,
  
  // Metric 3: MR Size Distribution
  "mrSizeDistribution": {
    "smallCount": 8,
    "mediumCount": 5,
    "largeCount": 2,
    "extraLargeCount": 1,
    "smallPercentage": 50.0,
    "mediumPercentage": 31.25,
    "largePercentage": 12.5,
    "extraLargePercentage": 6.25
  },
  "totalMergedMrs": 16,
  
  // Metric 4: File Churn Analysis
  "topFilesByChurn": [],
  
  // Metric 5: Squash Merge Strategy
  "squashMergeRate": 0.65,
  "squashedMrsCount": 10,
  
  // Metric 6: Commit Message Quality
  "averageCommitMessageLength": 42.5,
  "conventionalCommitRate": 0.75,
  "conventionalCommitsCount": 56,
  
  // Metric 7: Branch Naming Patterns
  "branchNamingComplianceRate": 0.85,
  "compliantBranchesCount": 13,
  
  "projects": [
    {
      "projectId": 100,
      "projectName": "backend-api",
      "commitCount": 45,
      "mergedMrCount": 10
    },
    {
      "projectId": 200,
      "projectName": "frontend-app",
      "commitCount": 30,
      "mergedMrCount": 6
    }
  ]
}
```

## Metrics Description

### 1. Commit Frequency

Measures how often a developer commits code.

- **commitsPerDay**: Average number of commits per day
- **commitsPerWeek**: Average number of commits per week
- **commitDaysCount**: Number of distinct days with commits
- **Direction**: Context-dependent (consistent activity is good, but quality matters more than quantity)

### 2. Commit Size Distribution

Analyzes the size of commits in lines of code changed.

- **commitSizeMedian**: Median (P50) lines changed per commit
- **commitSizeP95**: 95th percentile lines changed per commit
- **commitSizeAverage**: Average lines changed per commit
- **Direction**: Smaller commits are often preferred (easier to review, less risk)

### 3. MR Size Distribution

Categorizes merge requests by size with configurable thresholds:

- **Small**: < 100 lines changed (default)
- **Medium**: 100-500 lines changed (default)
- **Large**: 500-1000 lines changed (default)
- **Extra Large**: > 1000 lines changed (default)

**Direction**: More small MRs = ↑ good (easier to review, faster to merge)

### 4. File Churn Analysis

Identifies files that are modified most frequently by the developer.

**Note**: Currently returns empty array due to performance considerations. Full implementation would require fetching commit diffs for each commit, which could result in hundreds of additional API calls.

**Direction**: Context-dependent (high churn may indicate ownership or potential hotspots/problem areas)

### 5. Squash vs Merge Strategy

Measures the usage of squash merge vs regular merge.

- **squashMergeRate**: Percentage of MRs merged with squash enabled
- **Direction**: Context-dependent (squash can clean up history but loses granular commit information)

### 6. Commit Message Quality

Analyzes commit message quality based on length and conventional commit format.

- **averageCommitMessageLength**: Average length of commit messages in characters
- **conventionalCommitRate**: Percentage of commits following conventional commit format
- **Conventional Commit Pattern**: `(feat|fix|refactor|docs|test|chore|style|perf|ci|build|revert)(:|\\()`
- **Minimum Message Length**: 15 characters (configurable)
- **Direction**: ↑ good (better documentation and clearer intent)

### 7. Branch Naming Patterns

Measures adherence to branch naming conventions.

- **branchNamingComplianceRate**: Percentage of MRs with compliant branch names
- **Default Patterns**:
  - `feature/*` or `feat/*`
  - `bugfix/*` or `fix/*`
  - `hotfix/*` or `hf/*`
  - `release/*` or `rel/*`
  - `chore/*` or `task/*`
  - `refactor/*` or `refac/*`
- **Direction**: ↑ good (consistent naming improves organization and automation)

## Configuration

The metrics can be customized via the `Metrics.CodeCharacteristics` configuration section:

```json
{
  "Metrics": {
    "CodeCharacteristics": {
      "SmallMrThreshold": 100,
      "MediumMrThreshold": 500,
      "LargeMrThreshold": 1000,
      "TopFilesChurnCount": 10,
      "MinCommitMessageLength": 15,
      "ConventionalCommitPatterns": [
        "^(feat|fix|refactor|docs|test|chore|style|perf|ci|build|revert)(\\([\\w\\-]+\\))?:\\s+.+"
      ],
      "BranchNamingPatterns": [
        "^(feature|feat)/[\\w\\-]+",
        "^(bugfix|fix)/[\\w\\-]+",
        "^(hotfix|hf)/[\\w\\-]+",
        "^(release|rel)/[\\w\\-]+",
        "^(chore|task)/[\\w\\-]+",
        "^(refactor|refac)/[\\w\\-]+"
      ],
      "ExcludedCommitMessagePatterns": [
        "^wip$",
        "^fix$",
        "^typo$",
        "^merge\\s+",
        "^revert\\s+"
      ]
    }
  }
}
```

## Error Responses

### 400 Bad Request

```json
{
  "error": "windowDays must be greater than 0"
}
```

### 404 Not Found

```json
{
  "error": "User with ID 999 not found"
}
```

### 500 Internal Server Error

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.6.1",
  "title": "Error calculating code characteristics metrics",
  "status": 500,
  "detail": "An error occurred while processing the request"
}
```

## Use Cases

### 1. Identify Code Review Patterns

Monitor MR size distribution to encourage smaller, more reviewable merge requests:

```bash
# High percentage of small MRs indicates good practices
curl -X GET "https://api.example.com/api/v1/123/metrics/code-characteristics?windowDays=30"
```

### 2. Improve Commit Quality

Track conventional commit adoption and message quality:

```bash
# Check commit message quality trends
curl -X GET "https://api.example.com/api/v1/123/metrics/code-characteristics?windowDays=90"
```

### 3. Enforce Branch Naming Standards

Monitor branch naming compliance across the team:

```bash
# Identify developers not following branch naming conventions
curl -X GET "https://api.example.com/api/v1/123/metrics/code-characteristics?windowDays=14"
```

### 4. Understand Developer Working Styles

Analyze commit frequency and patterns:

```bash
# See how active a developer is
curl -X GET "https://api.example.com/api/v1/123/metrics/code-characteristics?windowDays=7"
```

## Best Practices

1. **Small MRs**: Encourage developers to keep MRs under 400 lines for easier review
2. **Consistent Commits**: Aim for regular, small commits rather than large infrequent ones
3. **Conventional Commits**: Adopt conventional commit format for better changelog generation
4. **Branch Naming**: Enforce consistent branch naming patterns for better organization
5. **Squash Strategically**: Use squash merge for feature branches, preserve commits for important changes

## Related Metrics

- `/api/v1/{userId}/metrics/quality` - Quality and reliability metrics
- `/api/v1/{userId}/metrics/collaboration` - Collaboration and review metrics
- `/api/v1/{userId}/metrics/flow` - Flow and throughput metrics

## Performance Considerations

- The endpoint fetches data for all projects the user has contributed to
- MR size calculation requires fetching changes for each MR (one API call per MR)
- File churn analysis is currently disabled to avoid excessive API calls
- Consider using shorter time windows (7-30 days) for faster responses
- Results are not cached; each request fetches fresh data from GitLab

## References

- [GitLab Commits API](https://docs.gitlab.com/api/commits.html)
- [GitLab Merge Requests API](https://docs.gitlab.com/api/merge_requests.html)
- [Conventional Commits](https://www.conventionalcommits.org/)
- [GitLab Developer Productivity Metrics PRD](../prds/gitlab-developer-productivity-metrics.md)
