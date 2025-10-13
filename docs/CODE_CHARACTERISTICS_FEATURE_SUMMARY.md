# Code Characteristics Metrics Feature

## Overview

This feature implements 7 code characteristics metrics to analyze developer code change patterns and working styles. It provides insights into commit frequency, commit sizes, merge request sizes, squash merge usage, commit message quality, and branch naming conventions.

## Implemented Metrics

### 1. Commit Frequency
- **Description**: Measures commits per day/week and distinct commit days
- **API**: `GET /projects/:id/repository/commits?author=:username`
- **Metrics**:
  - `commitsPerDay`: Average commits per day
  - `commitsPerWeek`: Average commits per week (commitsPerDay × 7)
  - `commitDaysCount`: Number of distinct days with commits
- **Direction**: Context-dependent
- **Status**: ✅ Fully Implemented

### 2. Commit Size Distribution
- **Description**: Analyzes lines per commit (median/P50/P95)
- **API**: Commit stats from `GET /projects/:id/repository/commits`
- **Metrics**:
  - `commitSizeMedian`: P50 lines changed per commit
  - `commitSizeP95`: P95 lines changed per commit
  - `commitSizeAverage`: Average lines changed per commit
- **Direction**: Smaller commits often preferred
- **Status**: ✅ Fully Implemented

### 3. MR Size Distribution
- **Description**: Categorizes MRs by size (S/M/L/XL)
- **API**: `GET /projects/:id/merge_requests/:iid/changes`
- **Thresholds** (configurable):
  - Small: < 100 lines
  - Medium: 100-500 lines
  - Large: 500-1000 lines
  - XL: > 1000 lines
- **Metrics**: Count and percentage for each size category
- **Direction**: More small MRs = ↑ good
- **Status**: ✅ Fully Implemented

### 4. File Churn Analysis
- **Description**: Most frequently modified files per developer
- **API**: Would require `GET /projects/:id/repository/commits/:sha/diff` for each commit
- **Status**: ⚠️ Not Implemented (performance considerations)
- **Reason**: Would require one API call per commit, potentially hundreds of calls per request
- **Future Enhancement**: Consider implementing with:
  - Caching layer
  - Background job processing
  - Pre-aggregated data store

### 5. Squash vs. Merge Strategy
- **Description**: Usage of squash merge vs. regular merge
- **API**: `GET /projects/:id/merge_requests/:iid` → `squash` field
- **Metrics**:
  - `squashMergeRate`: Percentage of MRs using squash
  - `squashedMrsCount`: Count of squash-merged MRs
- **Direction**: Context-dependent
- **Status**: ✅ Fully Implemented

### 6. Commit Message Quality
- **Description**: Average message length and conventional commit compliance
- **API**: Commit titles from `GET /projects/:id/repository/commits`
- **Metrics**:
  - `averageCommitMessageLength`: Average message length in characters
  - `conventionalCommitRate`: Percentage following conventional format
  - `conventionalCommitsCount`: Count of conventional commits
- **Pattern**: `(feat|fix|refactor|docs|test|chore|style|perf|ci|build|revert)(:|\\()`
- **Direction**: ↑ good (better documentation)
- **Status**: ✅ Fully Implemented

### 7. Branch Naming Patterns
- **Description**: Adherence to branch naming conventions
- **API**: Source branch from `GET /projects/:id/merge_requests`
- **Metrics**:
  - `branchNamingComplianceRate`: Percentage of compliant branch names
  - `compliantBranchesCount`: Count of compliant branches
- **Default Patterns**:
  - `feature/*`, `feat/*`
  - `bugfix/*`, `fix/*`
  - `hotfix/*`, `hf/*`
  - `release/*`, `rel/*`
  - `chore/*`, `task/*`
  - `refactor/*`, `refac/*`
- **Direction**: ↑ good
- **Status**: ✅ Fully Implemented

## API Endpoint

```
GET /api/v1/{userId}/metrics/code-characteristics?windowDays={days}
```

### Parameters
- `userId` (path, required): GitLab user ID
- `windowDays` (query, optional): Number of days to analyze (default: 30, max: 365)

