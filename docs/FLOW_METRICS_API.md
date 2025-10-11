# Flow & Throughput Metrics API

## Overview

The Flow Metrics API provides comprehensive velocity and throughput measurements for developers, enabling teams to track productivity patterns, identify bottlenecks, and optimize development workflows.

## Formula

This endpoint calculates 8 key flow metrics based on merge request (MR) activity within a configurable time window:

1. **Merged MRs Count**: Total number of merged MRs
2. **Lines Changed**: Total additions + deletions in merged MRs
3. **Coding Time (Median)**: Time from first commit to MR creation
4. **Time to First Review (Median)**: Time from MR creation to first non-author comment
5. **Review Time (Median)**: Time from first review to approval *
6. **Merge Time (Median)**: Time from MR creation to merge **
7. **WIP/Open MRs**: Count of open or draft MRs at snapshot time
8. **Context Switching Index**: Number of distinct projects touched

\* Currently not available without approval API data  
\** Currently calculated as MR created → merged (proxy metric)

## Endpoint

### GET /api/v1/{userId}/metrics/flow

Calculates comprehensive flow and throughput metrics for a specific developer.

**Path Parameters:**
- `userId` (required): GitLab user ID (long integer)

**Query Parameters:**
- `windowDays` (optional): Number of days to look back (default: 30, min: 1, max: 365)

**Example Request:**
```bash
# Calculate flow metrics for user 123 over the last 30 days
curl "http://localhost:5000/api/v1/123/metrics/flow"

# Custom time window (90 days)
curl "http://localhost:5000/api/v1/123/metrics/flow?windowDays=90"

# 14-day sprint metrics
curl "http://localhost:5000/api/v1/123/metrics/flow?windowDays=14"
```

**Response Schema:**
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
    },
    {
      "projectId": 200,
      "projectName": "mobile-app",
      "mergedMrCount": 3
    },
    {
      "projectId": 300,
      "projectName": "shared-lib",
      "mergedMrCount": 2
    }
  ]
}
```

**Response Fields:**
- `userId`: GitLab user ID
- `username`: GitLab username
- `windowDays`: Analysis window in days
- `windowStart`: Start date of analysis window (UTC)
- `windowEnd`: End date of analysis window (UTC)
- `mergedMrsCount`: Total merged merge requests in window
- `linesChanged`: Total lines changed (additions + deletions)
- `codingTimeMedianH`: Median hours from first commit to MR open (null if no data)
- `timeToFirstReviewMedianH`: Median hours from MR open to first review (null if no data)
- `reviewTimeMedianH`: Median hours from first review to approval (null if not available)
- `mergeTimeMedianH`: Median hours from MR creation to merge (null if no data)
- `wipOpenMrsCount`: Count of currently open/draft MRs
- `contextSwitchingIndex`: Number of distinct projects with merged MRs
- `projects`: List of projects with MR counts

## Data Source

### GitLab API Endpoints Used

1. **`GET /users/:user_id`** - Fetch user details
2. **`GET /users/:user_id/contributed_projects`** - Get projects user contributed to
3. **`GET /projects/:id/merge_requests`** - Fetch MRs with state filtering
4. **`GET /projects/:id/merge_requests/:iid/commits`** - Get commits for line counting and timing
5. **`GET /projects/:id/merge_requests/:iid/notes`** - Get review comments for review metrics

### Data Processing

- **Merged MRs**: Filtered by author and merge timestamp within window
- **Open MRs**: Filtered by state (opened/draft) at snapshot time
- **Lines Changed**: Sum of additions + deletions from commit stats
- **Timing Metrics**: Calculated from timestamps, excluding invalid/negative values
- **Medians**: Computed using standard statistical median calculation

## Use Cases

### Sprint Velocity Tracking
```bash
# Track 2-week sprint metrics
curl "http://localhost:5000/api/v1/123/metrics/flow?windowDays=14"
```
Monitor MRs merged, lines changed, and cycle times per sprint.

### Monthly Performance Review
```bash
# Get monthly metrics
curl "http://localhost:5000/api/v1/123/metrics/flow?windowDays=30"
```
Review developer productivity patterns and identify improvement areas.

### Quarterly Metrics Report
```bash
# Analyze 90-day trends
curl "http://localhost:5000/api/v1/123/metrics/flow?windowDays=90"
```
Generate comprehensive productivity reports for quarterly reviews.

### Context Switching Analysis
Compare `contextSwitchingIndex` across team members to identify:
- Developers working across too many projects
- Opportunities for better focus and specialization
- Potential knowledge sharing opportunities

### Bottleneck Identification
Use timing metrics to identify delays:
- High `timeToFirstReviewMedianH` → Review process needs improvement
- High `codingTimeMedianH` → Tasks may be too complex or unclear
- Large `wipOpenMrsCount` → Work-in-progress may be too high

## Performance Considerations

### Response Time
- **Small repos (1-5 projects)**: 2-5 seconds
- **Medium repos (5-15 projects)**: 5-15 seconds
- **Large repos (15+ projects)**: 15-30 seconds

Response time scales with:
- Number of contributed projects
- Number of MRs in the window
- API rate limits

### Optimization Tips
1. Use shorter time windows for faster responses
2. Cache results for frequently accessed users
3. Run during off-peak hours for batch processing
4. Consider using dedicated workers for metric calculation

### Rate Limiting
GitLab API has rate limits (typically 600 requests/minute for authenticated users). This endpoint makes:
- 1 request per project for MRs
- 1 request per merged MR for commits
- 1 request per merged MR for notes

**Example**: For a user with 5 projects and 20 merged MRs, expect ~45 API calls.

## Edge Cases

### No Contributed Projects
```json
{
  "userId": 123,
  "username": "john.doe",
  "windowDays": 30,
  "windowStart": "2024-09-06T10:30:00Z",
  "windowEnd": "2024-10-06T10:30:00Z",
  "mergedMrsCount": 0,
  "linesChanged": 0,
  "codingTimeMedianH": null,
  "timeToFirstReviewMedianH": null,
  "reviewTimeMedianH": null,
  "mergeTimeMedianH": null,
  "wipOpenMrsCount": 0,
  "contextSwitchingIndex": 0,
  "projects": []
}
```

### Missing Commit Data
If commit data is unavailable for an MR:
- Lines changed will be 0 for that MR
- Coding time metric will exclude that MR
- MR still counts toward merged MRs count

### No Review Comments
If no review comments exist:
- `timeToFirstReviewMedianH` will be null
- Other metrics are still calculated normally

### Invalid Timestamps
Negative time differences (e.g., commit after merge) are excluded from median calculations.

## Error Responses

### User Not Found
```json
{
  "error": "User with ID 123 not found"
}
```
**Status**: 404 Not Found

### Invalid Parameters
```json
{
  "error": "windowDays must be greater than 0"
}
```
**Status**: 400 Bad Request

```json
{
  "error": "windowDays cannot exceed 365 days"
}
```
**Status**: 400 Bad Request

### Server Error
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.6.1",
  "title": "Error calculating flow metrics",
  "status": 500,
  "detail": "Connection timeout to GitLab API"
}
```
**Status**: 500 Internal Server Error

