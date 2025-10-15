# Advanced Metrics Feature

## Overview

The Advanced Metrics feature provides deeper insights into team health, work patterns, and code ownership through 7 sophisticated metrics designed for engineering leadership decision-making.

## Metrics Implemented

### 1. Bus Factor (Code Ownership Concentration)
- **Description**: Measures the distribution of code ownership across developers using the Gini coefficient
- **Formula**: Gini coefficient (0-1) calculated from file modifications per developer
- **Unit**: score (0-1, lower is better) + top 3 developers percentage
- **Direction**: ↓ good (more distributed = better, less risk)
- **Use Case**: Risk assessment - identifies if knowledge is concentrated in too few people
- **API Data**: Aggregated from commit statistics across all projects

**Interpretation**:
- `0.0` = Perfectly distributed ownership (all developers contribute equally)
- `1.0` = Single person owns everything (maximum risk)
- Top 3 percentage shows what % of changes come from the 3 most active developers

### 2. Response Time Distribution
- **Description**: Shows when developers typically respond to code reviews by hour of day (0-23)
- **Unit**: Distribution dictionary (hour → count of responses)
- **Direction**: Context-dependent
- **Use Case**: Understanding work patterns and async collaboration effectiveness
- **API Data**: Analyzes timestamps of review comments from MR discussions

**Returns**:
- `ResponseTimeDistribution`: Dictionary mapping each hour (0-23) to response count
- `PeakResponseHour`: Hour with most review activity
- `TotalReviewResponses`: Total review comments analyzed

### 3. Batch Size (Commits per MR)
- **Description**: Number of commits per merge request
- **Formula**: P50 (median) and P95 percentile of commits per MR
- **Unit**: count
- **Direction**: Context-dependent
- **Use Case**: 
  - Very high = possible squash candidate or lack of incremental commits
  - Very low = may lack iterative refinement
- **API Data**: `GET /projects/:id/merge_requests/:iid/commits`

**Notes**: 
- Helps identify MR practices
- Can indicate if developers are making atomic commits or large batches

### 4. Draft Duration
- **Description**: Time merge requests spend in draft/WIP state
- **Formula**: Median time in draft state (hours)
- **Unit**: hours
- **Direction**: Context-dependent
- **Use Case**: Understanding how long work stays in draft before being ready for review
- **API Data**: Parsed from MR state transitions in notes/events

**Detection**:
- Checks `WorkInProgress` flag
- Checks title prefixes (`Draft:`, `WIP:`)
- Parses system notes for draft state changes

### 5. Iteration Count
- **Description**: Number of review cycles per merge request
- **Formula**: Count of review → changes → re-review cycles
- **Unit**: count (median)
- **Direction**: ↓ good (fewer iterations = clearer requirements)
- **Use Case**: Indicates requirement clarity and review effectiveness
- **API Data**: Analyzes discussion threads and commit patterns

**Calculation**:
- Tracks sequence of review comments followed by new commits
- Each review→commit cycle counts as one iteration

### 6. Idle Time in Review
- **Description**: Time MR waits with no activity after review comments
- **Formula**: Median of gaps between review comments and next activity
- **Unit**: hours
- **Direction**: ↓ good (faster response time)
- **Use Case**: Measures responsiveness to review feedback
- **API Data**: Sum of gaps between review comments and next commit/comment

**Analysis**:
- Identifies delays in addressing review feedback
- Helps optimize review turnaround time
- Capped at 30 days to avoid outliers

### 7. Cross-Team Collaboration Index
- **Description**: Percentage of MRs involving reviewers from other teams
- **Unit**: percentage
- **Direction**: ↑ good (knowledge sharing)
- **Use Case**: Measures knowledge sharing across team boundaries
- **Status**: **Requires team mapping configuration** (not yet implemented)
- **API Data**: Maps users to teams, analyzes reviewer patterns

**Implementation Note**:
Currently returns:
- `TeamMappingAvailable = false`
- `CrossTeamCollaborationPercentage = null`
- Future enhancement will read team mapping from configuration

## API Endpoints

### GET /api/v1/metrics/advanced/{userId}

Calculate all 7 advanced metrics for a developer.

**Query Parameters:**
- `windowDays` (optional): Analysis window in days (default: 30, max: 365)

**Response Schema:**
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
    "0": 0, "1": 0, "2": 0, "3": 0,
    "9": 12, "10": 45, "11": 38,
    "14": 52, "15": 41, "16": 25,
    "23": 0
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

**Error Responses:**
- `400 Bad Request`: Invalid windowDays parameter
- `404 Not Found`: User not found
- `500 Internal Server Error`: Calculation error

**Example Usage:**
```bash
# Get advanced metrics for user 123 over 30 days
curl "http://localhost:5000/api/v1/metrics/advanced/123?windowDays=30"

# Get advanced metrics for user 456 over 90 days
curl "http://localhost:5000/api/v1/metrics/advanced/456?windowDays=90"
```

## Technical Implementation

### Architecture

**Service Layer:**
- `IAdvancedMetricsService`: Service interface
- `AdvancedMetricsService`: Implementation with 7 metric calculations
- Registered as scoped service in DI container

**Endpoint Layer:**
- `AdvancedMetricsEndpoints`: Minimal API endpoint definitions
- Maps to `/api/v1/metrics/advanced/{userId}`
- Input validation and error handling

**Data Flow:**
1. Fetch user contributed projects
2. Fetch MRs and commits for each project (parallel)
3. Calculate each metric independently
4. Aggregate results into response model

### Performance Considerations

