using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

/// <summary>
/// Service implementation for analyzing commit time patterns
/// </summary>
public sealed class CommitTimeAnalysisService : ICommitTimeAnalysisService
{
    private readonly IGitLabHttpClient _gitLabHttpClient;
    private readonly ILogger<CommitTimeAnalysisService> _logger;

    public CommitTimeAnalysisService(
        IGitLabHttpClient gitLabHttpClient,
        ILogger<CommitTimeAnalysisService> logger)
    {
        _gitLabHttpClient = gitLabHttpClient;
        _logger = logger;
    }

    public async Task<CommitTimeDistributionAnalysis> AnalyzeCommitTimeDistributionAsync(
        long userId,
        int lookbackDays = 30,
        CancellationToken cancellationToken = default)
    {
        if (lookbackDays <= 0)
        {
            throw new ArgumentException("Lookback days must be greater than 0", nameof(lookbackDays));
        }

        _logger.LogInformation("Starting commit time distribution analysis for user {UserId} over {LookbackDays} days", userId, lookbackDays);

        // Get user details
        var user = await _gitLabHttpClient.GetUserByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            throw new InvalidOperationException($"User with ID {userId} not found");
        }

        var endDate = DateTime.UtcNow;
        var startDate = endDate.AddDays(-lookbackDays);

        _logger.LogDebug("Fetching push events for user {UserId} from {StartDate} to {EndDate}", userId, startDate, endDate);

        // Get push events for the user (using events API which doesn't require email)
        var events = await _gitLabHttpClient.GetUserEventsAsync(
            userId,
            new DateTimeOffset(startDate),
            new DateTimeOffset(endDate),
            cancellationToken);

        if (!events.Any())
        {
            _logger.LogWarning("No push events found for user {UserId} in the specified time period", userId);
            return CreateEmptyAnalysis(user, lookbackDays, startDate, endDate);
        }

        _logger.LogInformation("Found {EventCount} push events for user {UserId}", events.Count, userId);

        // Get unique project IDs from events
        var projectIds = events
            .Where(e => e.Project is not null)
            .Select(e => e.Project!.Id)
            .Distinct()
            .ToList();

        // Fetch project details to get names
        // Fetch only the required projects by ID to avoid unnecessary data transfer
        var projectTasks = projectIds
            .Select(id => _gitLabHttpClient.GetProjectByIdAsync(id, cancellationToken))
            .ToList();
        var requiredProjects = await Task.WhenAll(projectTasks);
        var projectNameMap = requiredProjects
            .Where(p => p != null)
            .ToDictionary(p => p!.Id, p => p.Name ?? "Unknown");

        // Group events by project and calculate commit counts
        var projectGroups = events
            .Where(e => e.Project is not null && e.PushData is not null)
            .GroupBy(e => e.Project!.Id)
            .Select(g => new
            {
                Id = g.Key,
                Name = projectNameMap.GetValueOrDefault(g.Key, "Unknown"),
                CommitCount = g.Sum(e => e.PushData!.CommitCount),
                Events = g.ToList()
            })
            .OrderByDescending(p => p.CommitCount)
            .ToList();

        var projectSummaries = projectGroups
            .Select(p => new ProjectCommitSummary
            {
                ProjectId = p.Id,
                ProjectName = p.Name,
                CommitCount = p.CommitCount
            })
            .ToList();

        // Create event time records for analysis
        // Each push event represents one or more commits at that time
        var eventTimes = new List<EventTime>();
        foreach (var evt in events.Where(e => e.PushData is not null))
        {
            var projectId = evt.Project?.Id ?? 0;
            var projectName = projectId != 0 ? projectNameMap.GetValueOrDefault(projectId, "Unknown") : "Unknown";
            
            // Add an entry for each commit in the push event
            // This gives us a more accurate distribution
            for (var i = 0; i < evt.PushData!.CommitCount; i++)
            {
                eventTimes.Add(new EventTime
                {
                    Timestamp = evt.CreatedAt,
                    ProjectId = projectId,
                    ProjectName = projectName
                });
            }
        }

        var totalCommits = eventTimes.Count;

        _logger.LogInformation("Analyzing {CommitCount} total commits from {EventCount} push events for user {UserId}",
            totalCommits, events.Count, userId);

