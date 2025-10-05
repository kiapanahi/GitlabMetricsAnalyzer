# Commit Time Distribution Analysis Feature

## Summary

A new feature has been added to analyze the distribution of commits across the 24 hours of the day for GitLab users. This feature fetches data **directly from GitLab** (not from the stored database) and provides real-time insights into developer work patterns.

## Files Created

### 1. Service Interface
**File:** `src/Toman.Management.KPIAnalysis.ApiService/Features/GitLabMetrics/Services/ICommitTimeAnalysisService.cs`

Defines the service contract and all response models:
- `ICommitTimeAnalysisService` - Service interface
- `CommitTimeDistributionAnalysis` - Main analysis result
- `TimePeriodDistribution` - Breakdown by time periods (Night, Morning, Afternoon, Evening)
- `TimePeriodPercentages` - Percentage distribution
- `ProjectCommitSummary` - Per-project commit counts

### 2. Service Implementation
**File:** `src/Toman.Management.KPIAnalysis.ApiService/Features/GitLabMetrics/Services/CommitTimeAnalysisService.cs`

Implements the analysis logic:
- Fetches user details from GitLab
- Uses GitLab Events API to get push events (no email required!)
- Filters events with `action_name = "pushed"`
- Extracts commit counts and timestamps from push events
- Analyzes hourly distribution (0-23 hours)
- Calculates time period breakdowns
- Identifies peak activity hours
- Aggregates per-project statistics

### 3. API Endpoint
**File:** `src/Toman.Management.KPIAnalysis.ApiService/Features/GitLabMetrics/CommitTimeAnalysisEndpoints.cs`

Exposes the HTTP endpoint:
- `GET /api/analysis/commit-time/{userId}?lookbackDays={days}`
- Validates input parameters
- Handles errors appropriately (400, 404, 500)
- Returns JSON response with analysis results

### 4. Documentation
**File:** `docs/COMMIT_TIME_ANALYSIS_API.md`

Comprehensive API documentation including:
- Endpoint details and parameters
- Response schema with examples
- Use cases and best practices
- Error responses
- Integration guidance

### 5. HTTP Test File
**File:** `requests/commit-time-analysis.http`

REST Client examples for testing:
- Basic usage with default parameters
- Custom lookback periods
- Edge cases and error scenarios

## Integration Points

### Service Registration
Updated `ServiceCollectionExtensions.cs`:
```csharp
builder.Services.AddScoped<ICommitTimeAnalysisService, CommitTimeAnalysisService>();
```

### Endpoint Mapping
Updated `GitLabMetricsEndpoints.cs`:
```csharp
app.MapCommitTimeAnalysisEndpoints();
```

## API Usage

### Endpoint
```
GET /api/analysis/commit-time/{userId}?lookbackDays={days}
```

### Parameters
- `userId` (required): GitLab user ID
- `lookbackDays` (optional): Number of days to analyze (default: 30, max: 365)

### Example Request
```http
GET /api/analysis/commit-time/123?lookbackDays=30
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
    "0": 2, "1": 1, "2": 0, "3": 0, "4": 0, "5": 1,
    "6": 3, "7": 8, "8": 15, "9": 22, "10": 18, "11": 12,
    "12": 8, "13": 10, "14": 16, "15": 14, "16": 11, "17": 7,
    "18": 4, "19": 2, "20": 1, "21": 0, "22": 1, "23": 0
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

## Key Features

1. **Real-time Data**: Fetches events directly from GitLab Events API
2. **No Database Dependency**: Does not rely on stored metrics
3. **No Email Required**: Uses Events API instead of email-based filtering
4. **Comprehensive Analysis**: 
   - Hourly breakdown (0-23)
   - Time period grouping (Night, Morning, Afternoon, Evening)
   - Peak activity identification
   - Per-project breakdown
5. **Flexible Time Ranges**: 1-365 days lookback period
6. **Error Handling**: Proper validation and error responses
7. **UTC Timezone**: All times standardized to UTC
8. **Better Performance**: Single API call for events vs multiple repository queries

## Technical Details

### Dependencies
- Uses `IGitLabHttpClient` for GitLab API calls
- Leverages existing GitLab integration infrastructure
- No additional NuGet packages required

### GitLab Events API Integration
- Endpoint: `/users/:user_id/events`
- Filter: `action=pushed`
- Date range: `after` and `before` parameters
- Returns push events with commit counts and timestamps

### Performance Considerations
- **Efficient**: Single API call to Events API
- **Fast**: Events API is well-indexed and optimized
- **Scalable**: No need to query multiple repositories
- API rate limiting applies
- Recommended lookback periods: 7-90 days for best performance

### Advantages Over Alternative Approaches
- **No email dependency**: Works regardless of email configuration
- **More reliable**: Direct association with user account
- **Better performance**: One API call vs many repository queries
- **Simpler logic**: No need to discover contributed projects first

### Architecture Alignment
- Follows vertical slice architecture
- Uses minimal APIs pattern
- Implements proper logging
- Includes comprehensive error handling
- Follows C# code style conventions

## Testing

The feature can be tested using:
1. The included HTTP file (`requests/commit-time-analysis.http`)
2. OpenAPI/Swagger UI (when running in development)
3. Direct HTTP requests via curl or PowerShell

## Build Status

✅ Successfully compiled with .NET 9
✅ All existing tests still passing
✅ No new compilation errors or warnings introduced

## Future Enhancements

Potential improvements identified:
- Day of week analysis
- Comparison with team averages
- Trend analysis over time
- Time zone support for local time
- Commit size correlation analysis
