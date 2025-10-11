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
            throw new ArgumentOutOfRangeException(nameof(windowDays), windowDays, "Window days must be greater than 0");
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

        // Fetch MRs from all contributed projects in parallel
        var fetchMrTasks = contributedProjects.Select(async project =>
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
                    _logger.LogDebug("Found {MrCount} merged MRs for user {UserId} in project {ProjectId}", 
                        userMergeRequests.Count, userId, project.Id);

                    return (
                        MergeRequests: userMergeRequests,
                        Summary: new ProjectMrSummary
                        {
                            ProjectId = project.Id,
                            ProjectName = project.Name ?? "Unknown",
                            MergedMrCount = userMergeRequests.Count
                        }
                    );
                }

                return (MergeRequests: new List<Models.Raw.GitLabMergeRequest>(), Summary: (ProjectMrSummary?)null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch merge requests for project {ProjectId}", project.Id);
                return (MergeRequests: new List<Models.Raw.GitLabMergeRequest>(), Summary: (ProjectMrSummary?)null);
            }
        });

        var projectResults = await Task.WhenAll(fetchMrTasks);

        var allMergeRequests = projectResults
            .SelectMany(r => r.MergeRequests)
            .ToList();

        var projectSummaries = projectResults
            .Where(r => r.Summary is not null)
            .Select(r => r.Summary!)
            .ToList();

        if (!allMergeRequests.Any())
        {
            _logger.LogWarning("No merged MRs found for user {UserId} in the specified time window", userId);
            return CreateEmptyResult(user, windowDays, windowStart, windowEnd);
        }

        _logger.LogInformation("Analyzing {MrCount} merged MRs for user {UserId}", 
            allMergeRequests.Count, userId);

        // Calculate MR cycle time (merged_at - first_commit_at) in parallel
        // Per PRD: cycle_time_median = median(merged_at - first_commit_at)
        var cycleTimeCalculationTasks = allMergeRequests.Select(async mr =>
        {
            if (!mr.MergedAt.HasValue)
            {
                return (CycleTime: (double?)null, Excluded: true, Reason: "NoMergeDate");
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
                            return (CycleTime: (double?)cycleTimeHours, Excluded: false, Reason: string.Empty);
                        }
                        else
                        {
                            _logger.LogDebug("Excluded MR {MrIid} in project {ProjectId} with negative cycle time: {CycleTime}h", 
                                mr.Iid, mr.ProjectId, cycleTimeHours);
                            return (CycleTime: (double?)null, Excluded: true, Reason: "NegativeCycleTime");
                        }
                    }
                    else
                    {
                        _logger.LogDebug("Excluded MR {MrIid} in project {ProjectId} due to missing commit timestamp", 
                            mr.Iid, mr.ProjectId);
                        return (CycleTime: (double?)null, Excluded: true, Reason: "MissingCommitTimestamp");
                    }
                }
                else
                {
                    _logger.LogDebug("Excluded MR {MrIid} in project {ProjectId} with no commits", 
                        mr.Iid, mr.ProjectId);
                    return (CycleTime: (double?)null, Excluded: true, Reason: "NoCommits");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch commits for MR {MrIid} in project {ProjectId}", 
                    mr.Iid, mr.ProjectId);
                return (CycleTime: (double?)null, Excluded: true, Reason: "Exception");
            }
        });

        var cycleTimeResults = await Task.WhenAll(cycleTimeCalculationTasks);

        var cycleTimes = cycleTimeResults
            .Where(r => r.CycleTime.HasValue)
            .Select(r => r.CycleTime!.Value)
            .ToList();

        var excludedCount = cycleTimeResults.Count(r => r.Excluded);

        // Calculate median (P50) and 90th percentile (P90)
        decimal? mrCycleTimeP50H = null;
        decimal? mrCycleTimeP90H = null;
        if (cycleTimes.Any())
        {
            var sortedCycleTimes = cycleTimes.OrderBy(x => x).ToList();
            var median = ComputeMedian(sortedCycleTimes);
            mrCycleTimeP50H = (decimal)median;

            var p90 = ComputePercentile(sortedCycleTimes, 90);
            mrCycleTimeP90H = (decimal)p90;

            _logger.LogInformation("Calculated MR cycle time P50: {CycleTimeP50}h, P90: {CycleTimeP90}h for user {UserId} from {Count} MRs", 
                mrCycleTimeP50H, mrCycleTimeP90H, userId, cycleTimes.Count);
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
            MrCycleTimeP50H = cycleTimes.Count == 0 ? null : ComputeMedian(cycleTimes),
            MrCycleTimeP90H = cycleTimes.Count == 0 ? null : ComputePercentile(cycleTimes, 90),
            MergedMrCount = cycleTimes.Count,
            ExcludedMrCount = excludedCount,
            Projects = projectSummaries
        };
    }

    private static double? ComputeMedian(List<double> sortedValues)
    {
        var count = sortedValues.Count;
        if (count == 0)
        {
            return null;
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

    private static double ComputePercentile(List<double> sortedValues, int percentile)
    {
        var count = sortedValues.Count;
        if (count == 0)
        {
            return 0;
        }

        if (percentile < 0 || percentile > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(percentile), "Percentile must be between 0 and 100");
        }

        // Calculate the index (0-based)
        double index = (percentile / 100.0) * (count - 1);
        int lowerIndex = (int)index;
        int upperIndex = lowerIndex + 1;

        if (lowerIndex == count - 1)
        {
            // Exactly at the last element
            return sortedValues[lowerIndex];
        }

        // Interpolate between lower and upper
        double lowerValue = sortedValues[lowerIndex];
        double upperValue = sortedValues[upperIndex];
        double fraction = index - lowerIndex;

        return lowerValue + (fraction * (upperValue - lowerValue));
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
            MrCycleTimeP90H = null,
            MergedMrCount = 0,
            ExcludedMrCount = 0,
            Projects = new List<ProjectMrSummary>()
        };
    }
}
