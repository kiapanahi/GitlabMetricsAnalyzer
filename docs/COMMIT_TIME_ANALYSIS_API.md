# Commit Time Distribution Analysis API

## Overview

The Commit Time Distribution Analysis feature provides insights into when developers are making commits throughout the day. This feature fetches commits directly from GitLab (not from the database) and analyzes their distribution across the 24 hours of the day.

## Endpoint

### GET `/api/analysis/commit-time/{userId}`

Analyzes the distribution of commits across 24 hours for a specific GitLab user.

**Path Parameters:**
- `userId` (long, required): The GitLab user ID to analyze

**Query Parameters:**
- `lookbackDays` (int, optional): Number of days to look back. Default: 30, Min: 1, Max: 365

**Response:** `CommitTimeDistributionAnalysis` object

## Response Schema

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
    "0": 2,
    "1": 1,
    "2": 0,
    "3": 0,
    "4": 0,
    "5": 1,
    "6": 3,
    "7": 8,
    "8": 15,
    "9": 22,
    "10": 18,
    "11": 12,
    "12": 8,
    "13": 10,
    "14": 16,
    "15": 14,
    "16": 11,
    "17": 7,
    "18": 4,
    "19": 2,
    "20": 1,
    "21": 0,
    "22": 1,
    "23": 0
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
    },
    {
      "projectId": 789,
      "projectName": "api-service",
      "commitCount": 67
    }
  ],
  "peakActivityHour": 9,
  "peakActivityPercentage": 14.10
}
```

## Response Fields

### Root Level
- **userId**: The GitLab user ID
- **username**: The user's username
- **email**: The email address used for commit attribution
- **lookbackDays**: Number of days included in the analysis
- **analysisStartDate**: Start date of the analysis period (UTC)
- **analysisEndDate**: End date of the analysis period (UTC)
- **totalCommits**: Total number of commits found in the period
- **hourlyDistribution**: Dictionary mapping hour (0-23) to commit count
- **timePeriods**: Breakdown by time periods (see below)
- **projects**: List of projects included in the analysis
- **peakActivityHour**: Hour with the most commits (0-23)
- **peakActivityPercentage**: Percentage of total commits in the peak hour

### Time Periods
The analysis breaks down commits into four periods:
- **Night** (00:00-05:59): Hours 0-5
- **Morning** (06:00-11:59): Hours 6-11
- **Afternoon** (12:00-17:59): Hours 12-17
- **Evening** (18:00-23:59): Hours 18-23

Each period includes:
- Raw count of commits
- Percentage of total commits

### Project Summary
Each project includes:
- **projectId**: GitLab project ID
- **projectName**: Project name
- **commitCount**: Number of commits in this project

Projects are sorted by commit count (descending).

## Example Usage

### Basic Analysis (30 days)
```http
GET /api/analysis/commit-time/123
```

### Custom Time Period (90 days)
```http
GET /api/analysis/commit-time/123?lookbackDays=90
```

### Using cURL
```bash
curl -X GET "https://your-api.com/api/analysis/commit-time/123?lookbackDays=60" \
  -H "accept: application/json"
```

### Using PowerShell
```powershell
Invoke-RestMethod -Uri "https://your-api.com/api/analysis/commit-time/123?lookbackDays=60" `
  -Method Get `
  -ContentType "application/json"
```

## Error Responses

### 400 Bad Request
Returned when request parameters are invalid:
```json
{
  "error": "lookbackDays must be greater than 0"
}
```

### 404 Not Found
Returned when the user doesn't exist or has no email:
```json
{
  "error": "User with ID 123 not found"
}
```

### 500 Internal Server Error
Returned for unexpected errors:
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.6.1",
  "title": "Error analyzing commit time distribution",
  "status": 500,
  "detail": "Unexpected error message"
}
```

## Implementation Details

### Data Source
- Uses GitLab's **Events API** (`/users/:user_id/events`)
- Filters events with `action_name = "pushed"` 
- Does **NOT** rely on stored commits in the database
- Does **NOT** require user email addresses
- Ensures real-time, up-to-date analysis

### How It Works
1. Fetches push events for the user within the specified time range
2. Each push event contains metadata including:
   - Number of commits in the push
   - Timestamp of the push (UTC)
   - Project information
3. Distributes commits across hours based on push event timestamps
4. Aggregates commit counts per project

### Time Zone
- All times are in **UTC**
- Event timestamps are based on `created_at` from GitLab Events API
- Hour analysis uses the UTC hour (0-23)

### Performance Considerations
- Uses GitLab's Events API which is efficient and well-indexed
- Faster than fetching individual commits from multiple repositories
- API rate limiting may affect performance for large datasets
- Events API typically returns data quickly
- Consider using shorter lookback periods for faster results

### Advantages Over Email-Based Approach
- **No email dependency**: Works even if users don't have email addresses configured
- **More reliable**: Uses official events API instead of filtering commits by email
- **Better performance**: Single API call instead of multiple project queries
- **Accurate attribution**: Events are directly associated with the user account

## Use Cases

1. **Work Pattern Analysis**: Understand when developers are most productive
2. **Burnout Detection**: Identify unusual commit patterns (e.g., late-night or weekend commits)
3. **Team Coordination**: Understand when team members are active for better collaboration
4. **Onboarding Insights**: Track how new developers' work patterns evolve
5. **Remote Work Analysis**: Analyze distributed team activity patterns across time zones

## Integration with Other Features

This endpoint complements the existing per-developer metrics but operates independently:
- Uses live GitLab data instead of stored metrics
- Focuses specifically on temporal patterns
- Can be used alongside the `/api/metrics/per-developer` endpoints

## Future Enhancements

Potential improvements:
- Day of week analysis (weekday vs weekend commits)
- Comparison with team averages
- Trend analysis over time (e.g., compare different 30-day periods)
- Time zone support for local time analysis
- Commit size correlation (commits by hour vs lines changed)