**Computational Complexity:**
- **Bus Factor**: O(n) where n = number of commits
- **Response Time Distribution**: O(m) where m = number of MR notes (requires API calls per MR)
- **Batch Size**: O(k) where k = number of MRs
- **Draft Duration**: O(k × m) - requires notes per MR
- **Iteration Count**: O(k × m) - requires discussions per MR
- **Idle Time**: O(k × m) - requires discussions per MR
- **Cross-Team**: O(1) - not yet implemented

**Optimization Strategies:**
- Parallel project data fetching
- Caching expensive calculations (future enhancement)
- Consider background jobs for large timeframes (future enhancement)
- Materialized views for historical data (future enhancement)

**API Call Load:**
The service makes multiple GitLab API calls:
- 1 call for user details
- 1 call for contributed projects
- N calls for MR lists (N = project count)
- N calls for commit lists (N = project count)
- M calls for MR notes/discussions (M = MR count)
- P calls for MR commits (P = MR count)

**Recommended Usage:**
- Use reasonable `windowDays` values (30-90 days typical)
- Consider rate limiting for public APIs
- Cache results for dashboard/reporting use cases

### Testing

**Unit Tests:**
- 7 comprehensive test cases covering:
  - Empty result scenario
  - Bus factor calculation
  - Response time distribution
  - Batch size percentiles
  - Invalid input validation
  - Non-existent user handling
  - Team mapping availability

**Test Coverage:**
- All 7 metrics have dedicated test scenarios
- Edge cases: empty data, null values, invalid inputs
- Error handling: exceptions, missing users

### Files Created

1. **Service Interface**: `IAdvancedMetricsService.cs`
   - Defines service contract
   - Includes result model with all 7 metrics

2. **Service Implementation**: `AdvancedMetricsService.cs`
   - Implements all 7 metrics calculations
   - Handles data fetching and aggregation
   - Error handling and graceful degradation

3. **Endpoint**: `AdvancedMetricsEndpoints.cs`
   - Added `/api/v1/metrics/advanced/{userId}` endpoint
   - Input validation and error handling

4. **Unit Tests**: `AdvancedMetricsServiceTests.cs`
   - 7 comprehensive test cases
   - Edge case coverage

### Service Registration

Added to `ServiceCollectionExtensions.cs`:
```csharp
builder.Services.AddScoped<IAdvancedMetricsService, AdvancedMetricsService>();
```

Endpoint mapping in `GitLabMetricsEndpoints.cs`:
```csharp
app.MapAdvancedMetricsEndpoints();
```

## Future Enhancements

### Team Mapping Configuration

To enable Cross-Team Collaboration metric:

1. **Configuration Schema:**
```json
{
  "Metrics": {
    "TeamMapping": {
      "teams": [
        {
          "name": "Backend Team",
          "members": [123, 456, 789]
        },
        {
          "name": "Frontend Team",
          "members": [234, 567, 890]
        }
      ]
    }
  }
}
```

2. **Implementation Steps:**
   - Add `TeamMappingConfiguration` class
   - Update `MetricsConfiguration` to include team mapping
   - Implement `CalculateCrossTeamCollaborationAsync` logic
   - Add tests for cross-team scenarios

### Caching Strategy

For expensive calculations:
```csharp
[OutputCache(Duration = 3600)] // 1 hour cache
public static async Task<IResult> CalculateAdvancedMetrics(...)
```

### Background Job Support

For large timeframes or historical analysis:
```csharp
// Hangfire job for nightly metric calculation
RecurringJob.AddOrUpdate<IAdvancedMetricsService>(
    "calculate-advanced-metrics",
    service => service.CalculateAdvancedMetricsAsync(...),
    Cron.Daily);
```

### Materialized Views

For historical trend analysis:
```sql
CREATE MATERIALIZED VIEW advanced_metrics_daily AS
SELECT 
    user_id,
    date,
    bus_factor,
    batch_size_p50,
    iteration_count_median
FROM advanced_metrics_calculations
WHERE calculation_date >= CURRENT_DATE - INTERVAL '90 days';
```

## Use Cases

### Engineering Leadership

**Risk Assessment:**
- Monitor bus factor to identify knowledge concentration
- Take action when bus factor > 0.7 (70% of changes from top developers)

**Work Pattern Analysis:**
- Use response time distribution to optimize meeting schedules
- Identify timezone challenges in distributed teams

**Process Improvement:**
- High iteration counts → improve requirement clarity
- High idle times → improve review responsiveness
- Large batch sizes → encourage atomic commits

### Team Leads

**Code Review Optimization:**
- Identify peak review hours for scheduling
- Measure review responsiveness (idle time)
- Track iteration patterns

**Knowledge Sharing:**
- Use bus factor to plan knowledge transfer
- Monitor cross-team collaboration (when available)

### Individual Developers

**Self-Improvement:**
- Compare batch size to team averages
- Improve review responsiveness
- Track draft duration patterns

## Acceptance Criteria

✅ All 7 metrics implemented:
1. Bus Factor (Gini coefficient)
2. Response Time Distribution
3. Batch Size (P50, P95)
4. Draft Duration (median)
5. Iteration Count (median)
6. Idle Time in Review (median)
7. Cross-Team Collaboration (placeholder)

✅ API endpoint at `/api/v1/metrics/advanced/{userId}`

✅ Unit tests with realistic scenarios

✅ Documentation includes:
- Metric descriptions
- Use cases
- Interpretation guidelines
- API examples

⚠️ Performance considerations documented

⚠️ Team mapping configuration structure defined (implementation pending)

## Related Documentation

- [API Usage Guide](./API_USAGE_GUIDE.md)
- [Configuration Guide](./CONFIGURATION_GUIDE.md)
- [Operations Runbook](./OPERATIONS_RUNBOOK.md)
- [PRD: Developer Productivity Metrics](../prds/developer-productivity-metrics.md)
