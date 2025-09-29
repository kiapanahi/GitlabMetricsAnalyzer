using Microsoft.EntityFrameworkCore;

using Toman.Management.KPIAnalysis.ApiService.Configuration;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Data;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

/// <summary>
/// Implementation of per-developer metrics computation service
/// </summary>
public sealed class PerDeveloperMetricsComputationService : IPerDeveloperMetricsComputationService
{
    private readonly GitLabMetricsDbContext _dbContext;
    private readonly IIdentityMappingService _identityService;
    private readonly ILogger<PerDeveloperMetricsComputationService> _logger;
    private readonly MetricsConfiguration _config;

    private static readonly IReadOnlyList<int> SupportedWindows = new[] { 14, 28, 90 };
    private const int MinSampleSize = 5;
    private const decimal WinsorizationPercentile = 0.05m; // 5% winsorization

    public PerDeveloperMetricsComputationService(
        GitLabMetricsDbContext dbContext,
        IIdentityMappingService identityService,
        ILogger<PerDeveloperMetricsComputationService> logger,
        Microsoft.Extensions.Options.IOptions<MetricsConfiguration> config)
    {
        _dbContext = dbContext;
        _identityService = identityService;
        _logger = logger;
        _config = config.Value;
    }

    public IReadOnlyList<int> GetSupportedWindowDays() => SupportedWindows;

    public async Task<PerDeveloperMetricsResult> ComputeMetricsAsync(long developerId, MetricsComputationOptions options, CancellationToken cancellationToken = default)
    {
        ValidateOptions(options);

        _logger.LogDebug("Computing metrics for developer {DeveloperId} with {WindowDays}-day window ending {EndDate}",
            developerId, options.WindowDays, options.EndDate);

        var windowStart = options.EndDate.AddDays(-options.WindowDays);

        // Get developer information
        var developer = await GetDeveloperInfoAsync(developerId, cancellationToken);
        if (developer is null)
        {
            throw new ArgumentException($"Developer with ID {developerId} not found", nameof(developerId));
        }

        // Filter projects if specified
        var projectIds = await GetFilteredProjectIdsAsync(options.ProjectIds, cancellationToken);

        // Fetch raw data
        var rawData = await FetchRawDataAsync(developerId, windowStart, options.EndDate, projectIds, cancellationToken);

        // Apply exclusions
        var filteredData = ApplyExclusions(rawData, options);

        // Compute metrics
        var metrics = await ComputeMetricsFromDataAsync(filteredData, options, cancellationToken);

        // Generate audit information
        var audit = GenerateAudit(rawData, filteredData, metrics);

        return new PerDeveloperMetricsResult
        {
            DeveloperId = developerId,
            DeveloperName = developer.Username,
            DeveloperEmail = developer.Email,
            ComputationDate = DateTimeOffset.UtcNow,
            WindowStart = windowStart,
            WindowEnd = options.EndDate,
            WindowDays = options.WindowDays,
            Metrics = metrics,
            Audit = audit
        };
    }

    public async Task<Dictionary<long, PerDeveloperMetricsResult>> ComputeMetricsAsync(IEnumerable<long> developerIds, MetricsComputationOptions options, CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<long, PerDeveloperMetricsResult>();

        foreach (var developerId in developerIds)
        {
            try
            {
                var result = await ComputeMetricsAsync(developerId, options, cancellationToken);
                results[developerId] = result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to compute metrics for developer {DeveloperId}", developerId);
                // Continue with other developers
            }
        }

        return results;
    }

    private static void ValidateOptions(MetricsComputationOptions options)
    {
        if (!SupportedWindows.Contains(options.WindowDays))
        {
            throw new ArgumentException($"Unsupported window size: {options.WindowDays}. Supported windows: {string.Join(", ", SupportedWindows)}", nameof(options));
        }
    }

    private async Task<DeveloperInfo?> GetDeveloperInfoAsync(long developerId, CancellationToken cancellationToken)
    {
        // Try to get from commits first
        var commit = await _dbContext.RawCommits
            .Where(c => c.AuthorUserId == developerId)
            .Select(c => new DeveloperInfo(c.AuthorName, c.AuthorEmail))
            .FirstOrDefaultAsync(cancellationToken);

        if (commit is not null)
            return commit;

        // Try merge requests
        var mr = await _dbContext.RawMergeRequests
            .Where(mr => mr.AuthorUserId == developerId)
            .Select(mr => new DeveloperInfo(mr.AuthorName, "unknown@example.com")) // AuthorEmail not available in MR table
            .FirstOrDefaultAsync(cancellationToken);

        return mr;
    }

