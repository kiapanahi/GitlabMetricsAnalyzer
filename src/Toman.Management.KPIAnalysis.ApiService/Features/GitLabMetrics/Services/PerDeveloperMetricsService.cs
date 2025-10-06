using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

/// <summary>
/// Service implementation for computing per-developer metrics from live GitLab data
/// </summary>
public sealed class PerDeveloperMetricsService : IPerDeveloperMetricsService
{
    private readonly IGitLabHttpClient _gitLabHttpClient;
    private readonly ILogger<PerDeveloperMetricsService> _logger;

    public PerDeveloperMetricsService(
        IGitLabHttpClient gitLabHttpClient,
        ILogger<PerDeveloperMetricsService> logger)
    {
        _gitLabHttpClient = gitLabHttpClient;
        _logger = logger;
    }

    public async Task<MrThroughputResult> CalculateMrThroughputAsync(
        long developerId,
        int windowDays = 7,
        CancellationToken cancellationToken = default)
    {
        if (windowDays <= 0)
        {
            throw new ArgumentException("Window days must be greater than 0", nameof(windowDays));
        }

        _logger.LogInformation("Calculating MR throughput for developer {DeveloperId} over {WindowDays} days", developerId, windowDays);

        // Get user details
        var user = await _gitLabHttpClient.GetUserByIdAsync(developerId, cancellationToken);
        if (user is null)
        {
            throw new InvalidOperationException($"User with ID {developerId} not found");
        }

        var endDate = DateTime.UtcNow;
        var startDate = endDate.AddDays(-windowDays);

        _logger.LogDebug("Fetching merge requests for developer {DeveloperId} from {StartDate} to {EndDate}", 
            developerId, startDate, endDate);

        // Get projects the user has contributed to
        var contributedProjects = await _gitLabHttpClient.GetUserContributedProjectsAsync(developerId, cancellationToken);

        if (!contributedProjects.Any())
        {
            _logger.LogWarning("No contributed projects found for developer {DeveloperId}", developerId);
            return CreateEmptyResult(user, windowDays, startDate, endDate);
        }

        _logger.LogInformation("Found {ProjectCount} contributed projects for developer {DeveloperId}", 
            contributedProjects.Count, developerId);

        // Fetch merge requests from all contributed projects
        var projectMrSummaries = new List<ProjectMrSummary>();
        var totalMergedMrs = 0;

        foreach (var project in contributedProjects)
        {
            try
            {
                var mergeRequests = await _gitLabHttpClient.GetMergeRequestsAsync(
                    project.Id,
                    new DateTimeOffset(startDate),
                    cancellationToken);

                // Filter merged MRs authored by this developer within the time window
                var mergedMrsByDeveloper = mergeRequests
                    .Where(mr => mr.Author?.Id == developerId &&
                                 mr.MergedAt.HasValue &&
                                 mr.MergedAt.Value >= startDate &&
                                 mr.MergedAt.Value <= endDate)
                    .ToList();

                if (mergedMrsByDeveloper.Any())
                {
                    var mergedCount = mergedMrsByDeveloper.Count;
                    totalMergedMrs += mergedCount;

                    projectMrSummaries.Add(new ProjectMrSummary
                    {
                        ProjectId = project.Id,
                        ProjectName = project.Name ?? "Unknown",
                        MergedMrCount = mergedCount
                    });

                    _logger.LogDebug("Found {MergedCount} merged MRs in project {ProjectId} for developer {DeveloperId}",
                        mergedCount, project.Id, developerId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch merge requests for project {ProjectId}", project.Id);
                // Continue with other projects
            }
        }

        // Calculate throughput as MRs merged per week
        var mrThroughputWk = CalculateThroughputPerWeek(totalMergedMrs, windowDays);

        _logger.LogInformation(
            "Calculated MR throughput for developer {DeveloperId}: {TotalMergedMrs} MRs in {WindowDays} days = {ThroughputWk} MRs/week",
            developerId, totalMergedMrs, windowDays, mrThroughputWk);

        return new MrThroughputResult
        {
            UserId = developerId,
            Username = user.Username ?? string.Empty,
            WindowDays = windowDays,
            AnalysisStartDate = startDate,
            AnalysisEndDate = endDate,
            TotalMergedMrs = totalMergedMrs,
            MrThroughputWk = mrThroughputWk,
            Projects = projectMrSummaries.OrderByDescending(p => p.MergedMrCount).ToList()
        };
    }

    private static int CalculateThroughputPerWeek(int totalMergedMrs, int windowDays)
    {
        // Convert to weekly rate: (count * 7) / windowDays
        return (int)(totalMergedMrs * 7.0 / windowDays);
    }

    private static MrThroughputResult CreateEmptyResult(
        Models.Raw.GitLabUser user,
        int windowDays,
        DateTime startDate,
        DateTime endDate)
    {
        return new MrThroughputResult
        {
            UserId = user.Id,
            Username = user.Username ?? string.Empty,
            WindowDays = windowDays,
            AnalysisStartDate = startDate,
            AnalysisEndDate = endDate,
            TotalMergedMrs = 0,
            MrThroughputWk = 0,
            Projects = new List<ProjectMrSummary>()
        };
    }
}