        // Analyze hourly distribution
        var hourlyDistribution = AnalyzeHourlyDistributionFromEvents(eventTimes);
        var timePeriodDistribution = AnalyzeTimePeriodDistribution(hourlyDistribution, totalCommits);

        // Find peak activity
        var (peakHour, peakCount) = hourlyDistribution.MaxBy(kvp => kvp.Value);
        var peakPercentage = totalCommits > 0
            ? (decimal)peakCount / totalCommits * 100
            : 0;

        _logger.LogInformation("Completed analysis for user {UserId}: {TotalCommits} commits, peak at hour {PeakHour}",
            userId, totalCommits, peakHour);

        return new CommitTimeDistributionAnalysis
        {
            UserId = userId,
            Username = user.Username ?? string.Empty,
            Email = user.Email ?? string.Empty,
            LookbackDays = lookbackDays,
            AnalysisStartDate = startDate,
            AnalysisEndDate = endDate,
            TotalCommits = totalCommits,
            HourlyDistribution = hourlyDistribution,
            TimePeriods = timePeriodDistribution,
            Projects = projectSummaries,
            PeakActivityHour = peakHour,
            PeakActivityPercentage = peakPercentage
        };
    }

    private static Dictionary<int, int> AnalyzeHourlyDistributionFromEvents(List<EventTime> events)
    {
        var distribution = Enumerable.Range(0, 24).ToDictionary(hour => hour, _ => 0);

        foreach (var evt in events)
        {
            // Use CreatedAt which is in UTC
            var hour = evt.Timestamp.Hour;
            distribution[hour]++;
        }

        return distribution;
    }

    private static TimePeriodDistribution AnalyzeTimePeriodDistribution(Dictionary<int, int> hourlyDistribution, int totalCommits)
    {
        var night = hourlyDistribution.Where(kvp => kvp.Key >= 0 && kvp.Key < 6).Sum(kvp => kvp.Value);
        var morning = hourlyDistribution.Where(kvp => kvp.Key >= 6 && kvp.Key < 12).Sum(kvp => kvp.Value);
        var afternoon = hourlyDistribution.Where(kvp => kvp.Key >= 12 && kvp.Key < 18).Sum(kvp => kvp.Value);
        var evening = hourlyDistribution.Where(kvp => kvp.Key >= 18 && kvp.Key < 24).Sum(kvp => kvp.Value);

        var percentages = new TimePeriodPercentages
        {
            Night = totalCommits > 0 ? (decimal)night / totalCommits * 100 : 0,
            Morning = totalCommits > 0 ? (decimal)morning / totalCommits * 100 : 0,
            Afternoon = totalCommits > 0 ? (decimal)afternoon / totalCommits * 100 : 0,
            Evening = totalCommits > 0 ? (decimal)evening / totalCommits * 100 : 0
        };

        return new TimePeriodDistribution
        {
            Night = night,
            Morning = morning,
            Afternoon = afternoon,
            Evening = evening,
            Percentages = percentages
        };
    }

    private static CommitTimeDistributionAnalysis CreateEmptyAnalysis(
        Models.Raw.GitLabUser user,
        int lookbackDays,
        DateTime startDate,
        DateTime endDate)
    {
        return new CommitTimeDistributionAnalysis
        {
            UserId = user.Id,
            Username = user.Username ?? string.Empty,
            Email = user.Email ?? string.Empty,
            LookbackDays = lookbackDays,
            AnalysisStartDate = startDate,
            AnalysisEndDate = endDate,
            TotalCommits = 0,
            HourlyDistribution = Enumerable.Range(0, 24).ToDictionary(hour => hour, _ => 0),
            TimePeriods = new TimePeriodDistribution
            {
                Night = 0,
                Morning = 0,
                Afternoon = 0,
                Evening = 0,
                Percentages = new TimePeriodPercentages
                {
                    Night = 0,
                    Morning = 0,
                    Afternoon = 0,
                    Evening = 0
                }
            },
            Projects = new List<ProjectCommitSummary>(),
            PeakActivityHour = 0,
            PeakActivityPercentage = 0
        };
    }

    private sealed class EventTime
    {
        public required DateTime Timestamp { get; init; }
        public required long ProjectId { get; init; }
        public required string ProjectName { get; init; }
    }
}
