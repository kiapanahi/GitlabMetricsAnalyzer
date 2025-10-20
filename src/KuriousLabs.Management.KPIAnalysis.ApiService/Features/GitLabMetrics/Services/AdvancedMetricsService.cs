using Microsoft.Extensions.Options;

using KuriousLabs.Management.KPIAnalysis.ApiService.Configuration;
using KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure;
using KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;

namespace KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

/// <summary>
/// Service implementation for calculating advanced metrics for deeper insights
/// </summary>
public sealed class AdvancedMetricsService : IAdvancedMetricsService
{
    private readonly IGitLabHttpClient _gitLabHttpClient;
    private readonly ILogger<AdvancedMetricsService> _logger;
    private readonly MetricsConfiguration _configuration;

    public AdvancedMetricsService(
        IGitLabHttpClient gitLabHttpClient,
        ILogger<AdvancedMetricsService> logger,
        IOptions<MetricsConfiguration> configuration)
    {
        _gitLabHttpClient = gitLabHttpClient;
        _logger = logger;
        _configuration = configuration.Value;
    }

    public async Task<AdvancedMetricsResult> CalculateAdvancedMetricsAsync(
        long userId,
        int windowDays = 30,
        CancellationToken cancellationToken = default)
    {
        if (windowDays <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(windowDays), windowDays, "Window days must be greater than 0");
        }

        _logger.LogInformation("Calculating advanced metrics for user {UserId} over {WindowDays} days", userId, windowDays);

