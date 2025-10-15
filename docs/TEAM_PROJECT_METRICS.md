# Team & Project-Level Aggregation Metrics

This document describes the team and project-level aggregation metrics feature that provides organizational insights beyond individual developer metrics.

## Overview

The team and project metrics services aggregate data across multiple developers and projects to provide insights at the organizational level. These metrics support OKR tracking, resource planning, and process improvement initiatives.

## Configuration

### Team Mapping

Teams must be configured in `appsettings.json` to use team-level metrics:

```json
{
  "Metrics": {
    "TeamMapping": {
      "Teams": [
        {
          "Id": "backend-team",
          "Name": "Backend Team",
          "Members": [123, 456, 789]
        },
        {
          "Id": "frontend-team",
          "Name": "Frontend Team",
          "Members": [234, 567, 890]
        }
      ]
    }
  }
}
```

**Configuration Properties:**
- `Id`: Unique identifier for the team (used in API calls)
- `Name`: Human-readable team name
- `Members`: Array of GitLab user IDs that belong to the team

## API Endpoints

### Team Metrics

**Endpoint:** `GET /api/v1/teams/{teamId}/metrics`

**Query Parameters:**
- `windowDays` (optional): Number of days to analyze (default: 30, max: 365)

**Example Request:**
```bash
curl "https://your-instance/api/v1/teams/backend-team/metrics?windowDays=30"
```

**Response:**
```json
{
  "teamId": "backend-team",
  "teamName": "Backend Team",
  "memberCount": 3,
  "windowDays": 30,
  "windowStart": "2025-09-15T00:00:00Z",
  "windowEnd": "2025-10-15T00:00:00Z",
  "totalMergedMrs": 45,
  "totalCommits": 0,
  "totalLinesChanged": 0,
  "avgMrCycleTimeP50H": 24.5,
  "crossProjectContributors": 2,
  "totalProjectsTouched": 5,
  "teamReviewCoveragePercentage": 88.9,
  "minReviewersRequired": 1,
  "mrsWithSufficientReviewers": 40,
  "projectActivities": [
    {
      "projectId": 100,
      "projectName": "company/api-service",
      "commitCount": 0,
      "mergedMrCount": 25,
      "contributorCount": 3
    },
    {
      "projectId": 101,
      "projectName": "company/web-app",
      "commitCount": 0,
      "mergedMrCount": 20,
      "contributorCount": 2
    }
  ]
}
```

### Project Metrics

**Endpoint:** `GET /api/v1/projects/{projectId}/metrics`

**Query Parameters:**
- `windowDays` (optional): Number of days to analyze (default: 30, max: 365)

**Example Request:**
```bash
curl "https://your-instance/api/v1/projects/100/metrics?windowDays=30"
```

**Response:**
```json
{
  "projectId": 100,
  "projectName": "company/api-service",
  "windowDays": 30,
  "windowStart": "2025-09-15T00:00:00Z",
  "windowEnd": "2025-10-15T00:00:00Z",
  "totalCommits": 125,
  "totalMergedMrs": 45,
  "totalLinesChanged": 0,
  "uniqueContributors": 5,
  "crossProjectContributors": 3,
  "longLivedBranchCount": 2,
  "avgLongLivedBranchAgeDays": 45.5,
  "longLivedBranches": [
    {
      "name": "feature/legacy-refactor",
      "ageDays": 60,
      "lastCommitDate": "2025-08-16T10:30:00Z",
      "isMerged": false
    },
    {
      "name": "experimental/new-architecture",
      "ageDays": 31,
      "lastCommitDate": "2025-09-14T15:45:00Z",
      "isMerged": false
    }
  ],
  "labelUsageDistribution": {
    "feature": 20,
    "bugfix": 15,
    "refactor": 8,
    "documentation": 2
  },
  "milestoneCompletionRate": 75.0,
  "completedMilestones": 3,
  "onTimeMilestones": 2,
  "totalMilestones": 4,
  "reviewCoveragePercentage": 88.9,
  "minReviewersRequired": 1,
  "mrsWithSufficientReviewers": 40
}
```

## Metrics Descriptions

### Team Metrics

#### 1. Team Velocity
- **Total Merged MRs**: Aggregate count of merged merge requests by all team members
- **Total Commits**: Aggregate commit count across all team projects
- **Total Lines Changed**: Aggregate lines added/deleted across all merged MRs
- **Avg MR Cycle Time (P50)**: Median cycle time across all team MRs
- **Direction**: ↑ good (higher throughput indicates higher velocity)
- **Use Case**: Sprint planning, capacity estimation
- **Note**: Lines changed is calculated by fetching MR changes for each merged MR (one API call per MR)

#### 2. Project Activity Scores
- **Per-Project Breakdown**: Lists each project the team contributes to with:
  - Commit count
  - Merged MR count
  - Number of team members contributing
- **Direction**: Context-dependent
- **Use Case**: Resource allocation, identifying key projects

