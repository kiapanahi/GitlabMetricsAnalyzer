# Testing the Commit Time Analysis Feature

## Quick Start Guide

### 1. Run the Application

Using .NET Aspire (recommended):
```powershell
aspire run
```

Or run the API service directly:
```powershell
dotnet run --project src/KuriousLabs.Management.KPIAnalysis.ApiService
```

### 2. Get a Valid User ID

First, you need a valid GitLab user ID from your instance. You can:

**Option A: Check the GitLab UI**
- Go to your GitLab instance
- Navigate to a user's profile
- The user ID is in the URL: `https://your-gitlab.com/username` → check the profile page

**Option B: Use the GitLab API directly**
```powershell
# Replace with your GitLab URL and token
$gitlabUrl = "https://your-gitlab.com"
$token = "your-gitlab-token"

Invoke-RestMethod -Uri "$gitlabUrl/api/v4/users" `
  -Headers @{ "Authorization" = "Bearer $token" } `
  | Select-Object id, username, email
```

### 3. Test the Endpoint

#### Using the HTTP File (VS Code)
1. Open `requests/commit-time-analysis.http`
2. Update the user ID (replace `123` with a valid ID)
3. Click "Send Request" above any request

#### Using PowerShell
```powershell
# Set your API URL (adjust port if needed)
$apiUrl = "http://localhost:5000"
$userId = 123  # Replace with valid user ID

# Basic request (30 days)
Invoke-RestMethod -Uri "$apiUrl/api/analysis/commit-time/$userId" `
  -Method Get | ConvertTo-Json -Depth 10

# With custom lookback period (60 days)
Invoke-RestMethod -Uri "$apiUrl/api/analysis/commit-time/$userId?lookbackDays=60" `
  -Method Get | ConvertTo-Json -Depth 10
```

#### Using curl
```bash
# Basic request
curl http://localhost:5000/api/analysis/commit-time/123

# With parameters
curl http://localhost:5000/api/analysis/commit-time/123?lookbackDays=60
```

### 4. Access Swagger UI

Navigate to: `http://localhost:5000/swagger`

1. Find the "Commit Time Analysis" section
2. Click on `GET /api/analysis/commit-time/{userId}`
3. Click "Try it out"
4. Enter a user ID and optional lookbackDays
5. Click "Execute"

## Expected Response

A successful request will return something like:

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
    "0": 2, "1": 1, "2": 0, ..., "23": 0
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

## Interpreting the Results

### Hourly Distribution
The `hourlyDistribution` object shows commits per hour (0-23 in UTC):
- **High morning commits (6-11)**: Developer works during standard business hours
- **High evening commits (18-23)**: Developer works late or is in a different timezone
- **High night commits (0-5)**: Potential burnout risk or timezone differences

### Time Periods
- **Night** (00:00-05:59): Late night/early morning work
- **Morning** (06:00-11:59): Early workday activity
- **Afternoon** (12:00-17:59): Standard workday hours
- **Evening** (18:00-23:59): After-hours work

### Peak Activity
- `peakActivityHour`: The hour with most commits
- `peakActivityPercentage`: What % of all commits happen in that hour

### Projects
Shows which projects the user contributed to, sorted by commit count.

## Common Test Scenarios

### 1. Active Developer (Recent Commits)
```powershell
# Should return data with commits
Invoke-RestMethod -Uri "http://localhost:5000/api/analysis/commit-time/123?lookbackDays=30"
```

### 2. Inactive Developer (No Recent Commits)
```powershell
# Should return zero commits but valid response structure
Invoke-RestMethod -Uri "http://localhost:5000/api/analysis/commit-time/456?lookbackDays=7"
```

### 3. Invalid User ID
```powershell
# Should return 404 Not Found
Invoke-RestMethod -Uri "http://localhost:5000/api/analysis/commit-time/999999" -ErrorAction Stop
```

### 4. Invalid Parameters
```powershell
# Should return 400 Bad Request
Invoke-RestMethod -Uri "http://localhost:5000/api/analysis/commit-time/123?lookbackDays=0" -ErrorAction Stop

# Should return 400 Bad Request
Invoke-RestMethod -Uri "http://localhost:5000/api/analysis/commit-time/123?lookbackDays=500" -ErrorAction Stop
```

## Troubleshooting

### "User with ID X not found"
- Verify the user ID exists in your GitLab instance
- Check if you have API access to view that user

### "No push events found"
- User might not have pushed any commits in the specified time period
- Check the user's GitLab profile for recent activity
- Try a longer lookback period (e.g., 60 or 90 days)
- Try a different, more active user

### Rate Limiting
- If you hit GitLab API rate limits, wait a moment
- The system has retry logic built-in
- Consider testing with shorter lookback periods

### Slow Response Times
- Analysis speed depends on:
  - Number of projects user contributed to
  - Number of commits in the time period
  - GitLab API response times
- Try shorter lookback periods for faster results
- Consider caching results for production use

## Performance Tips

1. **Start Small**: Test with 7-day lookback first
2. **Pick Active Users**: Users with regular commit activity
3. **Monitor Logs**: Check application logs for any issues
4. **Watch Rate Limits**: Be aware of GitLab API rate limits

## Next Steps

After successful testing:

1. **Integrate into Dashboards**: Use the API to build visual dashboards
2. **Automate Analysis**: Schedule regular analysis for team insights
3. **Compare Patterns**: Analyze multiple developers to identify team patterns
4. **Set Alerts**: Monitor for unusual commit patterns (e.g., excessive late-night work)
