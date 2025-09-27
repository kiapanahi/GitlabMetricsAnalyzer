namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

/// <summary>
/// Service for computing the PRD's 30 per-developer metrics over rolling windows
/// with support for winsorization, file exclusions, and audit tracking
/// </summary>
public interface IPerDeveloperMetricsComputationService
{
    /// <summary>
    /// Compute all metrics for a specific developer over a rolling window
    /// </summary>
    Task<PerDeveloperMetricsResult> ComputeMetricsAsync(long developerId, MetricsComputationOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Compute all metrics for multiple developers over a rolling window
    /// </summary>
    Task<Dictionary<long, PerDeveloperMetricsResult>> ComputeMetricsAsync(IEnumerable<long> developerIds, MetricsComputationOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get available metrics computation windows
    /// </summary>
    IReadOnlyList<int> GetSupportedWindowDays();
}

/// <summary>
/// Configuration options for metrics computation
/// </summary>
public sealed record MetricsComputationOptions
{
    /// <summary>
    /// Rolling window size in days (14, 28, or 90)
    /// </summary>
    public required int WindowDays { get; init; }

    /// <summary>
    /// End date for the computation window (start date is computed as EndDate - WindowDays)
    /// </summary>
    public required DateTimeOffset EndDate { get; init; }

    /// <summary>
    /// Optional project IDs to scope the computation to. If empty, uses all accessible projects.
    /// </summary>
    public IReadOnlyList<long> ProjectIds { get; init; } = Array.Empty<long>();

    /// <summary>
    /// Whether to apply winsorization to outlier values
    /// </summary>
    public bool ApplyWinsorization { get; init; } = true;

    /// <summary>
    /// Whether to apply file exclusion rules
    /// </summary>
    public bool ApplyFileExclusions { get; init; } = true;
}

/// <summary>
/// Result of per-developer metrics computation with audit information
/// </summary>
public sealed record PerDeveloperMetricsResult
{
    public required long DeveloperId { get; init; }
    public required string DeveloperName { get; init; }
    public required string DeveloperEmail { get; init; }
    public required DateTimeOffset ComputationDate { get; init; }
    public required DateTimeOffset WindowStart { get; init; }
    public required DateTimeOffset WindowEnd { get; init; }
    public required int WindowDays { get; init; }
    
    // Computed metrics matching MetricsData structure
    public required PerDeveloperMetrics Metrics { get; init; }
    
    // Audit information
    public required MetricsAudit Audit { get; init; }
}

/// <summary>
/// Per-developer metrics matching the PRD structure
/// </summary>
public sealed record PerDeveloperMetrics
{
    // Cycle time and review metrics (medians)
    public decimal? MrCycleTimeP50H { get; init; }
    public decimal? TimeToFirstReviewP50H { get; init; }
    public decimal? TimeInReviewP50H { get; init; }
    public decimal? WipAgeP50H { get; init; }
    public decimal? WipAgeP90H { get; init; }
    public decimal? BranchTtlP50H { get; init; }
    public decimal? BranchTtlP90H { get; init; }

    // Rate and ratio metrics
    public decimal? PipelineSuccessRate { get; init; }
    public decimal? ApprovalBypassRatio { get; init; }
    public decimal? ReworkRate { get; init; }
    public decimal? FlakyJobRate { get; init; }
    public decimal? SignedCommitRatio { get; init; }
    public decimal? IssueSlaBreachRate { get; init; }
    public decimal? ReopenedIssueRate { get; init; }
    public decimal? DefectEscapeRate { get; init; }

    // Count-based metrics
    public int DeploymentFrequencyWk { get; init; }
    public int MrThroughputWk { get; init; }
    public int WipMrCount { get; init; }
    public int ReleasesCadenceWk { get; init; }
    public int RollbackIncidence { get; init; }
    public int DirectPushesDefault { get; init; }
    public int ForcePushesProtected { get; init; }

    // Duration metrics (seconds)
    public decimal? MeanTimeToGreenSec { get; init; }
    public decimal? AvgPipelineDurationSec { get; init; }
}

/// <summary>
/// Audit information for metrics computation
/// </summary>
public sealed record MetricsAudit
{
    // Data availability flags
    public bool HasMergeRequestData { get; init; }
    public bool HasPipelineData { get; init; }
    public bool HasCommitData { get; init; }
    public bool HasReviewData { get; init; }

    // Low sample size flags (n < 5 for statistical reliability)
    public bool LowMergeRequestCount { get; init; }
    public bool LowPipelineCount { get; init; }
    public bool LowCommitCount { get; init; }
    public bool LowReviewCount { get; init; }

    // Null reasons for missing metrics
    public Dictionary<string, string> NullReasons { get; init; } = new();

    // Audit counts
    public int TotalMergeRequests { get; init; }
    public int TotalPipelines { get; init; }
    public int TotalCommits { get; init; }
    public int TotalReviews { get; init; }
    public int ExcludedFiles { get; init; }
    public int WinsorizedMetrics { get; init; }

    // Quality indicators
    public string DataQuality { get; init; } = "Good"; // Excellent, Good, Fair, Poor
    public bool HasSufficientData { get; init; }
}