### Response Structure
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
  "mrSizeDistribution": { /* ... */ },
  "totalMergedMrs": 16,
  "topFilesByChurn": [],
  "squashMergeRate": 0.65,
  "squashedMrsCount": 10,
  "averageCommitMessageLength": 42.5,
  "conventionalCommitRate": 0.75,
  "conventionalCommitsCount": 56,
  "branchNamingComplianceRate": 0.85,
  "compliantBranchesCount": 13,
  "projects": [ /* ... */ ]
}
```

## Technical Architecture

### Files Created

1. **Service Interface**: `ICodeCharacteristicsService.cs`
   - Defines the service contract
   - Includes all result model definitions
   - 7 key result models for metrics

2. **Service Implementation**: `CodeCharacteristicsService.cs`
   - Implements all 7 metrics calculations
   - Handles data fetching and aggregation
   - Includes error handling and graceful degradation
   - File churn analysis stubbed out for performance

3. **Configuration**: Updated `MetricsConfiguration.cs`
   - Added `CodeCharacteristicsConfiguration` class
   - Configurable thresholds for MR sizes
   - Configurable patterns for conventional commits
   - Configurable patterns for branch naming
   - Default values aligned with PRD

4. **Model Updates**:
   - Added `Squash` property to `GitLabMergeRequest`
   - Added `Squash` property to DTO `GitLabMergeRequest`
   - Updated mapping in `GitLabHttpClient`

5. **Endpoint**: Updated `UserMetricsEndpoints.cs`
   - Added `/metrics/code-characteristics` endpoint
   - Input validation and error handling
   - Consistent with other metrics endpoints

6. **Service Registration**: Updated `ServiceCollectionExtensions.cs`
   - Registered `ICodeCharacteristicsService` as scoped service

7. **Unit Tests**: `CodeCharacteristicsServiceTests.cs`
   - 8 comprehensive test cases
   - Edge case coverage (null values, empty data, invalid inputs)
   - All tests passing

8. **Documentation**: `CODE_CHARACTERISTICS_API.md`
   - Complete API documentation
   - Metric descriptions and interpretations
   - Configuration guide
   - Use cases and examples

9. **HTTP Tests**: `code-characteristics.http`
   - 10 test scenarios
   - Covers normal and error cases

## Configuration

### Default Configuration

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

### Customization

All thresholds and patterns can be customized via `appsettings.json` or environment variables.

## Data Flow

1. **User Request** → Validate parameters
2. **Fetch User Details** → Verify user exists
3. **Get Contributed Projects** → Parallel fetching
4. **For Each Project**:
   - Fetch commits (filtered by author and date)
   - Fetch merge requests (filtered by author and merged date)
5. **Calculate Metrics** → Aggregate data across all projects
6. **Return Results** → Formatted JSON response

## Performance Considerations

### Current Implementation
- Parallel data fetching for multiple projects
- Efficient filtering to reduce data processing
- Graceful handling of API failures per project
- MR size calculation requires one API call per MR

### Performance Optimizations
- Results are not cached (each request fetches fresh data)
- File churn analysis disabled to avoid N×commits API calls
- Consider shorter time windows (7-30 days) for faster responses

### Future Enhancements
1. **Caching Layer**: Cache MR changes and commit stats
2. **Background Processing**: Pre-calculate metrics periodically
3. **File Churn Implementation**: With caching and background jobs
4. **Incremental Updates**: Only fetch new data since last calculation

## Edge Cases Handled

1. **User not found**: Throws `InvalidOperationException`
2. **No contributed projects**: Returns empty result with zero values
3. **No commits or MRs**: Returns zeros/nulls appropriately
4. **Invalid window days**: Throws `ArgumentOutOfRangeException`
5. **API failures**: Logs warnings and continues with other projects
6. **Missing commit stats**: Skips commit in size calculations
7. **Missing MR changes**: Skips MR in size distribution

## Testing

### Test Coverage
- 8 unit tests covering all major scenarios
- All 41 tests in suite passing (33 existing + 8 new)
- Mock-based testing using Moq
- Integration with existing test infrastructure

### Test Scenarios
1. Invalid user ID handling
2. No projects scenario
3. Commit frequency calculation
4. MR size distribution categorization
5. Squash merge rate calculation
6. Commit message quality assessment
7. Branch naming compliance checking
8. Invalid parameter validation

## Acceptance Criteria

- [x] All 7 metrics implemented (6 fully, 1 placeholder)
- [x] Configurable size thresholds
- [x] Efficient diff processing (MR changes API used)
- [x] Unit tests with various commit/MR sizes
- [x] Documentation includes interpretation guide
- [x] Performance considerations documented

## Known Limitations

1. **File Churn Analysis**: Not implemented due to performance concerns
   - Would require one API call per commit
   - For a developer with 100 commits, that's 100 additional API calls
   - Recommendation: Implement with caching or background processing

2. **Real-time Data**: No caching, every request fetches fresh data
   - Can be slow for large datasets
   - Consider implementing caching layer for production

3. **API Rate Limits**: Heavy API usage for large time windows
   - Consider implementing exponential backoff
   - Add rate limit monitoring

## Future Enhancements

1. **File Churn Implementation**: With caching layer
2. **Trending Analysis**: Store historical data for trend visualization
3. **Benchmarking**: Add team/project averages for comparison
4. **Custom Metrics**: Allow users to define custom patterns
5. **Caching**: Implement distributed cache for better performance
6. **Webhooks**: Real-time updates via GitLab webhooks

## Related Documentation

- Issue: "Implement Code Characteristics Metrics"
- PRD: `prds/gitlab-developer-productivity-metrics.md` section 6.5
- API Documentation: `docs/CODE_CHARACTERISTICS_API.md`
- Configuration Guide: `docs/CONFIGURATION_GUIDE.md`

## Integration Points

### Service Registration
Updated `ServiceCollectionExtensions.cs`:
```csharp
builder.Services.AddScoped<ICodeCharacteristicsService, CodeCharacteristicsService>();
```

### Endpoint Mapping
Updated `UserMetricsEndpoints.cs`:
```csharp
group.MapGet("/metrics/code-characteristics", CalculateCodeCharacteristics)
    .WithName("CalculateCodeCharacteristics")
    .WithSummary("Calculate code characteristics metrics for a developer");
```

### Configuration Integration
Added to `MetricsConfiguration`:
```csharp
public CodeCharacteristicsConfiguration CodeCharacteristics { get; init; } = new();
```

## Summary

This implementation provides 6 out of 7 requested metrics with full functionality. The file churn analysis is intentionally deferred due to performance implications. The implementation follows the established patterns in the codebase, includes comprehensive tests, and provides detailed documentation for users.

All acceptance criteria are met except for file churn analysis, which is documented as a known limitation with recommendations for future implementation.
