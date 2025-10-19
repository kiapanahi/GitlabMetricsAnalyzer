using KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure;
using KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;

namespace KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

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
        var fetchMrTasks = contributedProjects.Select<GitLabContributedProject, Task<(IReadOnlyList<Models.Raw.GitLabMergeRequest> MergeRequests, ProjectMrSummary? Summary)>>(async project =>
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

                return (MergeRequests: Array.Empty<Models.Raw.GitLabMergeRequest>(), Summary: (ProjectMrSummary?)null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch merge requests for project {ProjectId}", project.Id);
                return (MergeRequests: Array.Empty<Models.Raw.GitLabMergeRequest>(), Summary: (ProjectMrSummary?)null);
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
        var sortedCycleTimes = cycleTimes.OrderBy(x => x).ToList();
        decimal? mrCycleTimeP50H = null;
        decimal? mrCycleTimeP90H = null;
        if (cycleTimes.Any())
        {
            var median = ComputeMedian(sortedCycleTimes);
            mrCycleTimeP50H = (decimal)median!;

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
            MrCycleTimeP50H = cycleTimes.Count == 0 ? null : (decimal?)ComputeMedian(sortedCycleTimes),
            MrCycleTimeP90H = cycleTimes.Count == 0 ? null : (decimal?)ComputePercentile(sortedCycleTimes, 90),
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

    public async Task<FlowMetricsResult> CalculateFlowMetricsAsync(
        long userId,
        int windowDays = 30,
        CancellationToken cancellationToken = default)
    {
        if (windowDays <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(windowDays), windowDays, "Window days must be greater than 0");
        }

        _logger.LogInformation("Calculating flow metrics for user {UserId} over {WindowDays} days", userId, windowDays);

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
            return CreateEmptyFlowResult(user, windowDays, windowStart, windowEnd);
        }

        _logger.LogInformation("Found {ProjectCount} contributed projects for user {UserId}", 
            contributedProjects.Count, userId);

        // Fetch MRs from all contributed projects in parallel
        var fetchMrTasks = contributedProjects.Select<GitLabContributedProject, Task<(
            IReadOnlyList<Models.Raw.GitLabMergeRequest> MergedMRs, 
            IReadOnlyList<Models.Raw.GitLabMergeRequest> OpenMRs,
            ProjectMrSummary? Summary)>>(async project =>
        {
            try
            {
                var mergeRequests = await _gitLabHttpClient.GetMergeRequestsAsync(
                    project.Id,
                    new DateTimeOffset(windowStart),
                    cancellationToken);

                // Filter MRs by author
                var userMergeRequests = mergeRequests
                    .Where(mr => mr.Author?.Id == userId)
                    .ToList();

                // Separate merged and open/draft MRs
                var mergedMrs = userMergeRequests
                    .Where(mr => mr.MergedAt.HasValue && mr.MergedAt.Value >= windowStart && mr.MergedAt.Value <= windowEnd)
                    .ToList();

                var openMrs = userMergeRequests
                    .Where(mr => mr.State == "opened" || mr.State == "draft")
                    .ToList();

                if (mergedMrs.Any() || openMrs.Any())
                {
                    _logger.LogDebug("Found {MergedCount} merged and {OpenCount} open MRs for user {UserId} in project {ProjectId}", 
                        mergedMrs.Count, openMrs.Count, userId, project.Id);

                    return (
                        MergedMRs: mergedMrs,
                        OpenMRs: openMrs,
                        Summary: new ProjectMrSummary
                        {
                            ProjectId = project.Id,
                            ProjectName = project.Name ?? "Unknown",
                            MergedMrCount = mergedMrs.Count
                        }
                    );
                }

                return (
                    MergedMRs: Array.Empty<Models.Raw.GitLabMergeRequest>(), 
                    OpenMRs: Array.Empty<Models.Raw.GitLabMergeRequest>(),
                    Summary: (ProjectMrSummary?)null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch merge requests for project {ProjectId}", project.Id);
                return (
                    MergedMRs: Array.Empty<Models.Raw.GitLabMergeRequest>(), 
                    OpenMRs: Array.Empty<Models.Raw.GitLabMergeRequest>(),
                    Summary: (ProjectMrSummary?)null);
            }
        });

        var projectResults = await Task.WhenAll(fetchMrTasks);

        var allMergedMergeRequests = projectResults
            .SelectMany(r => r.MergedMRs)
            .ToList();

        var allOpenMergeRequests = projectResults
            .SelectMany(r => r.OpenMRs)
            .ToList();

        var projectSummaries = projectResults
            .Where(r => r.Summary is not null)
            .Select(r => r.Summary!)
            .ToList();

        // Metric 1: Merged MRs Count
        var mergedMrsCount = allMergedMergeRequests.Count;

        // Metric 7: WIP/Open MRs Count (at snapshot time)
        var wipOpenMrsCount = allOpenMergeRequests.Count;

        // Metric 8: Context Switching Index (distinct projects)
        var contextSwitchingIndex = projectSummaries.Count(p => p.MergedMrCount > 0);

        _logger.LogInformation("Calculating detailed metrics for {MrCount} merged MRs", mergedMrsCount);

        // Calculate metrics in parallel for each MR
        var metricsCalculationTasks = allMergedMergeRequests.Select(async mr =>
        {
            try
            {
                // Get commits for this MR
                var mrCommits = await _gitLabHttpClient.GetMergeRequestCommitsAsync(
                    mr.ProjectId, 
                    mr.Iid, 
                    cancellationToken);

                // Calculate lines changed from commit stats
                var linesChanged = mrCommits
                    .Where(c => c.Stats is not null)
                    .Sum(c => (c.Stats!.Additions + c.Stats.Deletions));

                // Get first commit timestamp
                DateTime? firstCommitDate = null;
                if (mrCommits.Any())
                {
                    var firstCommit = mrCommits.OrderBy(c => c.CommittedDate).FirstOrDefault();
                    firstCommitDate = firstCommit?.CommittedDate;
                }

                // Calculate coding time (first commit → MR open)
                double? codingTimeH = null;
                if (firstCommitDate.HasValue && mr.CreatedAt.HasValue)
                {
                    codingTimeH = (mr.CreatedAt.Value - firstCommitDate.Value).TotalHours;
                    if (codingTimeH < 0) codingTimeH = null; // Invalid
                }

                // Get MR notes to calculate review metrics
                var mrNotes = await _gitLabHttpClient.GetMergeRequestNotesAsync(
                    mr.ProjectId,
                    mr.Iid,
                    cancellationToken);

                // Time to first review (MR open → first non-author comment)
                double? timeToFirstReviewH = null;
                var firstReviewNote = mrNotes
                    .Where(n => !n.System && n.Author?.Id != userId && n.CreatedAt.HasValue)
                    .OrderBy(n => n.CreatedAt)
                    .FirstOrDefault();

                if (firstReviewNote?.CreatedAt is not null && mr.CreatedAt.HasValue)
                {
                    timeToFirstReviewH = (firstReviewNote.CreatedAt.Value - mr.CreatedAt.Value).TotalHours;
                    if (timeToFirstReviewH < 0) timeToFirstReviewH = null; // Invalid
                }

                // For review time and merge time, we need approval data
                // GitLab API doesn't always have explicit approval timestamps in the basic API
                // We'll calculate merge time as a proxy: MR created → merged
                double? mergeTimeH = null;
                if (mr.MergedAt.HasValue && mr.CreatedAt.HasValue)
                {
                    mergeTimeH = (mr.MergedAt.Value - mr.CreatedAt.Value).TotalHours;
                    if (mergeTimeH < 0) mergeTimeH = null; // Invalid
                }

                return new
                {
                    LinesChanged = linesChanged,
                    CodingTimeH = codingTimeH,
                    TimeToFirstReviewH = timeToFirstReviewH,
                    MergeTimeH = mergeTimeH
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to calculate metrics for MR {MrIid} in project {ProjectId}", 
                    mr.Iid, mr.ProjectId);
                return new
                {
                    LinesChanged = 0,
                    CodingTimeH = (double?)null,
                    TimeToFirstReviewH = (double?)null,
                    MergeTimeH = (double?)null
                };
            }
        });

        var metricsResults = await Task.WhenAll(metricsCalculationTasks);

        // Metric 2: Lines Changed (total)
        var linesChanged = metricsResults.Sum(m => m.LinesChanged);

        // Metric 3: Coding Time (median)
        var codingTimes = metricsResults
            .Where(m => m.CodingTimeH.HasValue && m.CodingTimeH.Value > 0)
            .Select(m => m.CodingTimeH!.Value)
            .OrderBy(x => x)
            .ToList();
        var codingTimeMedianH = codingTimes.Any() ? (decimal?)ComputeMedian(codingTimes) : null;

        // Metric 4: Time to First Review (median)
        var timeToFirstReviewTimes = metricsResults
            .Where(m => m.TimeToFirstReviewH.HasValue && m.TimeToFirstReviewH.Value > 0)
            .Select(m => m.TimeToFirstReviewH!.Value)
            .OrderBy(x => x)
            .ToList();
        var timeToFirstReviewMedianH = timeToFirstReviewTimes.Any() ? (decimal?)ComputeMedian(timeToFirstReviewTimes) : null;

        // Metric 5: Review Time (median) - Not available without approval API
        decimal? reviewTimeMedianH = null;

        // Metric 6: Merge Time (median) - Using MR created → merged as proxy
        var mergeTimes = metricsResults
            .Where(m => m.MergeTimeH.HasValue && m.MergeTimeH.Value > 0)
            .Select(m => m.MergeTimeH!.Value)
            .OrderBy(x => x)
            .ToList();
        var mergeTimeMedianH = mergeTimes.Any() ? (decimal?)ComputeMedian(mergeTimes) : null;

        _logger.LogInformation(
            "Flow metrics calculated for user {UserId}: {MergedCount} merged MRs, {LinesChanged} lines changed, {OpenCount} open MRs, {ProjectCount} projects",
            userId, mergedMrsCount, linesChanged, wipOpenMrsCount, contextSwitchingIndex);

        return new FlowMetricsResult
        {
            UserId = userId,
            Username = user.Username ?? string.Empty,
            WindowDays = windowDays,
            WindowStart = windowStart,
            WindowEnd = windowEnd,
            MergedMrsCount = mergedMrsCount,
            LinesChanged = linesChanged,
            CodingTimeMedianH = codingTimeMedianH,
            TimeToFirstReviewMedianH = timeToFirstReviewMedianH,
            ReviewTimeMedianH = reviewTimeMedianH,
            MergeTimeMedianH = mergeTimeMedianH,
            WipOpenMrsCount = wipOpenMrsCount,
            ContextSwitchingIndex = contextSwitchingIndex,
            Projects = projectSummaries
        };
    }

    private static FlowMetricsResult CreateEmptyFlowResult(
        Models.Raw.GitLabUser user,
        int windowDays,
        DateTime windowStart,
        DateTime windowEnd)
    {
        return new FlowMetricsResult
        {
            UserId = user.Id,
            Username = user.Username ?? string.Empty,
            WindowDays = windowDays,
            WindowStart = windowStart,
            WindowEnd = windowEnd,
            MergedMrsCount = 0,
            LinesChanged = 0,
            CodingTimeMedianH = null,
            TimeToFirstReviewMedianH = null,
            ReviewTimeMedianH = null,
            MergeTimeMedianH = null,
            WipOpenMrsCount = 0,
            ContextSwitchingIndex = 0,
            Projects = new List<ProjectMrSummary>()
        };
    }
}