    private async Task<IReadOnlyList<long>> GetFilteredProjectIdsAsync(IReadOnlyList<long> requestedProjectIds, CancellationToken cancellationToken)
    {
        // If specific projects requested, use those
        if (requestedProjectIds.Count > 0)
        {
            return requestedProjectIds;
        }

        // Otherwise get all accessible projects from CommitFacts or MergeRequestFacts
        var allProjectIds = await _dbContext.RawCommits
            .Select(c => c.ProjectId)
            .Union(_dbContext.RawMergeRequests.Select(mr => (long)mr.ProjectId))
            .Distinct()
            .ToListAsync(cancellationToken);

        // Apply project scope filtering if configured
        if (_config.ProjectScope is not null)
        {
            return ApplyProjectScopeFiltering(allProjectIds);
        }

        return allProjectIds;
    }

    private IReadOnlyList<long> ApplyProjectScopeFiltering(List<long> allProjectIds)
    {
        var scope = _config.ProjectScope!;
        var filtered = allProjectIds.AsEnumerable();

        // Apply include filters
        if (scope.IncludeProjects.Count > 0)
        {
            filtered = filtered.Where(id => scope.IncludeProjects.Contains(id));
        }

        // Apply exclude filters
        if (scope.ExcludeProjects.Count > 0)
        {
            filtered = filtered.Where(id => !scope.ExcludeProjects.Contains(id));
        }

        // TODO: Apply regex patterns for project names if needed
        
        return filtered.ToList();
    }

    private async Task<RawMetricsData> FetchRawDataAsync(long developerId, DateTimeOffset windowStart, DateTimeOffset windowEnd, IReadOnlyList<long> projectIds, CancellationToken cancellationToken)
    {
        // Fetch data sequentially to avoid concurrent DbContext usage
        var commits = await _dbContext.RawCommits
            .Where(c => c.AuthorUserId == developerId &&
                       c.CommittedAt >= windowStart &&
                       c.CommittedAt < windowEnd &&
                       projectIds.Contains(c.ProjectId))
            .ToListAsync(cancellationToken);

        var mergeRequests = await _dbContext.RawMergeRequests
            .Where(mr => mr.AuthorUserId == developerId &&
                        mr.CreatedAt >= windowStart &&
                        mr.CreatedAt < windowEnd &&
                        projectIds.Contains((int)mr.ProjectId))
            .ToListAsync(cancellationToken);

        var pipelines = await _dbContext.RawPipelines
            .Where(p => p.AuthorUserId == developerId &&
                       p.CreatedAt >= windowStart &&
                       p.CreatedAt < windowEnd &&
                       projectIds.Contains(p.ProjectId))
            .ToListAsync(cancellationToken);

        // TODO: Add review events when available

        return new RawMetricsData
        {
            Commits = commits,
            MergeRequests = mergeRequests,
            Pipelines = pipelines,
            ReviewEvents = [] // TODO: Implement when review events are available
        };
    }

    private RawMetricsData ApplyExclusions(RawMetricsData rawData, MetricsComputationOptions options)
    {
        if (!options.ApplyFileExclusions)
            return rawData;

        var excludes = _config.Excludes;
        
        // Apply commit exclusions (no branch field in RawCommit, use source/target branches from MRs for reference)
        var filteredCommits = rawData.Commits.Where(c => 
            !IsExcludedCommit(c.Message, excludes.CommitPatterns)).ToList();

        // Apply merge request exclusions  
        var filteredMRs = rawData.MergeRequests.Where(mr =>
            !IsExcludedBranch(mr.SourceBranch, excludes.BranchPatterns) &&
            !IsExcludedBranch(mr.TargetBranch, excludes.BranchPatterns)).ToList();

        return new RawMetricsData
        {
            Commits = filteredCommits,
            MergeRequests = filteredMRs,
            Pipelines = rawData.Pipelines, // No pipeline exclusions for now
            ReviewEvents = rawData.ReviewEvents
        };
    }

    private static bool IsExcludedCommit(string message, List<string> patterns)
    {
        return patterns.Any(pattern => System.Text.RegularExpressions.Regex.IsMatch(message, pattern));
    }