        // Get user details
        var user = await _gitLabHttpClient.GetUserByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            throw new InvalidOperationException($"User with ID {userId} not found");
        }

        var windowEnd = DateTime.UtcNow;
        var windowStart = windowEnd.AddDays(-windowDays);

        _logger.LogDebug("Fetching data for user {UserId} from {WindowStart} to {WindowEnd}",
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

        // Fetch data from all contributed projects in parallel
        var projectDataTasks = contributedProjects.Select(async project =>
        {
            try
            {
                var mergeRequests = await _gitLabHttpClient.GetMergeRequestsAsync(
                    project.Id,
                    new DateTimeOffset(windowStart),
                    cancellationToken);

                // Filter MRs within time window (created or updated within window)
                var mrsInWindow = mergeRequests
                    .Where(mr => (mr.CreatedAt.HasValue && mr.CreatedAt.Value >= windowStart && mr.CreatedAt.Value <= windowEnd) ||
                                 (mr.UpdatedAt.HasValue && mr.UpdatedAt.Value >= windowStart && mr.UpdatedAt.Value <= windowEnd))
                    .ToList();

                var commits = await _gitLabHttpClient.GetCommitsAsync(
                    project.Id,
                    new DateTimeOffset(windowStart),
                    cancellationToken);

                var commitsInWindow = commits
                    .Where(c => c.CommittedDate.HasValue && c.CommittedDate.Value >= windowStart && c.CommittedDate.Value <= windowEnd)
                    .ToList();

                return new ProjectData
                {
                    ProjectId = project.Id,
                    ProjectName = project.Name ?? "Unknown",
                    MergeRequests = mrsInWindow,
                    Commits = commitsInWindow
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch data for project {ProjectId}", project.Id);
                return new ProjectData
                {
                    ProjectId = project.Id,
                    ProjectName = project.Name ?? "Unknown",
                    MergeRequests = [],
                    Commits = []
                };
            }
        });

        var projectDataList = await Task.WhenAll(projectDataTasks);
        var allMergeRequests = projectDataList.SelectMany(p => p.MergeRequests).ToList();
        var allCommits = projectDataList.SelectMany(p => p.Commits).ToList();

        _logger.LogInformation("Processing {MrCount} merge requests and {CommitCount} commits",
            allMergeRequests.Count, allCommits.Count);

        // Calculate each metric
        var busFactor = await CalculateBusFactorAsync(userId, projectDataList, cancellationToken);
        var responseTimeDistribution = await CalculateResponseTimeDistributionAsync(userId, projectDataList, cancellationToken);
        var batchSize = CalculateBatchSize(userId, allMergeRequests, projectDataList);
        var draftDuration = await CalculateDraftDurationAsync(userId, allMergeRequests, projectDataList, cancellationToken);
        var iterationCount = await CalculateIterationCountAsync(userId, allMergeRequests, projectDataList, cancellationToken);
        var idleTimeInReview = await CalculateIdleTimeInReviewAsync(userId, allMergeRequests, projectDataList, cancellationToken);
        var crossTeamCollab = await CalculateCrossTeamCollaborationAsync(userId, allMergeRequests, projectDataList, cancellationToken);

        // Build project summaries
        var projectSummaries = projectDataList.Select(p => new ProjectAdvancedMetricsSummary
        {
            ProjectId = p.ProjectId,
            ProjectName = p.ProjectName,
            MrCount = p.MergeRequests.Count,
            CommitCount = p.Commits.Count,
            FileChangeCount = p.Commits.Sum(c => c.Stats?.Total ?? 0)
        }).ToList();

        return new AdvancedMetricsResult
        {
            BusFactor = busFactor.GiniCoefficient,
            ContributingDevelopersCount = busFactor.DeveloperCount,
            Top3DevelopersFileChangePercentage = busFactor.Top3Percentage,
            ResponseTimeDistribution = responseTimeDistribution.Distribution,
            PeakResponseHour = responseTimeDistribution.PeakHour,
            TotalReviewResponses = responseTimeDistribution.TotalResponses,
            BatchSizeP50 = batchSize.P50,
            BatchSizeP95 = batchSize.P95,
            BatchSizeMrCount = batchSize.MrCount,
            DraftDurationMedianH = draftDuration.MedianHours,
            DraftMrCount = draftDuration.DraftMrCount,
            IterationCountMedian = iterationCount.Median,
            IterationMrCount = iterationCount.MrCount,
            IdleTimeInReviewMedianH = idleTimeInReview.MedianHours,
            IdleTimeMrCount = idleTimeInReview.MrCount,
            CrossTeamCollaborationPercentage = crossTeamCollab.Percentage,
            CrossTeamMrCount = crossTeamCollab.CrossTeamMrCount,
            TotalMrsForCrossTeam = crossTeamCollab.TotalMrCount,
            TeamMappingAvailable = crossTeamCollab.TeamMappingAvailable,
            Projects = projectSummaries
        };
    }

    private Task<BusFactorMetric> CalculateBusFactorAsync(
        long userId,
        IEnumerable<ProjectData> projectDataList,
        CancellationToken cancellationToken)
    {
        // Aggregate file changes by developer (by email) across all commits
        var developerFileChanges = new Dictionary<string, int>();

        foreach (var project in projectDataList)
        {
            foreach (var commit in project.Commits)
            {
                var authorEmail = commit.AuthorEmail;
                if (string.IsNullOrWhiteSpace(authorEmail)) continue;

                var fileChanges = commit.Stats?.Total ?? 0;
                if (developerFileChanges.ContainsKey(authorEmail))
                {
                    developerFileChanges[authorEmail] += fileChanges;
                }
                else
                {
                    developerFileChanges[authorEmail] = fileChanges;
                }
            }
        }

        if (developerFileChanges.Count == 0)
        {
            return Task.FromResult(new BusFactorMetric { GiniCoefficient = 0, DeveloperCount = 0, Top3Percentage = 0 });
        }

        // Calculate Gini coefficient (0 = perfectly equal, 1 = one person does everything)
        var totalChanges = developerFileChanges.Values.Sum();
        if (totalChanges == 0)
        {
            return Task.FromResult(new BusFactorMetric { GiniCoefficient = 0, DeveloperCount = developerFileChanges.Count, Top3Percentage = 0 });
        }

        var sortedChanges = developerFileChanges.Values.OrderBy(x => x).ToList();
        var n = sortedChanges.Count;
        decimal giniNumerator = 0;

        for (var i = 0; i < n; i++)
        {
            giniNumerator += (2 * (i + 1) - n - 1) * sortedChanges[i];
        }

        var giniCoefficient = giniNumerator / (n * totalChanges);

        // Calculate percentage by top 3 developers
        var top3Changes = developerFileChanges.Values.OrderByDescending(x => x).Take(3).Sum();
        var top3Percentage = totalChanges > 0 ? (decimal)top3Changes / totalChanges * 100 : 0;

        return Task.FromResult(new BusFactorMetric
        {
            GiniCoefficient = Math.Abs(giniCoefficient),
            DeveloperCount = developerFileChanges.Count,
            Top3Percentage = top3Percentage
        });
    }

    private async Task<ResponseTimeDistributionMetric> CalculateResponseTimeDistributionAsync(
        long userId,
        IEnumerable<ProjectData> projectDataList,
        CancellationToken cancellationToken)
    {
        var hourDistribution = Enumerable.Range(0, 24).ToDictionary(h => h, h => 0);
        var totalResponses = 0;

        foreach (var project in projectDataList)
        {
            foreach (var mr in project.MergeRequests)
            {
                try
                {
                    // Get discussions/notes for the MR
                    var notes = await _gitLabHttpClient.GetMergeRequestNotesAsync(
                        project.ProjectId,
                        mr.Iid,
                        cancellationToken);

                    // Filter to review comments (not system notes, from the user)
                    var userReviewComments = notes
                        .Where(n => n.Author?.Id == userId && !n.System && n.CreatedAt.HasValue)
                        .ToList();

                    foreach (var comment in userReviewComments)
                    {
                        var hour = comment.CreatedAt!.Value.Hour;
                        hourDistribution[hour]++;
                        totalResponses++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to fetch notes for MR {MrIid} in project {ProjectId}",
                        mr.Iid, project.ProjectId);
                }
            }
        }

        var peakHour = totalResponses > 0
            ? hourDistribution.OrderByDescending(kvp => kvp.Value).First().Key
            : (int?)null;

        return new ResponseTimeDistributionMetric
        {
            Distribution = hourDistribution,
            PeakHour = peakHour,
            TotalResponses = totalResponses
        };
    }

    private BatchSizeMetric CalculateBatchSize(
        long userId,
        List<GitLabMergeRequest> allMergeRequests,
        IEnumerable<ProjectData> projectDataList)
    {
        var commitCounts = new List<int>();

        // Get commit counts for user's MRs
        var userMrs = allMergeRequests.Where(mr => mr.Author?.Id == userId).ToList();

        foreach (var mr in userMrs)
        {
            // Try to use the commits count from the MR data if available via API
            // For now, we'll need to estimate based on project commit data or fetch per MR
            // This is a simplified version - in production, you'd fetch commits per MR
            var project = projectDataList.FirstOrDefault(p => p.ProjectId == mr.ProjectId);
            if (project is not null)
            {
                // Estimate: count commits in the time range of the MR
                // Note: We need to match by email since GitLabCommit doesn't have Author.Id
                var userEmail = mr.Author?.Email;
                if (string.IsNullOrWhiteSpace(userEmail))
                {
                    continue;
                }

                var mrCommits = project.Commits
                    .Where(c => c.AuthorEmail == userEmail &&
                                c.CommittedDate >= mr.CreatedAt &&
                                c.CommittedDate <= (mr.MergedAt ?? mr.UpdatedAt ?? DateTime.UtcNow))
                    .Count();

                if (mrCommits > 0)
                {
                    commitCounts.Add(mrCommits);
                }
            }
        }

        if (commitCounts.Count == 0)
        {
            return new BatchSizeMetric { P50 = null, P95 = null, MrCount = 0 };
        }

        var sorted = commitCounts.OrderBy(x => x).ToList();
        var p50Index = (int)(sorted.Count * 0.5);
        var p95Index = (int)(sorted.Count * 0.95);

        return new BatchSizeMetric
        {
            P50 = sorted[Math.Min(p50Index, sorted.Count - 1)],
            P95 = sorted[Math.Min(p95Index, sorted.Count - 1)],
            MrCount = commitCounts.Count
        };
    }

    private async Task<DraftDurationMetric> CalculateDraftDurationAsync(
        long userId,
        List<GitLabMergeRequest> allMergeRequests,
        IEnumerable<ProjectData> projectDataList,
        CancellationToken cancellationToken)
    {
        var draftDurations = new List<double>();
        var userMrs = allMergeRequests.Where(mr => mr.Author?.Id == userId).ToList();

        foreach (var mr in userMrs)
        {
            // Check if MR was ever in draft/WIP state
            var isDraft = mr.WorkInProgress || 
                          mr.Title?.StartsWith("Draft:", StringComparison.OrdinalIgnoreCase) == true ||
                          mr.Title?.StartsWith("WIP:", StringComparison.OrdinalIgnoreCase) == true;

            if (!isDraft)
            {
                try
                {
                    // Check system notes for draft state changes
                    var notes = await _gitLabHttpClient.GetMergeRequestNotesAsync(
                        mr.ProjectId,
                        mr.Iid,
                        cancellationToken);

                    var draftNotes = notes
                        .Where(n => n.System &&
                                    n.Body is not null &&
                                    (n.Body.Contains("marked as a **Work In Progress**", StringComparison.OrdinalIgnoreCase) ||
                                     n.Body.Contains("marked as **draft**", StringComparison.OrdinalIgnoreCase)))
                        .ToList();

                    var undraftNotes = notes
                        .Where(n => n.System &&
                                    n.Body is not null &&
                                    (n.Body.Contains("unmarked as a **Work In Progress**", StringComparison.OrdinalIgnoreCase) ||
                                     n.Body.Contains("unmarked as **draft**", StringComparison.OrdinalIgnoreCase)))
                        .ToList();

                    if (draftNotes.Any() && undraftNotes.Any())
                    {
                        var draftStart = draftNotes.First().CreatedAt;
                        var draftEnd = undraftNotes.First().CreatedAt;

                        if (draftStart.HasValue && draftEnd.HasValue && draftEnd.Value > draftStart.Value)
                        {
                            var duration = (draftEnd.Value - draftStart.Value).TotalHours;
                            draftDurations.Add(duration);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to fetch draft state for MR {MrIid} in project {ProjectId}",
                        mr.Iid, mr.ProjectId);
                }
            }
        }

        if (draftDurations.Count == 0)
        {
            return new DraftDurationMetric { MedianHours = null, DraftMrCount = 0 };
        }

        var sorted = draftDurations.OrderBy(x => x).ToList();
        var medianIndex = sorted.Count / 2;
        var median = sorted.Count % 2 == 0
            ? (sorted[medianIndex - 1] + sorted[medianIndex]) / 2
            : sorted[medianIndex];

        return new DraftDurationMetric
        {
            MedianHours = (decimal)median,
            DraftMrCount = draftDurations.Count
        };
    }

    private async Task<IterationCountMetric> CalculateIterationCountAsync(
        long userId,
        List<GitLabMergeRequest> allMergeRequests,
        IEnumerable<ProjectData> projectDataList,
        CancellationToken cancellationToken)
    {
        var iterationCounts = new List<int>();
        var userMrs = allMergeRequests.Where(mr => mr.Author?.Id == userId).ToList();

        foreach (var mr in userMrs)
        {
            try
            {
                var discussions = await _gitLabHttpClient.GetMergeRequestDiscussionsAsync(
                    mr.ProjectId,
                    mr.Iid,
                    cancellationToken);

                var commits = await _gitLabHttpClient.GetMergeRequestCommitsAsync(
                    mr.ProjectId,
                    mr.Iid,
                    cancellationToken);

                // Count review cycles: each cycle is review comments followed by new commits
                var iterations = 0;
                var events = new List<(DateTime Time, string Type)>();

                // Add review comments as events
                foreach (var discussion in discussions)
                {
                    if (discussion.Notes is not null)
                    {
                        foreach (var note in discussion.Notes)
                        {
                            if (!note.System && note.Author?.Id != userId && note.CreatedAt.HasValue)
                            {
                                events.Add((note.CreatedAt.Value, "review"));
                            }
                        }
                    }
                }

                // Add commits as events
                foreach (var commit in commits)
                {
                    if (commit.CommittedDate.HasValue)
                    {
                        events.Add((commit.CommittedDate.Value, "commit"));
                    }
                }

                // Sort by time and count review→commit cycles
                var sortedEvents = events.OrderBy(e => e.Time).ToList();
                var lastEventType = "";
                foreach (var evt in sortedEvents)
                {
                    if (evt.Type == "commit" && lastEventType == "review")
                    {
                        iterations++;
                    }
                    lastEventType = evt.Type;
                }

                if (iterations > 0)
                {
                    iterationCounts.Add(iterations);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to calculate iterations for MR {MrIid} in project {ProjectId}",
                    mr.Iid, mr.ProjectId);
            }
        }

        if (iterationCounts.Count == 0)
        {
            return new IterationCountMetric { Median = null, MrCount = 0 };
        }

        var sorted = iterationCounts.OrderBy(x => x).ToList();
        var medianIndex = sorted.Count / 2;
        var median = sorted.Count % 2 == 0
            ? (sorted[medianIndex - 1] + sorted[medianIndex]) / 2.0m
            : sorted[medianIndex];

        return new IterationCountMetric
        {
            Median = median,
            MrCount = iterationCounts.Count
        };
    }

    private async Task<IdleTimeInReviewMetric> CalculateIdleTimeInReviewAsync(
        long userId,
        List<GitLabMergeRequest> allMergeRequests,
        IEnumerable<ProjectData> projectDataList,
        CancellationToken cancellationToken)
    {
        var idleTimes = new List<double>();
        var userMrs = allMergeRequests.Where(mr => mr.Author?.Id == userId).ToList();

        foreach (var mr in userMrs)
        {
            try
            {
                var discussions = await _gitLabHttpClient.GetMergeRequestDiscussionsAsync(
                    mr.ProjectId,
                    mr.Iid,
                    cancellationToken);

                var commits = await _gitLabHttpClient.GetMergeRequestCommitsAsync(
                    mr.ProjectId,
                    mr.Iid,
                    cancellationToken);

                // Find gaps between review comments and next activity (commit or comment)
                var events = new List<(DateTime Time, string Type, bool IsFromAuthor)>();

                // Add review comments and author responses
                foreach (var discussion in discussions)
                {
                    if (discussion.Notes is not null)
                    {
                        foreach (var note in discussion.Notes)
                        {
                            if (!note.System && note.CreatedAt.HasValue)
                            {
                                var isAuthor = note.Author?.Id == userId;
                                events.Add((note.CreatedAt.Value, "comment", isAuthor));
                            }
                        }
                    }
                }

                // Add commits
                foreach (var commit in commits)
                {
                    if (commit.CommittedDate.HasValue)
                    {
                        events.Add((commit.CommittedDate.Value, "commit", true));
                    }
                }

                // Sort by time
                var sortedEvents = events.OrderBy(e => e.Time).ToList();

                // Find idle periods after review comments
                for (var i = 0; i < sortedEvents.Count - 1; i++)
                {
                    var current = sortedEvents[i];
                    var next = sortedEvents[i + 1];

                    // If current is a review comment (not from author) and next is author's activity
                    if (current.Type == "comment" && !current.IsFromAuthor && next.IsFromAuthor)
                    {
                        var idleTime = (next.Time - current.Time).TotalHours;
                        if (idleTime > 0 && idleTime < 24 * 30) // Cap at 30 days to avoid outliers
                        {
                            idleTimes.Add(idleTime);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to calculate idle time for MR {MrIid} in project {ProjectId}",
                    mr.Iid, mr.ProjectId);
            }
        }

        if (idleTimes.Count == 0)
        {
            return new IdleTimeInReviewMetric { MedianHours = null, MrCount = 0 };
        }

        var sorted = idleTimes.OrderBy(x => x).ToList();
        var medianIndex = sorted.Count / 2;
        var median = sorted.Count % 2 == 0
            ? (sorted[medianIndex - 1] + sorted[medianIndex]) / 2
            : sorted[medianIndex];

        return new IdleTimeInReviewMetric
        {
            MedianHours = (decimal)median,
            MrCount = userMrs.Count
        };
    }

    private Task<CrossTeamCollaborationMetric> CalculateCrossTeamCollaborationAsync(
        long userId,
        List<GitLabMergeRequest> allMergeRequests,
        IEnumerable<ProjectData> projectDataList,
        CancellationToken cancellationToken)
    {
        // Team mapping is not yet implemented - return placeholder
        // In the future, this would read from configuration to map users to teams
        var userMrs = allMergeRequests.Where(mr => mr.Author?.Id == userId).ToList();

        return Task.FromResult(new CrossTeamCollaborationMetric
        {
            Percentage = null,
            CrossTeamMrCount = 0,
            TotalMrCount = userMrs.Count,
            TeamMappingAvailable = false
        });
    }

    private AdvancedMetricsResult CreateEmptyResult(GitLabUser user, int windowDays, DateTime windowStart, DateTime windowEnd)
    {
        return new AdvancedMetricsResult
        {
            BusFactor = 0,
            ContributingDevelopersCount = 0,
            Top3DevelopersFileChangePercentage = 0,
            ResponseTimeDistribution = Enumerable.Range(0, 24).ToDictionary(h => h, h => 0),
            PeakResponseHour = null,
            TotalReviewResponses = 0,
            BatchSizeP50 = null,
            BatchSizeP95 = null,
            BatchSizeMrCount = 0,
            DraftDurationMedianH = null,
            DraftMrCount = 0,
            IterationCountMedian = null,
            IterationMrCount = 0,
            IdleTimeInReviewMedianH = null,
            IdleTimeMrCount = 0,
            CrossTeamCollaborationPercentage = null,
            CrossTeamMrCount = 0,
            TotalMrsForCrossTeam = 0,
            TeamMappingAvailable = false,
            Projects = []
        };
    }

    // Internal data structures
    private sealed class ProjectData
    {
        public required long ProjectId { get; init; }
        public required string ProjectName { get; init; }
        public required List<GitLabMergeRequest> MergeRequests { get; init; }
        public required List<GitLabCommit> Commits { get; init; }
    }

    private sealed class BusFactorMetric
    {
        public required decimal GiniCoefficient { get; init; }
        public required int DeveloperCount { get; init; }
        public required decimal Top3Percentage { get; init; }
    }

    private sealed class ResponseTimeDistributionMetric
    {
        public required Dictionary<int, int> Distribution { get; init; }
        public required int? PeakHour { get; init; }
        public required int TotalResponses { get; init; }
    }

    private sealed class BatchSizeMetric
    {
        public required decimal? P50 { get; init; }
        public required decimal? P95 { get; init; }
        public required int MrCount { get; init; }
    }

    private sealed class DraftDurationMetric
    {
        public required decimal? MedianHours { get; init; }
        public required int DraftMrCount { get; init; }
    }

    private sealed class IterationCountMetric
    {
        public required decimal? Median { get; init; }
        public required int MrCount { get; init; }
    }

    private sealed class IdleTimeInReviewMetric
    {
        public required decimal? MedianHours { get; init; }
        public required int MrCount { get; init; }
    }

    private sealed class CrossTeamCollaborationMetric
    {
        public required decimal? Percentage { get; init; }
        public required int CrossTeamMrCount { get; init; }
        public required int TotalMrCount { get; init; }
        public required bool TeamMappingAvailable { get; init; }
    }
}
