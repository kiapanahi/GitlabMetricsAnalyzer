# Test Fixtures Documentation

This document describes the deterministic test fixtures created for comprehensive testing of GitLab metrics ingestion and computation.

## Overview

The `GitLabTestFixtures` class provides a complete set of synthetic GitLab data that covers all major edge cases and scenarios encountered in real-world GitLab environments. All fixtures use deterministic data with fixed timestamps and IDs to ensure reproducible test outcomes.

## Design Principles

1. **Deterministic**: All data uses fixed seeds and timestamps
2. **Comprehensive**: Covers all major GitLab entities and edge cases
3. **Realistic**: Data patterns mirror real-world GitLab usage
4. **Interconnected**: Entities reference each other correctly
5. **Edge Case Focused**: Includes problematic scenarios like flaky jobs, reverts, conflicts

## Fixed Base Date

All temporal data is relative to: **January 1, 2024 00:00:00 UTC**

This ensures consistent date calculations across test runs and different environments.

## Test Data Structure

### Users (4 total)

| ID | Username | Role | Type | Description |
|----|----------|------|------|-------------|
| 1 | alice.developer | Developer | Human | Primary developer with various commit patterns |
| 2 | bob.reviewer | Reviewer | Human | Focus on review activities and collaborative work |
| 3 | charlie.maintainer | Maintainer | Human | Admin user handling hotfixes and maintenance |
| 4 | deployment.bot | Bot | External | Automated deployment system (should be excluded) |

### Projects (3 total)

| ID | Name | Status | Owner | Description |
|----|------|--------|--------|-------------|
| 1 | main-service | Active | Alice | Primary production service with full workflows |
| 2 | legacy-system | Active | Charlie | Legacy system with minimal CI/CD |
| 3 | archived-project | Archived | Bob | Archived project for exclusion testing |

### Commits (5 total per project)

#### Edge Cases Covered:
- **Regular commits**: Normal development work
- **Merge commits**: `ParentCount > 1`, merge messages
- **Revert commits**: Commits that undo previous changes
- **Bot commits**: Automated commits (should be excluded)
- **Large refactoring**: High line count changes

```
ID: abc123def456 - Regular development commit
ID: merge456789 - Merge commit (ParentCount=2)
ID: revert789 - Revert commit
ID: bot123456 - Bot commit (excluded)
ID: refactor999 - Large refactoring
```

### Merge Requests (6 total)

#### Edge Cases Covered:
- **Standard MR**: Normal workflow with reviews and merge
- **Draft/WIP MR**: Work in progress, not ready for merge
- **Hotfix MR**: Critical fixes with fast-track approval
- **Revert MR**: Rolling back problematic changes
- **Conflicted MR**: Closed due to merge conflicts
- **Squash MR**: Many commits squashed into one

| ID | Title | State | Pattern | Days to Merge |
|----|-------|-------|---------|---------------|
| 101 | feat: authentication | merged | Standard | 4 days |
| 102 | Draft: experimental | opened | Draft/WIP | - |
| 103 | hotfix: security patch | merged | Hotfix | 2 hours |
| 104 | Revert "authentication" | merged | Revert | 1 hour |
| 105 | feature: permissions | closed | Conflicted | - |
| 106 | refactor: cleanup | merged | Squash (25→1) | 5 days |

### Pipelines (6 total)

#### Edge Cases Covered:
- **Successful pipelines**: Normal CI/CD flow
- **Failed pipelines**: Build or test failures
- **Flaky pipelines**: Initial failure, successful retry
- **Scheduled pipelines**: Automated triggers
- **Different triggers**: push, web, schedule

| ID | SHA | Status | Trigger | Special Notes |
|----|-----|--------|---------|---------------|
| 1001 | abc123def456 | success | push | Standard success |
| 1002 | fail456789 | failed | push | Build failure |
| 1003 | flaky123456 | failed | push | Flaky (retry below) |
| 1004 | flaky123456 | success | web | Successful retry |
| 1005 | scheduled123 | success | schedule | Bot-triggered |
| 2001 | legacy456789 | success | push | Legacy project |

### Jobs (6 total)

#### Edge Cases Covered:
- **Successful jobs**: Normal execution
- **Failed jobs**: Test or build failures
- **Retried jobs**: `RetriedFlag = true`
- **Different stages**: build, test, deploy

| ID | Pipeline | Name | Status | Retry | Duration |
|----|----------|------|--------|-------|----------|
| 10001 | 1001 | unit-tests | success | No | 120s |
| 10003 | 1003 | integration-tests | failed | No | 180s |
| 10004 | 1004 | integration-tests | success | Yes | 175s |
| 10005 | 1001 | compile | success | No | 90s |
| 10006 | 1002 | deploy-staging | failed | No | 30s |
| 20001 | 2001 | test | success | No | 60s |