    private static bool IsExcludedBranch(string branch, List<string> patterns)
    {
        return patterns.Any(pattern => System.Text.RegularExpressions.Regex.IsMatch(branch, pattern));
    }

    private async Task<PerDeveloperMetrics> ComputeMetricsFromDataAsync(RawMetricsData data, MetricsComputationOptions options, CancellationToken cancellationToken)
    {
        // Compute median-based metrics
        var mrCycleTimeP50H = ComputeMergeRequestCycleTimeP50(data.MergeRequests);
        var timeToFirstReviewP50H = ComputeTimeToFirstReviewP50(data.MergeRequests);
        var timeInReviewP50H = ComputeTimeInReviewP50(data.MergeRequests);
        var wipAgeP50H = ComputeWipAgeP50(data.MergeRequests);
        var wipAgeP90H = ComputeWipAgeP90(data.MergeRequests);
        
        // Compute rate-based metrics
        var pipelineSuccessRate = ComputePipelineSuccessRate(data.Pipelines);
        var approvalBypassRatio = ComputeApprovalBypassRatio(data.MergeRequests);
        var reworkRate = ComputeReworkRate(data.MergeRequests, data.Commits);
        var signedCommitRatio = ComputeSignedCommitRatio(data.Commits);

        // Compute count-based metrics (weekly rates)
        var mrThroughputWk = ComputeMergeRequestThroughputWeekly(data.MergeRequests, options.WindowDays);
        var wipMrCount = ComputeWipMergeRequestCount(data.MergeRequests);

        // Compute duration metrics
        var avgPipelineDurationSec = ComputeAveragePipelineDuration(data.Pipelines);
        var meanTimeToGreenSec = ComputeMeanTimeToGreen(data.Pipelines);

        // Apply winsorization if requested
        var metrics = new PerDeveloperMetrics
        {
            MrCycleTimeP50H = mrCycleTimeP50H,
            TimeToFirstReviewP50H = timeToFirstReviewP50H,
            TimeInReviewP50H = timeInReviewP50H,
            WipAgeP50H = wipAgeP50H,
            WipAgeP90H = wipAgeP90H,
            PipelineSuccessRate = pipelineSuccessRate,
            ApprovalBypassRatio = approvalBypassRatio,
            ReworkRate = reworkRate,
            SignedCommitRatio = signedCommitRatio,
            MrThroughputWk = mrThroughputWk,
            WipMrCount = wipMrCount,
            AvgPipelineDurationSec = avgPipelineDurationSec,
            MeanTimeToGreenSec = meanTimeToGreenSec,
            
            // Computed remaining metrics with basic implementations
            DeploymentFrequencyWk = ComputeDeploymentFrequency(data.Pipelines, options.WindowDays),
            ReleasesCadenceWk = ComputeReleasesCadence(data.MergeRequests, options.WindowDays),
            FlakyJobRate = ComputeFlakyJobRate(data.Pipelines),
            RollbackIncidence = ComputeRollbackIncidence(data.MergeRequests),
            DirectPushesDefault = ComputeDirectPushesToDefault(data.Commits, data.MergeRequests),
            ForcePushesProtected = 0, // TODO: Implement when force push data is available
            BranchTtlP50H = ComputeBranchTtlP50(data.MergeRequests),
            BranchTtlP90H = ComputeBranchTtlP90(data.MergeRequests),
            IssueSlaBreachRate = null, // TODO: Implement when issue SLA data is available
            ReopenedIssueRate = null, // TODO: Implement when issue data is available
            DefectEscapeRate = null // TODO: Implement when defect tracking is available
        };

        if (options.ApplyWinsorization)
        {
            metrics = ApplyWinsorization(metrics);
        }

        return metrics;
    }

    // Metric computation methods
    private static decimal? ComputeMergeRequestCycleTimeP50(List<Models.Raw.RawMergeRequest> mergeRequests)
    {
        var cycleTimes = mergeRequests
            .Where(mr => mr.MergedAt.HasValue)
            .Select(mr => (mr.MergedAt!.Value - mr.CreatedAt).TotalHours)
            .Where(hours => hours > 0)
            .OrderBy(x => x)
            .ToList();

        return cycleTimes.Count > 0 ? (decimal)ComputePercentile(cycleTimes, 0.5) : null;
    }