#### 3. Cross-Project Contributors
- **Count**: Number of team members contributing to multiple projects
- **Total Projects**: Total unique projects touched by the team
- **Direction**: Context-dependent (can indicate flexibility or context switching)
- **Use Case**: Knowledge distribution, context switching analysis

#### 4. Team Review Coverage
- **Percentage**: % of MRs with at least N reviewers (default: 1)
- **Direction**: ↑ good (higher coverage indicates better code review practices)
- **Use Case**: Quality assurance, process adherence

### Project Metrics

#### 1. Project Activity Score
- **Total Commits**: Count of commits in the analysis window
- **Total Merged MRs**: Count of merged merge requests
- **Total Lines Changed**: Aggregate code changes across all merged MRs
- **Direction**: Context-dependent
- **Use Case**: Project health monitoring, activity tracking
- **Note**: Lines changed is calculated by fetching MR changes for each merged MR (one API call per MR)

#### 2. Branch Lifecycle Analysis
- **Long-Lived Branch Count**: Branches older than 30 days that aren't merged
- **Average Age**: Mean age of long-lived branches in days
- **Branch Details**: List of long-lived branches with ages and last commit dates
- **Direction**: ↓ good (fewer stale branches indicates better branch hygiene)
- **Use Case**: Technical debt identification, workflow optimization

#### 3. Label Usage Distribution
- **Label Counts**: Most common labels on MRs with their frequencies
- **Direction**: Context-dependent
- **Use Case**: Process adherence tracking, categorization analysis

#### 4. Milestone Completion Rate
- **Percentage**: % of completed milestones that finished on time
- **Completed Count**: Total milestones marked as closed
- **On-Time Count**: Milestones completed before their due date
- **Direction**: ↑ good (higher rate indicates better planning/execution)
- **Use Case**: Project planning, delivery predictability

#### 5. Review Coverage
- **Percentage**: % of MRs with at least N reviewers (configurable, default: 1)
- **Direction**: ↑ good (higher coverage indicates better code quality practices)
- **Use Case**: Quality assurance, team collaboration

#### 6. Cross-Project Contributors
- **Count**: Number of project contributors also working on other projects
- **Unique Contributors**: Total unique developers contributing to the project
- **Direction**: Context-dependent
- **Use Case**: Knowledge distribution, resource planning

## Use Cases

### Executive/Leadership Level
- **OKR Tracking**: Monitor team velocity and milestone completion rates
- **Resource Allocation**: Identify which projects need more attention
- **Planning**: Use historical velocity data for sprint/quarter planning

### Engineering Managers
- **Team Health**: Monitor review coverage and cross-team collaboration
- **Process Improvement**: Identify bottlenecks via branch lifecycle analysis
- **Knowledge Distribution**: Track cross-project contributors

### Tech Leads
- **Technical Debt**: Long-lived branches indicate stale work
- **Code Review Culture**: Review coverage metrics
- **Project Focus**: Identify projects consuming most team resources

## Performance Considerations

### Caching Strategy
- Consider implementing caching for expensive aggregations
- Recommended cache duration: 1-3 hours for team metrics
- Project metrics can be cached for longer periods (6-12 hours)

### Query Optimization
- Team metrics make multiple API calls per member and per MR/project:
  - Commits: One call per project
  - MR Changes: One call per merged MR
  - For a team with 5 members across 3 projects with 50 total MRs, this results in ~53 API calls
- For large teams (>10 members) or active projects (>100 MRs), consider:
  - Limiting the window period (30-90 days)
  - Running calculations asynchronously
  - Pre-computing metrics via background jobs
  - GitLab's API rate limit is typically 600 requests/minute for authenticated users

### Efficient Aggregation
- Current implementation fetches data on-demand
- For production use, consider:
  - Materialized views for frequently accessed aggregations
  - Background jobs for pre-calculation
  - Database indexes on frequently queried fields

## Future Enhancements

### Short-Term
1. **Configurable Review Thresholds**: Allow teams to set their own minimum reviewer counts
2. **Time-Series Support**: Track metrics over time for trending analysis
3. **Improved Contributor Deduplication**: Better matching of commit authors to GitLab user IDs

### Long-Term
1. **Background Processing**: Move expensive calculations to background jobs
2. **Caching Layer**: Implement Redis caching for frequently accessed metrics
3. **Real-Time Updates**: WebSocket support for live metric updates
4. **Custom Dashboards**: Pre-built visualization components for common use cases
5. **Alerting**: Notify when metrics fall outside expected ranges

## Related Documentation
- [API Usage Guide](./API_USAGE_GUIDE.md) - General API documentation
- [Identity Mapping](./IDENTITY_MAPPING.md) - Team and user configuration
- [Advanced Metrics](./ADVANCED_METRICS_FEATURE_SUMMARY.md) - Individual developer metrics
- [Operations Runbook](./OPERATIONS_RUNBOOK.md) - Deployment and maintenance
