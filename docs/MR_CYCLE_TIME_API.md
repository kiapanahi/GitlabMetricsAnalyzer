# MR Cycle Time Metrics API

## Overview

The MR Cycle Time API provides real-time calculation of merge request cycle time metrics for individual developers by fetching live data from your GitLab server. This metric measures the median (P50) and 90th percentile (P90) time from the first commit in a merge request to when it gets merged.

## Formula

```
MR Cycle Time (P50) = median(merged_at - first_commit_at) for merged MRs
MR Cycle Time (P90) = 90th percentile(merged_at - first_commit_at) for merged MRs
```

Where:
- `merged_at`: Timestamp when the MR was merged
- `first_commit_at`: Timestamp of the first commit in the MR
- Unit: Hours
- Percentiles: P50 (median), P90 (90th percentile)

## Endpoint

### GET /api/v1/{userId}/metrics/mr-cycle-time

Calculates the median (P50) and 90th percentile (P90) MR cycle time for a specific developer.

**Path Parameters:**
- `userId` (required): GitLab user ID (long integer)

**Query Parameters:**
- `windowDays` (optional): Number of days to look back (default: 30, min: 1, max: 365)

**Example Request:**
```bash
# Calculate MR cycle time for user 123 over the last 30 days
curl "http://localhost:5000/api/v1/123/metrics/mr-cycle-time"

# Custom time window
curl "http://localhost:5000/api/v1/123/metrics/mr-cycle-time?windowDays=90"
```

**Response Schema:**
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
    },
    {
      "projectId": 200,
      "projectName": "mobile-app",
      "mergedMrCount": 5
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
- `mrCycleTimeP50H`: Median MR cycle time in hours (null if no valid data)
- `mergedMrCount`: Number of merged MRs included in calculation
- `excludedMrCount`: Number of MRs excluded due to missing data
- `projects`: List of projects with MR counts

## Data Source

This endpoint fetches **live data** from GitLab APIs:
1. Retrieves projects the user has contributed to
2. Fetches merge requests for each project
3. For each merged MR, fetches commits to find the first commit timestamp
4. Calculates the time difference between first commit and merge
5. Computes the median across all MRs

## Use Cases

### Team Performance Review
```bash
# Calculate cycle time for all team members
for user_id in 123 456 789; do
  curl "http://localhost:5000/api/v1/$user_id/metrics/mr-cycle-time?windowDays=90" | jq '.'
done
```

### Individual Developer Dashboard
```bash
# Get developer's MR cycle time with project breakdown
curl "http://localhost:5000/api/v1/123/metrics/mr-cycle-time" | \
  jq '{username, cycleTime: .mrCycleTimeP50H, projects: .projects}'
```

### Quarterly Metrics Report
```bash
# Calculate quarterly metrics
curl "http://localhost:5000/api/v1/123/metrics/mr-cycle-time?windowDays=90" | \
  jq '{
    user: .username,
    quarter: "Q4 2024",
    cycleTimeHours: .mrCycleTimeP50H,
    cycleTimeDays: (.mrCycleTimeP50H / 24 | round),
    totalMRs: .mergedMrCount
  }'
```

## Performance Considerations

- **API Calls**: This endpoint makes multiple GitLab API calls:
  - 1 call to get user details
  - 1 call to get contributed projects
  - N calls to get MRs (one per project)
  - M calls to get commits (one per merged MR)
  
- **Response Time**: Depends on:
  - Number of projects the user has contributed to
  - Number of merged MRs in the time window
  - GitLab server response time

- **Recommendation**: 
  - Use reasonable `windowDays` values (30-90 days)
  - Cache results for dashboard displays
  - Schedule periodic calculations rather than real-time requests

## Edge Cases

### No Merged MRs
If the user has no merged MRs in the time window:
```json
{
  "userId": 123,
  "username": "john.doe",
  "windowDays": 30,
  "windowStart": "2024-09-06T10:30:00Z",
  "windowEnd": "2024-10-06T10:30:00Z",
  "mrCycleTimeP50H": null,
  "mergedMrCount": 0,
  "excludedMrCount": 0,
  "projects": []
}
```

### Missing First Commit Timestamp
MRs without accessible commit history are excluded from calculation:
```json
{
  "mrCycleTimeP50H": 48.5,
  "mergedMrCount": 10,
  "excludedMrCount": 3
}
```

## Error Responses

### User Not Found
```bash
HTTP 404 Not Found
{
  "error": "User with ID 99999 not found"
}
```

### Invalid Parameters
```bash
HTTP 400 Bad Request
{
  "error": "windowDays must be greater than 0"
}
```

### Server Error
```bash
HTTP 500 Internal Server Error
{
  "detail": "Failed to fetch merge requests from GitLab",
  "title": "Error calculating MR cycle time",
  "statusCode": 500
}
```

## Best Practices

1. **Choose Appropriate Time Windows**
   - Use 30 days for recent performance
   - Use 90 days for quarterly reviews
   - Avoid very long windows (>365 days) for better performance

2. **Interpret Results in Context**
   - Compare cycle time across similar project types
   - Consider team size and project complexity
   - Look at trends over time, not absolute values

3. **Handle Null Results**
   - `mrCycleTimeP50H: null` means no valid data
   - Check `excludedMrCount` to understand data quality
   - Verify user has contributed to projects in the window

4. **Rate Limiting**
   - Be mindful of GitLab API rate limits
   - Implement caching for frequently accessed data
   - Schedule background jobs for regular calculations

## Related Metrics

- **Time to First Review**: Time from MR creation to first review
- **Time in Review**: Time from first review to merge
- **WIP Age**: Age of work-in-progress MRs
- **Pipeline Success Rate**: Percentage of successful CI/CD runs

## API Version

- **Version**: 1.0
- **Status**: Stable
- **Last Updated**: October 2024
