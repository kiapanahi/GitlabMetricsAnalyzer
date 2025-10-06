using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

/// <summary>
/// Service implementation for calculating per-developer metrics from live GitLab data
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

    public async Task<MrCycleTimeResult> CalculateMrCycleTimeAsync(
        long userId,
        int windowDays = 30,
        CancellationToken cancellationToken = default)
    {
        if (windowDays <= 0)
        {
            throw new ArgumentException("Window days must be greater than 0", nameof(windowDays));
        }

        _logger.LogInformation("Calculating MR cycle time for user {UserId} over {WindowDays} days", userId, windowDays);

        // Get user details
        var user = await _gitLabHttpClient.GetUserByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            throw new InvalidOperationException($"User with ID {userId} not found");
        }

        var windowEnd = DateTime.UtcNow;
        var windowStart = windowEnd.AddDays(-windowDays);

        _logger.LogDebug("Fetching merge requests for user {UserId} from {WindowStart} to {WindowEnd}", 
            userId, windowStart, windowEnd);

        // Get projects the user has contributed to
        var contributedProjects = await _gitLabHttpClient.GetUserContributedProjectsAsync(userId, cancellationToken);

        if (!contributedProjects.Any())
        {
            _logger.LogWarning("No contributed projects found for user {UserId}", userId);
            return CreateEmptyResult(user, windowDays, windowStart, windowEnd);
        }

        _logger.LogInformation("Found {ProjectCount} contributed projects for user {UserId}", 
            contributedProjects.Count, userId);

        // Fetch MRs from all contributed projects
        var allMergeRequests = new List<Models.Raw.GitLabMergeRequest>();
        var projectSummaries = new List<ProjectMrSummary>();

        foreach (var project in contributedProjects)
        {
            try
            {
                var mergeRequests = await _gitLabHttpClient.GetMergeRequestsAsync(
                    project.Id,
                    new DateTimeOffset(windowStart),
                    cancellationToken);

                // Filter MRs by author and within time window
                var userMergeRequests = mergeRequests
                    .Where(mr => mr.Author?.Id == userId)
                    .Where(mr => mr.MergedAt.HasValue && mr.MergedAt.Value >= windowStart && mr.MergedAt.Value <= windowEnd)
                    .ToList();

                if (userMergeRequests.Any())
                {
                    allMergeRequests.AddRange(userMergeRequests);
                    projectSummaries.Add(new ProjectMrSummary
                    {
                        ProjectId = project.Id,
                        ProjectName = project.Name ?? "Unknown",
                        MergedMrCount = userMergeRequests.Count
                    });

                    _logger.LogDebug("Found {MrCount} merged MRs for user {UserId} in project {ProjectId}", 
                        userMergeRequests.Count, userId, project.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch merge requests for project {ProjectId}", project.Id);
                // Continue with other projects
            }
        }

        if (!allMergeRequests.Any())
        {
            _logger.LogWarning("No merged MRs found for user {UserId} in the specified time window", userId);
            return CreateEmptyResult(user, windowDays, windowStart, windowEnd);
        }

        _logger.LogInformation("Analyzing {MrCount} merged MRs for user {UserId}", 
            allMergeRequests.Count, userId);

        // Calculate MR cycle time (merged_at - first_commit_at)
        // Per PRD: cycle_time_median = median(merged_at - first_commit_at)
        var cycleTimes = new List<double>();
        var excludedCount = 0;

        foreach (var mr in allMergeRequests)
        {
            if (!mr.MergedAt.HasValue)
            {
                excludedCount++;
                continue;
            }

            try
            {
                // Fetch commits for this MR to get the first commit timestamp
                var mrCommits = await _gitLabHttpClient.GetMergeRequestCommitsAsync(
                    mr.ProjectId, 
                    mr.Iid, 
                    cancellationToken);

                if (mrCommits.Any())
                {
                    // Get the first (oldest) commit - commits are typically returned newest first
                    var firstCommit = mrCommits.OrderBy(c => c.CommittedDate).FirstOrDefault();
                    
                    if (firstCommit?.CommittedDate is not null)
                    {
                        var cycleTimeHours = (mr.MergedAt.Value - firstCommit.CommittedDate.Value).TotalHours;
                        
                        // Only include positive cycle times
                        if (cycleTimeHours > 0)
                        {
                            cycleTimes.Add(cycleTimeHours);
                        }
                        else
                        {
                            excludedCount++;
                            _logger.LogDebug("Excluded MR {MrIid} in project {ProjectId} with negative cycle time: {CycleTime}h", 
                                mr.Iid, mr.ProjectId, cycleTimeHours);
                        }
                    }
                    else
                    {
                        excludedCount++;
                        _logger.LogDebug("Excluded MR {MrIid} in project {ProjectId} due to missing commit timestamp", 
                            mr.Iid, mr.ProjectId);
                    }
                }
                else
                {
                    excludedCount++;
                    _logger.LogDebug("Excluded MR {MrIid} in project {ProjectId} with no commits", 
                        mr.Iid, mr.ProjectId);
                }
            }
            catch (Exception ex)
            {
                excludedCount++;
                _logger.LogWarning(ex, "Failed to fetch commits for MR {MrIid} in project {ProjectId}", 
                    mr.Iid, mr.ProjectId);
            }
        }

        // Calculate median (P50)
        decimal? mrCycleTimeP50H = null;
        if (cycleTimes.Any())
        {
            var sortedCycleTimes = cycleTimes.OrderBy(x => x).ToList();
            var median = ComputeMedian(sortedCycleTimes);
            mrCycleTimeP50H = (decimal)median;

            _logger.LogInformation("Calculated MR cycle time P50 for user {UserId}: {CycleTimeP50}h from {Count} MRs", 
                userId, mrCycleTimeP50H, cycleTimes.Count);
        }
        else
        {
            _logger.LogWarning("No valid cycle times calculated for user {UserId}", userId);
        }

        return new MrCycleTimeResult
        {
            UserId = userId,
            Username = user.Username ?? string.Empty,
            WindowDays = windowDays,
            WindowStart = windowStart,
            WindowEnd = windowEnd,
            MrCycleTimeP50H = mrCycleTimeP50H,
            MergedMrCount = cycleTimes.Count,
            ExcludedMrCount = excludedCount,
            Projects = projectSummaries
        };
    }

    private static double ComputeMedian(List<double> sortedValues)
    {
        var count = sortedValues.Count;
        if (count == 0)
        {
            return 0;
        }

        if (count % 2 == 1)
        {
            // Odd number of elements - return middle element
            return sortedValues[count / 2];
        }
        else
        {
            // Even number of elements - return average of two middle elements
            var mid1 = sortedValues[count / 2 - 1];
            var mid2 = sortedValues[count / 2];
            return (mid1 + mid2) / 2.0;
        }
    }

    private static MrCycleTimeResult CreateEmptyResult(
        Models.Raw.GitLabUser user,
        int windowDays,
        DateTime windowStart,
        DateTime windowEnd)
    {
        return new MrCycleTimeResult
        {
            UserId = user.Id,
            Username = user.Username ?? string.Empty,
            WindowDays = windowDays,
            WindowStart = windowStart,
            WindowEnd = windowEnd,
            MrCycleTimeP50H = null,
            MergedMrCount = 0,
            ExcludedMrCount = 0,
            Projects = new List<ProjectMrSummary>()
        };
    }
}