    private static decimal? ComputeTimeToFirstReviewP50(List<Models.Raw.RawMergeRequest> mergeRequests)
    {
        // TODO: Implement when review data is available
        return null;
    }

    private static decimal? ComputeTimeInReviewP50(List<Models.Raw.RawMergeRequest> mergeRequests)
    {
        // TODO: Implement when review data is available  
        return null;
    }

    private static decimal? ComputeWipAgeP50(List<Models.Raw.RawMergeRequest> mergeRequests)
    {
        var now = DateTimeOffset.UtcNow;
        var wipAges = mergeRequests
            .Where(mr => mr.State == "opened" && (mr.Title.Contains("WIP") || mr.Title.Contains("Draft")))
            .Select(mr => (now - mr.CreatedAt).TotalHours)
            .Where(hours => hours > 0)
            .OrderBy(x => x)
            .ToList();

        return wipAges.Count > 0 ? (decimal)ComputePercentile(wipAges, 0.5) : null;
    }

    private static decimal? ComputeWipAgeP90(List<Models.Raw.RawMergeRequest> mergeRequests)
    {
        var now = DateTimeOffset.UtcNow;
        var wipAges = mergeRequests
            .Where(mr => mr.State == "opened" && (mr.Title.Contains("WIP") || mr.Title.Contains("Draft")))
            .Select(mr => (now - mr.CreatedAt).TotalHours)
            .Where(hours => hours > 0)
            .OrderBy(x => x)
            .ToList();

        return wipAges.Count > 0 ? (decimal)ComputePercentile(wipAges, 0.9) : null;
    }

    private static decimal? ComputePipelineSuccessRate(List<Models.Raw.RawPipeline> pipelines)
    {
        if (pipelines.Count == 0) return null;

        var successCount = pipelines.Count(p => p.Status == "success");
        return (decimal)successCount / pipelines.Count;
    }

    private static decimal? ComputeApprovalBypassRatio(List<Models.Raw.RawMergeRequest> mergeRequests)
    {
        if (mergeRequests.Count == 0) return null;

        // TODO: Implement when approval data is available
        return 0m;
    }

    private static decimal? ComputeReworkRate(List<Models.Raw.RawMergeRequest> mergeRequests, List<Models.Raw.RawCommit> commits)
    {
        if (mergeRequests.Count == 0) return null;

        // Simplified rework detection based on commit patterns after MR creation (no branch mapping available)
        var reworkCount = 0;
        foreach (var mr in mergeRequests.Where(mr => mr.MergedAt.HasValue))
        {
            var mrCommits = commits.Where(c => c.CommittedAt > mr.CreatedAt && c.CommittedAt <= (mr.MergedAt ?? DateTimeOffset.MaxValue)).ToList();
            if (mrCommits.Any(c => c.Message.ToLowerInvariant().Contains("fix") || c.Message.ToLowerInvariant().Contains("rework")))
            {
                reworkCount++;
            }
        }

        return (decimal)reworkCount / mergeRequests.Count * 100;
    }

    private static decimal? ComputeSignedCommitRatio(List<Models.Raw.RawCommit> commits)
    {
        if (commits.Count == 0) return null;

        // TODO: Implement when commit signature data is available
        return 0m;
    }

    private static int ComputeMergeRequestThroughputWeekly(List<Models.Raw.RawMergeRequest> mergeRequests, int windowDays)
    {
        var mergedCount = mergeRequests.Count(mr => mr.MergedAt.HasValue);
        return (int)(mergedCount * 7.0 / windowDays); // Convert to weekly rate
    }

    private static int ComputeWipMergeRequestCount(List<Models.Raw.RawMergeRequest> mergeRequests)
    {
        return mergeRequests.Count(mr => mr.State == "opened" && 
                                        (mr.Title.Contains("WIP") || mr.Title.Contains("Draft")));
    }

    private static decimal? ComputeAveragePipelineDuration(List<Models.Raw.RawPipeline> pipelines)
    {
        var durations = pipelines
            .Where(p => p.DurationSec > 0)
            .Select(p => (double)p.DurationSec)
            .ToList();

        return durations.Count > 0 ? (decimal)durations.Average() : null;
    }

    private static decimal? ComputeMeanTimeToGreen(List<Models.Raw.RawPipeline> pipelines)
    {
        // TODO: Implement MTTG calculation based on pipeline retry patterns
        return null;
    }

