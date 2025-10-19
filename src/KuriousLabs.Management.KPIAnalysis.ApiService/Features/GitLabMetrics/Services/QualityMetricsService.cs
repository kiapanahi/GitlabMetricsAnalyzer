using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

/// <summary>
/// Service implementation for calculating quality and reliability metrics from live GitLab data
/// </summary>
public sealed class QualityMetricsService : IQualityMetricsService
{
    private readonly IGitLabHttpClient _gitLabHttpClient;
    private readonly ILogger<QualityMetricsService> _logger;

    public QualityMetricsService(
        IGitLabHttpClient gitLabHttpClient,
        ILogger<QualityMetricsService> logger)
    {
        _gitLabHttpClient = gitLabHttpClient;
        _logger = logger;
    }

    public async Task<QualityMetricsResult> CalculateQualityMetricsAsync(
        long userId,
        int windowDays = 30,
        int revertDetectionDays = 30,
        CancellationToken cancellationToken = default)
    {
        if (windowDays <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(windowDays), windowDays, "Window days must be greater than 0");
        }

        if (revertDetectionDays <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(revertDetectionDays), revertDetectionDays, "Revert detection days must be greater than 0");
        }

        _logger.LogInformation("Calculating quality metrics for user {UserId} over {WindowDays} days", userId, windowDays);

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
            return CreateEmptyResult(user, windowDays, revertDetectionDays, windowStart, windowEnd);
        }

        _logger.LogInformation("Found {ProjectCount} contributed projects for user {UserId}",
            contributedProjects.Count, userId);

        // Fetch MRs and pipelines from all contributed projects in parallel
        var fetchDataTasks = contributedProjects.Select(async project =>
        {
            try
            {
                var mergeRequests = await _gitLabHttpClient.GetMergeRequestsAsync(
                    project.Id,
                    new DateTimeOffset(windowStart),
                    cancellationToken);

                var pipelines = await _gitLabHttpClient.GetPipelinesAsync(
                    project.Id,
                    new DateTimeOffset(windowStart),
                    cancellationToken);

                // Filter MRs by author and within time window
                var userMergeRequests = mergeRequests
                    .Where(mr => mr.Author?.Id == userId)
                    .Where(mr => mr.MergedAt.HasValue && mr.MergedAt.Value >= windowStart && mr.MergedAt.Value <= windowEnd)
                    .ToList();

                // Filter pipelines by author
                var userPipelines = pipelines
                    .Where(p => p.User?.Id == userId)
                    .Where(p => p.CreatedAt.HasValue && p.CreatedAt.Value >= windowStart && p.CreatedAt.Value <= windowEnd)
                    .ToList();

                if (userMergeRequests.Any() || userPipelines.Any())
                {
                    _logger.LogDebug("Found {MrCount} merged MRs and {PipelineCount} pipelines for user {UserId} in project {ProjectId}",
                        userMergeRequests.Count, userPipelines.Count, userId, project.Id);

                    return new ProjectData
                    {
                        ProjectId = project.Id,
                        ProjectName = project.Name ?? "Unknown",
                        MergeRequests = userMergeRequests,
                        Pipelines = userPipelines
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch data for project {ProjectId}", project.Id);
                return null;
            }
        });

        var projectDataResults = await Task.WhenAll(fetchDataTasks);
        var projectData = projectDataResults.Where(pd => pd is not null).ToList();

        if (!projectData.Any())
        {
            _logger.LogWarning("No data found for user {UserId} in any project", userId);
            return CreateEmptyResult(user, windowDays, revertDetectionDays, windowStart, windowEnd);
        }

        // Aggregate all MRs and pipelines
        var allMergeRequests = projectData.SelectMany(pd => pd!.MergeRequests).ToList();
        var allPipelines = projectData.SelectMany(pd => pd!.Pipelines).ToList();

        _logger.LogInformation("Processing {MrCount} merged MRs and {PipelineCount} pipelines for user {UserId}",
            allMergeRequests.Count, allPipelines.Count, userId);

        // Calculate metrics
        var reworkMetrics = await CalculateReworkMetrics(allMergeRequests, cancellationToken);
        var revertMetrics = CalculateRevertMetrics(allMergeRequests, revertDetectionDays);
        var ciMetrics = CalculateCiMetrics(allPipelines);
        var pipelineDurationMetrics = CalculatePipelineDurationMetrics(allPipelines);
        var coverageMetrics = CalculateCoverageMetrics(allPipelines);
        var hotfixMetrics = CalculateHotfixMetrics(allMergeRequests);
        var conflictMetrics = CalculateConflictMetrics(allMergeRequests);

        // Build project summaries
        var projectSummaries = projectData.Select(pd => new ProjectQualitySummary
        {
            ProjectId = pd!.ProjectId,
            ProjectName = pd.ProjectName,
            MergedMrCount = pd.MergeRequests.Count,
            PipelineCount = pd.Pipelines.Count
        }).ToList();

        return new QualityMetricsResult
        {
            UserId = userId,
            Username = user.Username ?? "Unknown",
            WindowDays = windowDays,
            WindowStart = windowStart,
            WindowEnd = windowEnd,
            MergedMrCount = allMergeRequests.Count,
            ReworkRatio = reworkMetrics.Ratio,
            ReworkMrCount = reworkMetrics.Count,
            RevertRate = revertMetrics.Rate,
            RevertedMrCount = revertMetrics.Count,
            RevertDetectionDays = revertDetectionDays,
            CiSuccessRate = ciMetrics.SuccessRate,
            SuccessfulPipelinesFirstRun = ciMetrics.SuccessfulCount,
            TotalFirstRunPipelines = ciMetrics.TotalCount,
            PipelineDurationP50Min = pipelineDurationMetrics.P50,
            PipelineDurationP95Min = pipelineDurationMetrics.P95,
            PipelinesWithDurationCount = pipelineDurationMetrics.Count,
            TestCoveragePercent = coverageMetrics.AverageCoverage,
            PipelinesWithCoverageCount = coverageMetrics.Count,
            HotfixRate = hotfixMetrics.Rate,
            HotfixMrCount = hotfixMetrics.Count,
            ConflictRate = conflictMetrics.Rate,
            ConflictMrCount = conflictMetrics.Count,
            Projects = projectSummaries
        };
    }

    private async Task<(decimal Ratio, int Count)> CalculateReworkMetrics(
        List<GitLabMergeRequest> mergeRequests,
        CancellationToken cancellationToken)
    {
        if (!mergeRequests.Any())
        {
            return (0, 0);
        }

        var reworkCount = 0;

        // For each MR, check if there were commits after first review
        foreach (var mr in mergeRequests)
        {
            try
            {
                // Get commits for the MR
                var commits = await _gitLabHttpClient.GetMergeRequestCommitsAsync(
                    mr.ProjectId,
                    mr.Iid,
                    cancellationToken);

                if (!commits.Any())
                {
                    continue;
                }

                // Get notes/discussions to find first review timestamp
                var notes = await _gitLabHttpClient.GetMergeRequestNotesAsync(
                    mr.ProjectId,
                    mr.Iid,
                    cancellationToken);

                // First review is first non-author comment
                var firstReview = notes
                    .Where(n => n.Author?.Id != mr.Author?.Id)
                    .OrderBy(n => n.CreatedAt)
                    .FirstOrDefault();

                if (firstReview is not null)
                {
                    var firstReviewTime = firstReview.CreatedAt;

                    // Check if any commits came after first review
                    var commitsAfterReview = commits
                        .Where(c => c.CommittedDate.HasValue && c.CommittedDate.Value > firstReviewTime)
                        .ToList();

                    if (commitsAfterReview.Any())
                    {
                        reworkCount++;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to check rework for MR {MrIid} in project {ProjectId}",
                    mr.Iid, mr.ProjectId);
            }
        }

        var ratio = (decimal)reworkCount / mergeRequests.Count;
        return (ratio, reworkCount);
    }

    private (decimal Rate, int Count) CalculateRevertMetrics(
        List<GitLabMergeRequest> mergeRequests,
        int revertDetectionDays)
    {
        if (!mergeRequests.Any())
        {
            return (0, 0);
        }

        // Check title for revert patterns
        var revertCount = mergeRequests.Count(mr =>
            mr.Title is not null &&
            (mr.Title.Contains("revert", StringComparison.OrdinalIgnoreCase) ||
             mr.Title.StartsWith("Revert", StringComparison.Ordinal)));

        var rate = (decimal)revertCount / mergeRequests.Count;
        return (rate, revertCount);
    }

    private (decimal? SuccessRate, int SuccessfulCount, int TotalCount) CalculateCiMetrics(
        List<GitLabPipeline> pipelines)
    {
        if (!pipelines.Any())
        {
            return (null, 0, 0);
        }

        // Group pipelines by SHA to identify first runs
        var pipelinesBySha = pipelines
            .GroupBy(p => p.Sha)
            .Select(g => g.OrderBy(p => p.CreatedAt).First())
            .ToList();

        var successfulFirstRuns = pipelinesBySha
            .Count(p => p.Status?.Equals("success", StringComparison.OrdinalIgnoreCase) ?? false);

        var successRate = (decimal)successfulFirstRuns / pipelinesBySha.Count;
        return (successRate, successfulFirstRuns, pipelinesBySha.Count);
    }

    private (decimal? P50, decimal? P95, int Count) CalculatePipelineDurationMetrics(
        List<GitLabPipeline> pipelines)
    {
        var durationsInSeconds = pipelines
            .Where(p => p.UpdatedAt.HasValue && p.CreatedAt.HasValue)
            .Select(p => (p.UpdatedAt!.Value - p.CreatedAt!.Value).TotalSeconds)
            .Where(d => d > 0)
            .OrderBy(d => d)
            .ToList();

        if (!durationsInSeconds.Any())
        {
            return (null, null, 0);
        }

        var p50Index = (int)Math.Ceiling(durationsInSeconds.Count * 0.5) - 1;
        var p95Index = (int)Math.Ceiling(durationsInSeconds.Count * 0.95) - 1;

        var p50Minutes = (decimal)(durationsInSeconds[p50Index] / 60.0);
        var p95Minutes = (decimal)(durationsInSeconds[p95Index] / 60.0);

        return (p50Minutes, p95Minutes, durationsInSeconds.Count);
    }

    private (decimal? AverageCoverage, int Count) CalculateCoverageMetrics(
        List<GitLabPipeline> pipelines)
    {
        // Note: Coverage field is not yet implemented in the Pipeline model
        // This is a placeholder for when pipeline details are fetched
        // For now, return null as coverage data is not available
        return (null, 0);
    }

    private (decimal Rate, int Count) CalculateHotfixMetrics(
        List<GitLabMergeRequest> mergeRequests)
    {
        if (!mergeRequests.Any())
        {
            return (0, 0);
        }

        var hotfixCount = mergeRequests.Count(mr =>
            (mr.Labels?.Any(l => l.Contains("hotfix", StringComparison.OrdinalIgnoreCase)) ?? false) ||
            (mr.Title?.Contains("hotfix", StringComparison.OrdinalIgnoreCase) ?? false) ||
            (mr.SourceBranch?.Contains("hotfix", StringComparison.OrdinalIgnoreCase) ?? false) ||
            (mr.SourceBranch?.Contains("hot-fix", StringComparison.OrdinalIgnoreCase) ?? false));

        var rate = (decimal)hotfixCount / mergeRequests.Count;
        return (rate, hotfixCount);
    }

    private (decimal Rate, int Count) CalculateConflictMetrics(
        List<GitLabMergeRequest> mergeRequests)
    {
        if (!mergeRequests.Any())
        {
            return (0, 0);
        }

        var conflictCount = mergeRequests.Count(mr => mr.HasConflicts);
        var rate = (decimal)conflictCount / mergeRequests.Count;
        return (rate, conflictCount);
    }

    private QualityMetricsResult CreateEmptyResult(
        GitLabUser user,
        int windowDays,
        int revertDetectionDays,
        DateTime windowStart,
        DateTime windowEnd)
    {
        return new QualityMetricsResult
        {
            UserId = user.Id,
            Username = user.Username ?? "Unknown",
            WindowDays = windowDays,
            WindowStart = windowStart,
            WindowEnd = windowEnd,
            MergedMrCount = 0,
            ReworkRatio = 0,
            ReworkMrCount = 0,
            RevertRate = 0,
            RevertedMrCount = 0,
            RevertDetectionDays = revertDetectionDays,
            CiSuccessRate = null,
            SuccessfulPipelinesFirstRun = 0,
            TotalFirstRunPipelines = 0,
            PipelineDurationP50Min = null,
            PipelineDurationP95Min = null,
            PipelinesWithDurationCount = 0,
            TestCoveragePercent = null,
            PipelinesWithCoverageCount = 0,
            HotfixRate = 0,
            HotfixMrCount = 0,
            ConflictRate = 0,
            ConflictMrCount = 0,
            Projects = new List<ProjectQualitySummary>()
        };
    }

    private sealed class ProjectData
    {
        public required long ProjectId { get; init; }
        public required string ProjectName { get; init; }
        public required List<GitLabMergeRequest> MergeRequests { get; init; }
        public required List<GitLabPipeline> Pipelines { get; init; }
    }
}