### Notes/Discussions (6 total)

#### Edge Cases Covered:
- **Review comments**: Human feedback on code
- **System notes**: Automated events (approvals, merges)
- **Discussion threads**: Back-and-forth conversations
- **Resolved discussions**: Completed review topics

| ID | MR | Type | Status | Author | Content Type |
|----|----|----- |--------|--------|--------------|
| 2001 | 101 | Review | Open | Bob | Code feedback |
| 2002 | 101 | System | - | Bob | Approval event |
| 2003 | 101 | Response | - | Alice | Author response |
| 2004 | 101 | Review | Resolved | Bob | Resolution |
| 2005 | 103 | Review | - | Bob | Hotfix approval |
| 2006 | 103 | System | - | Charlie | Merge event |

## Key Test Scenarios

### 1. Ingestion Edge Cases

- **Bot Detection**: User ID 4 should be excluded from metrics
- **Merge Commit Filtering**: ParentCount > 1 indicates merge commits
- **Revert Detection**: Title and message pattern matching
- **Hotfix Identification**: Branch naming and label patterns
- **Draft Handling**: "Draft:" prefix and WIP patterns
- **Conflict Resolution**: HasConflicts flag and closed state

### 2. Flaky Behavior Detection

- **Pipeline Flakiness**: Same SHA with different outcomes
- **Job Retries**: RetriedFlag and different pipeline IDs
- **Timing Analysis**: Failed job followed by successful retry

### 3. Metric Calculations

#### For Alice (ID=1):
- **Expected Commits**: 2 (excludes bot commits, includes regular + refactoring)
- **Expected Lines Added**: 950 (150 + 800)
- **Expected Lines Deleted**: 675 (25 + 650)
- **Expected MRs**: 3 (standard, draft, conflicted)
- **Expected Merged MRs**: 1 (only standard MR)

#### For Bob (ID=2):
- **Role**: Primarily reviewer
- **Expected Commits**: 1 (merge commit)
- **Expected MRs**: 1 (squash MR)

#### For Charlie (ID=3):
- **Role**: Maintainer
- **Expected MRs**: 2 (hotfix + revert)
- **Expected Fast-track**: Hotfix merged in 2 hours

### 4. Data Quality Validation

- **Sufficient Data**: Alice should have good data quality
- **Low Data**: Bot user should trigger low data flags
- **Audit Trails**: All entities include IngestedAt timestamps

## Usage in Tests

### Basic Usage
```csharp
var fixtures = GitLabTestFixtures.CompleteFixture;
await dbContext.RawCommits.AddRangeAsync(fixtures.Commits);
await dbContext.SaveChangesAsync();
```

### Individual Entity Access
```csharp
var users = GitLabTestFixtures.CreateTestUsers();
var commits = GitLabTestFixtures.CreateTestCommits();
var mergeRequests = GitLabTestFixtures.CreateTestMergeRequests();
```

### Date-Relative Testing
```csharp
var baseDate = GitLabTestFixtures.FixedBaseDate; // 2024-01-01
var options = new MetricsComputationOptions
{
    EndDate = baseDate,
    WindowDays = 30
};
```

## Test Categories

### 1. Integration Tests
- **IngestionEdgeCaseTests**: Validates data ingestion and enrichment
- **MetricVerificationTests**: Verifies metric calculation accuracy

### 2. Unit Tests
- **ServiceEdgeCaseTests**: Tests individual service edge cases
- **Performance Tests**: Large dataset handling
- **Concurrency Tests**: Multi-threaded access patterns

## Maintenance

When adding new edge cases:

1. **Add to Fixtures**: Update `GitLabTestFixtures` with new patterns
2. **Document Here**: Update this documentation
3. **Add Tests**: Create tests that verify the new edge case
4. **Verify Determinism**: Ensure new data doesn't break reproducibility

## Expected Test Coverage

The fixtures should enable testing of:

- ✅ Bot user detection and exclusion
- ✅ Merge commit identification and filtering
- ✅ Revert pattern detection
- ✅ Hotfix fast-track workflows
- ✅ Draft MR handling
- ✅ Merge conflict scenarios
- ✅ Squash merge processing
- ✅ Pipeline flakiness detection
- ✅ Job retry identification
- ✅ Discussion resolution tracking
- ✅ Large dataset performance
- ✅ Concurrent access safety
- ✅ Data quality auditing
- ✅ Metric calculation accuracy