    private static int ComputeDeploymentFrequency(List<Models.Raw.RawPipeline> pipelines, int windowDays)
    {
        // Count successful pipelines that likely represent deployments (success status)
        var deployments = pipelines.Count(p => p.Status == "success" && 
                                               (p.Environment is not null || p.Ref == "main" || p.Ref == "master"));
        return (int)(deployments * 7.0 / windowDays); // Convert to weekly rate
    }

    private static int ComputeReleasesCadence(List<Models.Raw.RawMergeRequest> mergeRequests, int windowDays)
    {
        // Estimate releases based on MRs to main/master with "release" keywords
        var releases = mergeRequests.Count(mr => mr.State == "merged" &&
                                                 (mr.TargetBranch == "main" || mr.TargetBranch == "master") &&
                                                 (mr.Title.ToLowerInvariant().Contains("release") || 
                                                  mr.Title.ToLowerInvariant().Contains("version")));
        return (int)(releases * 7.0 / windowDays); // Convert to weekly rate
    }

    private static decimal? ComputeFlakyJobRate(List<Models.Raw.RawPipeline> pipelines)
    {
        if (pipelines.Count == 0) return null;

        // Simplified flaky detection: pipelines that have both success and failure on same commit/ref
        var groupedByRef = pipelines.GroupBy(p => p.Ref).ToList();
        var flakyRefs = 0;

        foreach (var group in groupedByRef)
        {
            var statuses = group.Select(p => p.Status).Distinct().ToList();
            if (statuses.Contains("success") && (statuses.Contains("failed") || statuses.Contains("canceled")))
            {
                flakyRefs++;
            }
        }

        return groupedByRef.Count > 0 ? (decimal)flakyRefs / groupedByRef.Count : 0;
    }

    private static int ComputeRollbackIncidence(List<Models.Raw.RawMergeRequest> mergeRequests)
    {
        // Detect rollbacks based on title/branch patterns
        return mergeRequests.Count(mr => mr.State == "merged" &&
                                        (mr.Title.ToLowerInvariant().Contains("rollback") ||
                                         mr.Title.ToLowerInvariant().Contains("revert") ||
                                         mr.SourceBranch.ToLowerInvariant().Contains("rollback")));
    }

    private static int ComputeDirectPushesToDefault(List<Models.Raw.RawCommit> commits, List<Models.Raw.RawMergeRequest> mergeRequests)
    {
        // Estimate direct pushes as commits without associated merge requests
        // This is a simplified heuristic since we don't have branch information for commits
        var commitsWithMRs = new HashSet<string>();
        
        foreach (var mr in mergeRequests)
        {
            // Add estimated commits for this MR (simplified)
            if (mr.FirstCommitSha is not null)
            {
                commitsWithMRs.Add(mr.FirstCommitSha);
            }
        }

        return commits.Count(c => !commitsWithMRs.Contains(c.CommitId));
    }

    private static decimal? ComputeBranchTtlP50(List<Models.Raw.RawMergeRequest> mergeRequests)
    {
        var branchLifetimes = mergeRequests
            .Where(mr => mr.MergedAt.HasValue || mr.ClosedAt.HasValue)
            .Select(mr => 
            {
                var endDate = mr.MergedAt ?? mr.ClosedAt!.Value;
                return (endDate - mr.CreatedAt).TotalHours;
            })
            .Where(hours => hours > 0)
            .OrderBy(x => x)
            .ToList();

        return branchLifetimes.Count > 0 ? (decimal)ComputePercentile(branchLifetimes, 0.5) : null;
    }

    private static decimal? ComputeBranchTtlP90(List<Models.Raw.RawMergeRequest> mergeRequests)
    {
        var branchLifetimes = mergeRequests
            .Where(mr => mr.MergedAt.HasValue || mr.ClosedAt.HasValue)
            .Select(mr => 
            {
                var endDate = mr.MergedAt ?? mr.ClosedAt!.Value;
                return (endDate - mr.CreatedAt).TotalHours;
            })
            .Where(hours => hours > 0)
            .OrderBy(x => x)
            .ToList();

        return branchLifetimes.Count > 0 ? (decimal)ComputePercentile(branchLifetimes, 0.9) : null;
    }

    private static double ComputePercentile(List<double> sortedValues, double percentile)
    {
        if (sortedValues.Count == 0) return 0;

        var index = percentile * (sortedValues.Count - 1);
        var lower = Math.Floor(index);
        var upper = Math.Ceiling(index);

        if (lower == upper)
            return sortedValues[(int)lower];

        return sortedValues[(int)lower] * (upper - index) + sortedValues[(int)upper] * (index - lower);
    }