## Best Practices

### Recommended Time Windows
- **Sprint metrics**: 14 days (2 weeks)
- **Monthly reviews**: 28-30 days
- **Quarterly reports**: 90 days

### Interpreting Metrics

#### Good Indicators
- **Merged MRs Count**: ↑ Higher throughput
- **Coding Time**: ↓ Faster development
- **Time to First Review**: ↓ Responsive team
- **Merge Time**: ↓ Efficient process
- **WIP MRs**: ↓ Lower work-in-progress

#### Context-Dependent
- **Lines Changed**: Can indicate both productivity and code complexity
- **Context Switching**: Some switching is healthy; too much reduces focus

### Team Comparisons
When comparing metrics across team members:
- Consider role differences (senior vs. junior)
- Account for project complexity
- Look at trends over time, not absolute values
- Use metrics to identify support needs, not for individual performance reviews

### Avoiding Pitfalls
1. **Don't use single metrics in isolation** - Look at the full picture
2. **Consider project context** - Legacy code vs. greenfield projects differ
3. **Account for time zones** - Review timing may be affected by distributed teams
4. **Beware of gaming** - Metrics can be gamed if used for performance evaluations

## Related Metrics

- **MR Cycle Time** (`/api/v1/{userId}/metrics/mr-cycle-time`) - More detailed cycle time analysis
- **Commit Time Analysis** (`/api/v1/{userId}/analysis/commit-time`) - When developers commit code

## Limitations

1. **Review Time Metric**: Requires GitLab approval API data (not currently available)
2. **Merge Time Metric**: Currently uses MR created → merged as a proxy
3. **Bot Detection**: Bot accounts are not automatically excluded (implement via configuration)
4. **File Exclusions**: Vendor/lock files are not automatically excluded from line counts

## Future Enhancements

- [ ] Add approval API integration for accurate review/merge time metrics
- [ ] Implement file pattern exclusions (vendor, lock files)
- [ ] Add bot account detection and filtering
- [ ] Support for custom metric aggregations (P75, P90)
- [ ] Add sparkline data for trend visualization
- [ ] Include MR size distribution metrics
- [ ] Add revert/rework metrics

## API Version

**Current Version**: v1  
**Introduced**: October 2024  
**Stability**: Stable
