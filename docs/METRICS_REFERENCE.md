# Metrics Reference Guide

Complete reference for all metrics, calculations, and APIs in GitLab Metrics Analyzer.

## Table of Contents

1. [Overview](#overview)
2. [Quick API Reference](#quick-api-reference)
3. [Flow & Throughput Metrics](#flow--throughput-metrics)
4. [MR Cycle Time Metrics](#mr-cycle-time-metrics)
5. [Quality & Reliability Metrics](#quality--reliability-metrics)
6. [Code Characteristics Metrics](#code-characteristics-metrics)
7. [Pipeline & CI/CD Metrics](#pipeline--cicd-metrics)
8. [Advanced Metrics](#advanced-metrics)
9. [Commit Time Analysis](#commit-time-analysis)
10. [Team & Project Metrics](#team--project-metrics)
11. [Configuration](#configuration)
12. [Best Practices](#best-practices)

---

## Overview

GitLab Metrics Analyzer provides 35+ developer productivity and code quality metrics calculated in real-time from GitLab API data. All metrics support configurable time windows (1-365 days) and are designed for engineering leadership decision-making.

**Key Features:**
- Real-time calculation (no data storage required)
- Flexible time windows for analysis
- Team and project-level aggregations
- Configurable thresholds and patterns
- Built with .NET 9 and minimal APIs

---

## Quick API Reference

### User Metrics
| Endpoint                                            | Description         | Key Metrics                                         |
| --------------------------------------------------- | ------------------- | --------------------------------------------------- |
| `GET /api/v1/{userId}/metrics/flow`                 | Flow and throughput | Merged MRs, lines changed, coding time, review time |
| `GET /api/v1/{userId}/metrics/mr-cycle-time`        | MR cycle time       | P50 and P90 cycle time                              |
| `GET /api/v1/{userId}/metrics/quality`              | Quality indicators  | Rework ratio, revert rate, CI success               |
| `GET /api/v1/{userId}/metrics/code-characteristics` | Code patterns       | Commit size, MR size, message quality               |
| `GET /api/v1/{userId}/analysis/commit-time`         | Commit patterns     | Hourly distribution, peak times                     |
| `GET /api/v1/{userId}/metrics/advanced`             | Advanced analytics  | Bus factor, batch size, iteration count             |

### Team & Project Metrics
| Endpoint                                    | Description       | Key Metrics                                   |
| ------------------------------------------- | ----------------- | --------------------------------------------- |
| `GET /api/v1/teams/{teamId}/metrics`        | Team aggregations | Velocity, review coverage, cross-project work |
| `GET /api/v1/projects/{projectId}/metrics`  | Project health    | Activity score, branch lifecycle, milestones  |
| `GET /api/v1/metrics/pipelines/{projectId}` | CI/CD metrics     | Failed jobs, retry rate, deployment frequency |

**Common Query Parameters:**
- `windowDays` (optional): Analysis period in days (default: 30, max: 365)
- `lookbackDays` (optional): Alternative name for windowDays in some endpoints

---

## Flow & Throughput Metrics

### Endpoint
```
GET /api/v1/{userId}/metrics/flow
```

### Metrics Calculated

#### 1. Merged MRs Count
- **Description**: Total number of merged merge requests in time window
- **Unit**: count
- **Direction**: ↑ good (higher throughput)
- **Use Case**: Track developer output and sprint velocity

#### 2. Lines Changed
- **Description**: Total additions + deletions across all merged MRs
- **Unit**: lines
- **Direction**: Context-dependent (can indicate productivity or complexity)
- **Use Case**: Measure code output volume

#### 3. Coding Time (Median)
- **Description**: Time from first commit in MR to MR creation
- **Formula**: `median(mr_created_at - first_commit_at)`
- **Unit**: hours
- **Direction**: ↓ good (faster development cycle)
- **Use Case**: Measure development efficiency

#### 4. Time to First Review (Median)
- **Description**: Time from MR creation to first non-author comment
- **Formula**: `median(first_review_at - mr_created_at)`
- **Unit**: hours
- **Direction**: ↓ good (responsive review process)
- **Use Case**: Identify review bottlenecks

#### 5. Review Time (Median)
- **Description**: Time from first review to approval
- **Status**: Not available (requires approval API data)
- **Unit**: hours
- **Direction**: ↓ good (faster review cycles)

#### 6. Merge Time (Median)
- **Description**: Time from MR creation to merge
- **Formula**: `median(merged_at - created_at)`
- **Unit**: hours
- **Direction**: ↓ good (faster delivery)
- **Use Case**: Track overall MR cycle time

#### 7. WIP/Open MRs
- **Description**: Count of open or draft MRs at snapshot time
- **Unit**: count
- **Direction**: ↓ good (lower work-in-progress)
- **Use Case**: Identify context switching and WIP limits

#### 8. Context Switching Index
- **Description**: Number of distinct projects with merged MRs
- **Unit**: count
- **Direction**: Context-dependent (can indicate flexibility or distraction)
- **Use Case**: Assess focus vs. distribution

### Example Request
```bash
# 30-day flow metrics
curl "http://localhost:5000/api/v1/123/metrics/flow?windowDays=30"
```

### Example Response
```json
{
  "userId": 123,
  "username": "john.doe",
  "windowDays": 30,
  "windowStart": "2024-09-06T10:30:00Z",
  "windowEnd": "2024-10-06T10:30:00Z",
  "mergedMrsCount": 15,
  "linesChanged": 3450,
  "codingTimeMedianH": 12.5,
  "timeToFirstReviewMedianH": 4.2,
  "reviewTimeMedianH": null,
  "mergeTimeMedianH": 48.3,
  "wipOpenMrsCount": 2,
  "contextSwitchingIndex": 3,
  "projects": [
    {
      "projectId": 100,
      "projectName": "web-api",
      "mergedMrCount": 10
    }
  ]
}
```

---

## MR Cycle Time Metrics

### Endpoint
```
GET /api/v1/{userId}/metrics/mr-cycle-time
```

### Metrics Calculated

#### MR Cycle Time (P50 and P90)
- **Description**: Time from first commit to merge
- **Formula**: `percentile(merged_at - first_commit_at)`
- **Unit**: hours
- **Direction**: ↓ good (faster delivery)
- **Percentiles**:
  - P50 (median): Typical cycle time
  - P90: Upper bound for most MRs
- **Use Case**: Track delivery speed and identify outliers

### Example Request
```bash
# 90-day MR cycle time
curl "http://localhost:5000/api/v1/123/metrics/mr-cycle-time?windowDays=90"
```

### Example Response
```json
{
  "userId": 123,
  "username": "john.doe",
  "windowDays": 30,
  "windowStart": "2024-09-06T10:30:00Z",
  "windowEnd": "2024-10-06T10:30:00Z",
  "mrCycleTimeP50H": 48.5,
  "mrCycleTimeP90H": 120.2,
  "mergedMrCount": 15,
  "excludedMrCount": 2,
  "projects": [
    {
      "projectId": 100,
      "projectName": "web-api",
      "mergedMrCount": 10
    }
  ]
}
```

---

## Quality & Reliability Metrics

### Endpoint
```
GET /api/v1/{userId}/metrics/quality
```

### Metrics Calculated

#### 1. Rework Ratio
- **Description**: Percentage of MRs requiring additional commits after review started
- **Formula**: `(MRs_with_commits_after_first_review) / merged_mrs`
- **Unit**: ratio [0..1]
- **Direction**: ↓ good (fewer revisions needed)
- **Use Case**: Measure code quality and review effectiveness

#### 2. Revert Rate
- **Description**: Percentage of merged MRs that were later reverted
- **Formula**: `(MRs_with_revert_in_title) / merged_mrs`
- **Unit**: ratio [0..1]
- **Direction**: ↓ good (fewer reversions)
- **Detection**: Checks MR title for "revert" or "Revert" patterns
- **Use Case**: Track production issues and code stability

#### 3. CI Success Rate
- **Description**: First-time pipeline success rate
- **Formula**: `(successful_pipelines_first_run) / total_pipelines`
- **Unit**: percentage
- **Direction**: ↑ good (fewer test failures)
- **Use Case**: Identify test reliability issues

#### 4. Pipeline Duration (P50, P95)
- **Description**: Build/test time percentiles
- **Formula**: `percentile(pipeline_updated_at - pipeline_created_at)`
- **Unit**: minutes
- **Direction**: ↓ good (faster feedback)
- **Use Case**: Optimize CI/CD performance

#### 5. Test Coverage
- **Description**: Coverage percentage from pipeline reports
- **Unit**: percentage
- **Direction**: ↑ good (better test coverage)
- **Status**: Placeholder (returns null, requires pipeline details API)

#### 6. Hotfix Rate
- **Description**: Percentage of MRs identified as hotfixes
- **Formula**: `(hotfix_MRs) / merged_mrs`
- **Unit**: ratio [0..1]
- **Direction**: ↓ good (fewer urgent fixes)
- **Detection Heuristics**:
  - Labels contain "hotfix" (case-insensitive)
  - Title contains "hotfix" (case-insensitive)
  - Branch name contains "hotfix" or "hot-fix"

#### 7. Merge Conflicts Frequency
- **Description**: Frequency of conflict resolution needed
- **Unit**: ratio [0..1]
- **Direction**: ↓ good (better coordination)
- **Implementation**: Uses GitLab API `has_conflicts` field

### Example Request
```bash
# Quality metrics with custom revert detection window
curl "http://localhost:5000/api/v1/123/metrics/quality?windowDays=30&revertDetectionDays=30"
```

### Example Response
```json
{
  "userId": 123,
  "username": "developer",
  "windowDays": 30,
  "windowStart": "2024-09-12T10:20:41.021Z",
  "windowEnd": "2024-10-12T10:20:41.021Z",
  "mergedMrCount": 10,
  "reworkRatio": 0.3,
  "reworkMrCount": 3,
  "revertRate": 0.1,
  "revertedMrCount": 1,
  "revertDetectionDays": 30,
  "ciSuccessRate": 0.75,
  "successfulPipelinesFirstRun": 15,
  "totalFirstRunPipelines": 20,
  "pipelineDurationP50Min": 8.5,
  "pipelineDurationP95Min": 15.2,
  "pipelinesWithDurationCount": 20,
  "testCoveragePercent": null,
  "pipelinesWithCoverageCount": 0,
  "hotfixRate": 0.2,
  "hotfixMrCount": 2,
  "conflictRate": 0.1,
  "conflictMrCount": 1,
  "projects": [
    {
      "projectId": 456,
      "projectName": "web-app",
      "mergedMrCount": 7,
      "pipelineCount": 15
    }
  ]
}
```

---

## Code Characteristics Metrics

### Endpoint
```
GET /api/v1/{userId}/metrics/code-characteristics
```

### Metrics Calculated

#### 1. Commit Frequency
- **Description**: Commits per day/week and distinct commit days
- **Unit**: 
  - `commitsPerDay`: Average commits per day
  - `commitsPerWeek`: Average commits per week
  - `commitDaysCount`: Number of distinct days with commits
- **Direction**: Context-dependent
- **Use Case**: Understand developer activity patterns

#### 2. Commit Size Distribution
- **Description**: Lines per commit statistics
- **Unit**: lines
- **Metrics**:
  - `commitSizeMedian`: P50 lines changed per commit
  - `commitSizeP95`: P95 lines changed per commit
  - `commitSizeAverage`: Average lines changed per commit
- **Direction**: Smaller commits often preferred
- **Use Case**: Encourage atomic commits

#### 3. MR Size Distribution
- **Description**: Categorizes MRs by size (S/M/L/XL)
- **Thresholds** (configurable):
  - Small: < 100 lines
  - Medium: 100-500 lines
  - Large: 500-1000 lines
  - XL: > 1000 lines
- **Direction**: More small MRs = ↑ good
- **Use Case**: Promote reviewable MR sizes

#### 4. File Churn Analysis
- **Description**: Most frequently modified files
- **Status**: Not implemented (performance considerations)
- **Reason**: Would require one API call per commit
- **Future**: Consider with caching or background processing

#### 5. Squash vs. Merge Strategy
- **Description**: Usage of squash merge vs. regular merge
- **Unit**: percentage
- **Metrics**:
  - `squashMergeRate`: Percentage of MRs using squash
  - `squashedMrsCount`: Count of squash-merged MRs
- **Direction**: Context-dependent
- **Use Case**: Track merge strategy preferences

#### 6. Commit Message Quality
- **Description**: Message length and conventional commit compliance
- **Metrics**:
  - `averageCommitMessageLength`: Average length in characters
  - `conventionalCommitRate`: % following conventional format
  - `conventionalCommitsCount`: Count of conventional commits
- **Pattern**: `(feat|fix|refactor|docs|test|chore|style|perf|ci|build|revert)(:|\\()`
- **Direction**: ↑ good (better documentation)

#### 7. Branch Naming Patterns
- **Description**: Adherence to branch naming conventions
- **Metrics**:
  - `branchNamingComplianceRate`: Percentage of compliant branches
  - `compliantBranchesCount`: Count of compliant branches
- **Default Patterns**:
  - `feature/*`, `feat/*`
  - `bugfix/*`, `fix/*`
  - `hotfix/*`, `hf/*`
  - `release/*`, `rel/*`
  - `chore/*`, `task/*`
  - `refactor/*`, `refac/*`
- **Direction**: ↑ good (better organization)

### Example Request
```bash
# Code characteristics for 30 days
curl "http://localhost:5000/api/v1/123/metrics/code-characteristics?windowDays=30"
```

### Example Response
```json
{
  "userId": 1,
  "username": "john.doe",
  "windowDays": 30,
  "windowStart": "2025-09-13T00:00:00Z",
  "windowEnd": "2025-10-13T00:00:00Z",
  "commitsPerDay": 2.5,
  "commitsPerWeek": 17.5,
  "totalCommits": 75,
  "commitDaysCount": 22,
  "commitSizeMedian": 45.0,
  "commitSizeP95": 320.0,
  "commitSizeAverage": 85.5,
  "mrSizeDistribution": {
    "small": { "count": 8, "percentage": 50.0 },
    "medium": { "count": 5, "percentage": 31.25 },
    "large": { "count": 2, "percentage": 12.5 },
    "xl": { "count": 1, "percentage": 6.25 }
  },
  "totalMergedMrs": 16,
  "topFilesByChurn": [],
  "squashMergeRate": 0.65,
  "squashedMrsCount": 10,
  "averageCommitMessageLength": 42.5,
  "conventionalCommitRate": 0.75,
  "conventionalCommitsCount": 56,
  "branchNamingComplianceRate": 0.85,
  "compliantBranchesCount": 13,
  "projects": [
    {
      "projectId": 100,
      "projectName": "backend-api",
      "commitCount": 50,
      "mergedMrCount": 10
    }
  ]
}
```

---

## Pipeline & CI/CD Metrics

### Endpoint
```
GET /api/v1/metrics/pipelines/{projectId}
```

### Metrics Calculated

#### 1. Failed Job Rate
- **Description**: Most frequently failing pipeline jobs
- **Unit**: failure count and rate per job
- **Direction**: ↓ good (fewer failures)
- **Returns**: Top 10 most failing jobs
- **Use Case**: Identify flaky tests

#### 2. Pipeline Retry Rate
- **Description**: Pipelines requiring manual retry
- **Formula**: `(pipelines_with_multiple_runs_same_sha) / total_unique_shas`
- **Unit**: percentage
- **Direction**: ↓ good (fewer retries needed)
- **Use Case**: Measure pipeline reliability

#### 3. Pipeline Wait Time
- **Description**: Queue time before pipeline starts
- **Formula**: `percentile(started_at - created_at)`
- **Unit**: minutes (P50 and P95)
- **Direction**: ↓ good (faster start times)
- **Use Case**: Identify runner capacity issues

#### 4. Deployment Frequency
- **Description**: Merges to main/production branches (DORA metric)
- **Unit**: count per period
- **Direction**: ↑ good (more frequent deployments)
- **Branches**: main, master, production
- **DORA Benchmarks**:
  - Elite: Multiple per day
  - High: Daily to weekly
  - Medium: Weekly to monthly
  - Low: Less than monthly

#### 5. Job Duration Trends
- **Description**: Track duration changes to detect degradation
- **Unit**: minutes with trend indicator
- **Trends**: improving, stable, degrading
- **Analysis**: Compares first half vs second half of time window
- **Threshold**: >10% change flags as improving/degrading
- **Returns**: Top 10 longest running jobs

#### 6. Pipeline Success Rate by Branch Type
- **Description**: Success rate comparison between main and feature branches
- **Unit**: percentage by category
- **Categories**:
  - Main branches: main/master/production
  - Feature branches: all others
- **Direction**: ↑ good (higher success rates)

#### 7. Coverage Trend
- **Description**: Test coverage change over time
- **Unit**: percentage with trend indicator
- **Trends**: improving, stable, degrading
- **Requirements**: At least 4 pipelines with coverage data
- **Direction**: ↑ good (increasing coverage)

### Example Request
```bash
# Pipeline metrics for project
curl "http://localhost:5000/api/v1/metrics/pipelines/123?windowDays=30"
```

### Example Response
```json
{
  "projectId": 123,
  "projectName": "web-app",
  "windowDays": 30,
  "windowStart": "2024-09-12T10:20:41.021Z",
  "windowEnd": "2024-10-12T10:20:41.021Z",
  "failedJobs": [
    {
      "jobName": "integration-tests",
      "failureCount": 15,
      "totalRuns": 50,
      "failureRate": 0.3
    }
  ],
  "pipelineRetryRate": 0.12,
  "retriedPipelineCount": 6,
  "totalPipelineCount": 50,
  "pipelineWaitTimeP50Min": 2.5,
  "pipelineWaitTimeP95Min": 8.2,
  "pipelinesWithWaitTimeCount": 48,
  "deploymentFrequency": 10,
  "jobDurationTrends": [
    {
      "jobName": "integration-tests",
      "averageDurationMin": 12.5,
      "durationP50Min": 11.2,
      "durationP95Min": 18.5,
      "trend": "degrading",
      "runCount": 50
    }
  ],
  "branchTypeMetrics": {
    "mainBranchSuccessRate": 0.95,
    "mainBranchSuccessCount": 19,
    "mainBranchTotalCount": 20,
    "featureBranchSuccessRate": 0.87,
    "featureBranchSuccessCount": 26,
    "featureBranchTotalCount": 30
  },
  "averageCoveragePercent": 82.5,
  "coverageTrend": "improving",
  "pipelinesWithCoverageCount": 48
}
```

---

## Advanced Metrics

### Endpoint
```
GET /api/v1/{userId}/metrics/advanced
```

### Metrics Calculated

#### 1. Bus Factor (Code Ownership Concentration)
- **Description**: Distribution of code ownership using Gini coefficient
- **Formula**: Gini coefficient (0-1) from file modifications per developer
- **Unit**: score (0-1) + top 3 developers percentage
- **Direction**: ↓ good (more distributed = less risk)
- **Interpretation**:
  - 0.0 = Perfectly distributed ownership
  - 1.0 = Single person owns everything
  - Top 3 percentage shows concentration of changes
- **Use Case**: Risk assessment and knowledge distribution

#### 2. Response Time Distribution
- **Description**: When developers respond to code reviews by hour (0-23)
- **Unit**: Distribution dictionary (hour → count)
- **Returns**:
  - `ResponseTimeDistribution`: Hour-by-hour counts
  - `PeakResponseHour`: Hour with most activity
  - `TotalReviewResponses`: Total responses analyzed
- **Direction**: Context-dependent
- **Use Case**: Understanding work patterns

#### 3. Batch Size (Commits per MR)
- **Description**: Number of commits per merge request
- **Formula**: P50 (median) and P95 percentile
- **Unit**: count
- **Direction**: Context-dependent
- **Use Case**: 
  - Very high = possible squash candidate
  - Very low = may lack iterative refinement

#### 4. Draft Duration
- **Description**: Time merge requests spend in draft/WIP state
- **Formula**: Median time in draft state
- **Unit**: hours
- **Direction**: Context-dependent
- **Detection**:
  - `WorkInProgress` flag
  - Title prefixes: `Draft:`, `WIP:`
  - System notes for draft state changes

#### 5. Iteration Count
- **Description**: Number of review cycles per merge request
- **Formula**: Count of review → changes → re-review cycles
- **Unit**: count (median)
- **Direction**: ↓ good (fewer iterations = clearer requirements)
- **Calculation**: Tracks sequence of review comments followed by commits

#### 6. Idle Time in Review
- **Description**: Time MR waits with no activity after review comments
- **Formula**: Median of gaps between review comments and next activity
- **Unit**: hours
- **Direction**: ↓ good (faster response)
- **Cap**: 30 days to avoid outliers

#### 7. Cross-Team Collaboration Index
- **Description**: Percentage of MRs involving reviewers from other teams
- **Unit**: percentage
- **Direction**: ↑ good (knowledge sharing)
- **Status**: Requires team mapping configuration
- **Returns**: `teamMappingAvailable = false` until configured

### Example Request
```bash
# Advanced metrics for 30 days
curl "http://localhost:5000/api/v1/123/metrics/advanced?windowDays=30"
```

### Example Response
```json
{
  "userId": 123,
  "username": "developer",
  "windowDays": 30,
  "windowStart": "2025-09-15T00:00:00Z",
  "windowEnd": "2025-10-15T00:00:00Z",
  "busFactor": 0.45,
  "contributingDevelopersCount": 8,
  "top3DevelopersFileChangePercentage": 65.5,
  "responseTimeDistribution": {
    "9": 12, "10": 45, "11": 38,
    "14": 52, "15": 41, "16": 25
  },
  "peakResponseHour": 14,
  "totalReviewResponses": 213,
  "batchSizeP50": 3.0,
  "batchSizeP95": 8.0,
  "batchSizeMrCount": 42,
  "draftDurationMedianH": 12.5,
  "draftMrCount": 15,
  "iterationCountMedian": 2.0,
  "iterationMrCount": 38,
  "idleTimeInReviewMedianH": 4.2,
  "idleTimeMrCount": 42,
  "crossTeamCollaborationPercentage": null,
  "crossTeamMrCount": 0,
  "totalMrsForCrossTeam": 42,
  "teamMappingAvailable": false,
  "projects": [
    {
      "projectId": 100,
      "projectName": "backend-api",
      "mrCount": 25,
      "commitCount": 147,
      "fileChangeCount": 8432
    }
  ]
}
```

---

## Commit Time Analysis

### Endpoint
```
GET /api/v1/{userId}/analysis/commit-time
```

### Metrics Calculated

#### Hourly Distribution
- **Description**: Distribution of commits across 24 hours
- **Unit**: Dictionary mapping hour (0-23) to commit count
- **Use Case**: Identify peak coding times

#### Time Period Breakdown
- **Periods**:
  - Night: 0-5 (midnight to 6am)
  - Morning: 6-11 (6am to noon)
  - Afternoon: 12-17 (noon to 6pm)
  - Evening: 18-23 (6pm to midnight)
- **Returns**: Count and percentage for each period
- **Use Case**: Understand work patterns

#### Peak Activity Analysis
- **Metrics**:
  - `peakActivityHour`: Hour with most commits
  - `peakActivityPercentage`: Percentage of commits in peak hour
- **Use Case**: Optimize meeting schedules

### Example Request
```bash
# Commit time analysis for 30 days
curl "http://localhost:5000/api/v1/123/analysis/commit-time?lookbackDays=30"
```

### Example Response
```json
{
  "userId": 123,
  "username": "john.doe",
  "email": "john.doe@company.com",
  "lookbackDays": 30,
  "analysisStartDate": "2025-09-05T00:00:00Z",
  "analysisEndDate": "2025-10-05T00:00:00Z",
  "totalCommits": 156,
  "hourlyDistribution": {
    "0": 2, "1": 1, "8": 15, "9": 22,
    "10": 18, "14": 16, "15": 14
  },
  "timePeriods": {
    "night": 4,
    "morning": 78,
    "afternoon": 61,
    "evening": 13,
    "percentages": {
      "night": 2.56,
      "morning": 50.00,
      "afternoon": 39.10,
      "evening": 8.33
    }
  },
  "projects": [
    {
      "projectId": 456,
      "projectName": "web-app",
      "commitCount": 89
    }
  ],
  "peakActivityHour": 9,
  "peakActivityPercentage": 14.10
}
```

---

## Team & Project Metrics

### Team Metrics Endpoint
```
GET /api/v1/teams/{teamId}/metrics
```

#### Configuration Required
Teams must be configured in `appsettings.json`:

```json
{
  "Metrics": {
    "TeamMapping": {
      "Teams": [
        {
          "Id": "backend-team",
          "Name": "Backend Team",
          "Members": [123, 456, 789]
        }
      ]
    }
  }
}
```

#### Metrics Calculated

**1. Team Velocity**
- Total merged MRs across all members
- Total commits across all projects
- Total lines changed
- Average MR cycle time (P50)

**2. Cross-Project Contributors**
- Count of members working on multiple projects
- Total unique projects touched by team

**3. Team Review Coverage**
- Percentage of MRs with sufficient reviewers
- Configurable minimum reviewer requirement

### Project Metrics Endpoint
```
GET /api/v1/projects/{projectId}/metrics
```

#### Metrics Calculated

**1. Project Activity Score**
- Total commits in window
- Total merged MRs
- Total lines changed
- Unique contributors count

**2. Branch Lifecycle Analysis**
- Long-lived branch count (>30 days)
- Average age of long-lived branches
- List of stale branches with details

**3. Label Usage Distribution**
- Most common labels with frequencies
- Use case: Track categorization patterns

**4. Milestone Completion Rate**
- Percentage completed on time
- Total completed vs. total milestones

**5. Review Coverage**
- Percentage with sufficient reviewers
- Configurable threshold

**6. Cross-Project Contributors**
- Contributors also working elsewhere
- Knowledge distribution metric

### Example Team Response
```json
{
  "teamId": "backend-team",
  "teamName": "Backend Team",
  "memberCount": 3,
  "windowDays": 30,
  "totalMergedMrs": 45,
  "avgMrCycleTimeP50H": 24.5,
  "crossProjectContributors": 2,
  "totalProjectsTouched": 5,
  "teamReviewCoveragePercentage": 88.9,
  "projectActivities": [
    {
      "projectId": 100,
      "projectName": "api-service",
      "mergedMrCount": 25,
      "contributorCount": 3
    }
  ]
}
```

### Example Project Response
```json
{
  "projectId": 100,
  "projectName": "api-service",
  "windowDays": 30,
  "totalCommits": 125,
  "totalMergedMrs": 45,
  "uniqueContributors": 5,
  "longLivedBranchCount": 2,
  "avgLongLivedBranchAgeDays": 45.5,
  "longLivedBranches": [
    {
      "name": "feature/legacy-refactor",
      "ageDays": 60,
      "lastCommitDate": "2025-08-16T10:30:00Z"
    }
  ],
  "labelUsageDistribution": {
    "feature": 20,
    "bugfix": 15
  },
  "milestoneCompletionRate": 75.0,
  "reviewCoveragePercentage": 88.9
}
```

---

## Configuration

### GitLab Connection
```json
{
  "GitLab": {
    "BaseUrl": "https://your-gitlab-instance.com",
    "Token": "your-personal-access-token"
  }
}
```

**Token Requirements:** `api` scope

### Code Characteristics Thresholds
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
      ]
    }
  }
}
```

### Bot Filtering
```json
{
  "Metrics": {
    "Identity": {
      "BotRegexPatterns": [
        "^.*bot$",
        "^.*\\[bot\\]$",
        "^gitlab-ci$",
        "^dependabot.*"
      ]
    }
  }
}
```

### File Exclusions
```json
{
  "Metrics": {
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

---

## Best Practices

### Recommended Time Windows

**Sprint Metrics:** 14 days (2 weeks)
- Daily standups and sprint reviews
- Focus on recent activity

**Monthly Reviews:** 28-30 days
- Individual performance reviews
- Team velocity tracking

**Quarterly Reports:** 90 days
- Strategic planning
- Trend analysis

### Interpreting Metrics

#### Flow Metrics
- **High merged MRs + Low cycle time** = Excellent throughput
- **High WIP + High context switching** = Potential overload
- **Long time to first review** = Review process bottleneck

#### Quality Metrics
- **High rework ratio** = Review process or requirements issues
- **High revert rate** = Code quality or testing gaps
- **Low CI success rate** = Flaky tests or infrastructure issues

#### Code Characteristics
- **Large MR sizes** = Encourage smaller, reviewable changes
- **Low commit message quality** = Implement conventional commits
- **Poor branch naming** = Enforce naming conventions

#### Advanced Metrics
- **High bus factor (>0.7)** = Knowledge concentration risk
- **High iteration count** = Unclear requirements
- **Long idle times** = Review responsiveness issues

### Performance Optimization

**API Rate Limiting:**
- GitLab: ~600 requests/minute for authenticated users
- Monitor rate limit consumption
- Implement caching for frequently accessed data

**Response Time:**
- Shorter windows (7-30 days) = Faster responses
- Large teams/projects = Consider background jobs
- Cache dashboard data (1-3 hour TTL)

**Efficient Queries:**
- Use appropriate time windows
- Avoid very long periods (>90 days) for real-time queries
- Schedule periodic calculations for reports

### Common Pitfalls

**Don't:**
- Use single metrics in isolation
- Compare across different contexts
- Use for individual performance reviews
- Ignore project/team complexity

**Do:**
- Look at the full picture
- Track trends over time
- Consider role differences
- Use for process improvement
- Focus on team health

### Team Comparisons

When comparing metrics:
- Account for project complexity
- Consider team seniority mix
- Look at relative trends, not absolutes
- Use for support identification, not rankings

### Error Handling

**User Not Found (404):**
- Verify user ID exists in GitLab
- Check token permissions

**Empty Results:**
- Verify time window has activity
- Check bot filtering isn't too aggressive
- Confirm user has access to projects

**Slow Responses:**
- Reduce time window
- Check GitLab API performance
- Implement caching

---

## Related Documentation

- **[API Usage Guide](./API_USAGE_GUIDE.md)** - Comprehensive API usage patterns and examples
- **[Configuration Guide](./CONFIGURATION_GUIDE.md)** - Detailed configuration options
- **[Deployment Guide](./DEPLOYMENT_GUIDE.md)** - Deployment and container setup
- **[Operations Runbook](./OPERATIONS_RUNBOOK.md)** - Operational procedures
- **[Endpoint Audit](./ENDPOINT_AUDIT.md)** - Complete endpoint inventory

---

## Version Information

**API Version:** v1  
**Last Updated:** October 2024  
**Stability:** Stable

All endpoints follow semantic versioning through URI-based versioning (`/api/v1/`).