    private static PerDeveloperMetrics ApplyWinsorization(PerDeveloperMetrics metrics)
    {
        // TODO: Implement winsorization for extreme outliers
        return metrics;
    }

    private static MetricsAudit GenerateAudit(RawMetricsData rawData, RawMetricsData filteredData, PerDeveloperMetrics metrics)
    {
        return new MetricsAudit
        {
            HasMergeRequestData = rawData.MergeRequests.Count > 0,
            HasPipelineData = rawData.Pipelines.Count > 0,
            HasCommitData = rawData.Commits.Count > 0,
            HasReviewData = rawData.ReviewEvents.Count > 0,

            LowMergeRequestCount = rawData.MergeRequests.Count < MinSampleSize,
            LowPipelineCount = rawData.Pipelines.Count < MinSampleSize,
            LowCommitCount = rawData.Commits.Count < MinSampleSize,
            LowReviewCount = rawData.ReviewEvents.Count < MinSampleSize,

            TotalMergeRequests = rawData.MergeRequests.Count,
            TotalPipelines = rawData.Pipelines.Count,
            TotalCommits = rawData.Commits.Count,
            TotalReviews = rawData.ReviewEvents.Count,

            ExcludedFiles = rawData.Commits.Count - filteredData.Commits.Count,
            WinsorizedMetrics = 0, // TODO: Count winsorized metrics

            DataQuality = DetermineDataQuality(rawData),
            HasSufficientData = rawData.MergeRequests.Count >= MinSampleSize || 
                               rawData.Commits.Count >= MinSampleSize,

            NullReasons = GenerateNullReasons(rawData, metrics)
        };
    }

    private static string DetermineDataQuality(RawMetricsData data)
    {
        var totalDataPoints = data.Commits.Count + data.MergeRequests.Count + data.Pipelines.Count;
        return totalDataPoints switch
        {
            >= 50 => "Excellent",
            >= 20 => "Good", 
            >= 10 => "Fair",
            _ => "Poor"
        };
    }

    private static Dictionary<string, string> GenerateNullReasons(RawMetricsData data, PerDeveloperMetrics metrics)
    {
        var reasons = new Dictionary<string, string>();

        if (metrics.MrCycleTimeP50H is null)
            reasons["MrCycleTimeP50H"] = data.MergeRequests.Count == 0 ? "No merge requests in window" : "No merged merge requests in window";

        if (metrics.PipelineSuccessRate is null)
            reasons["PipelineSuccessRate"] = "No pipeline executions in window";

        if (metrics.TimeToFirstReviewP50H is null)
            reasons["TimeToFirstReviewP50H"] = "Review data not available";

        if (metrics.TimeInReviewP50H is null)
            reasons["TimeInReviewP50H"] = "Review data not available";

        if (metrics.WipAgeP50H is null)
            reasons["WipAgeP50H"] = "No WIP merge requests found";

        if (metrics.FlakyJobRate is null)
            reasons["FlakyJobRate"] = "Insufficient pipeline data for flaky detection";

        if (metrics.BranchTtlP50H is null)
            reasons["BranchTtlP50H"] = "No completed branches (merged/closed MRs) in window";

        if (metrics.IssueSlaBreachRate is null)
            reasons["IssueSlaBreachRate"] = "Issue SLA data not available";

        if (metrics.ReopenedIssueRate is null)
            reasons["ReopenedIssueRate"] = "Issue data not available";

        if (metrics.DefectEscapeRate is null)
            reasons["DefectEscapeRate"] = "Defect tracking data not available";

        if (metrics.MeanTimeToGreenSec is null)
            reasons["MeanTimeToGreenSec"] = "Pipeline retry patterns not implemented";

        return reasons;
    }

    // Helper records
    private sealed record DeveloperInfo(string Username, string Email);
    
    private sealed record RawMetricsData
    {
        public List<Models.Raw.RawCommit> Commits { get; init; } = [];
        public List<Models.Raw.RawMergeRequest> MergeRequests { get; init; } = [];
        public List<Models.Raw.RawPipeline> Pipelines { get; init; } = [];
        public List<object> ReviewEvents { get; init; } = []; // TODO: Use proper review event type
    }
}
