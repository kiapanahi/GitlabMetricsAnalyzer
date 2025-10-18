namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

/// <summary>
/// Service for aggregating all user metrics concurrently
/// </summary>
public interface IUserMetricsAggregationService
{
    /// <summary>
    /// Gathers all available user metrics concurrently
    /// </summary>
    /// <param name="userId">The GitLab user ID</param>
    /// <param name="windowDays">Number of days to look back (default: 30)</param>
    /// <param name="revertDetectionDays">Number of days to check for reverts in quality metrics (default: 30)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Aggregated user metrics result</returns>
    Task<AggregatedUserMetricsResult> GetAllUserMetricsAsync(
        long userId,
        int windowDays = 30,
        int revertDetectionDays = 30,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result containing all user metrics gathered concurrently
/// </summary>
public sealed class AggregatedUserMetricsResult
{
    /// <summary>
    /// The GitLab user ID
    /// </summary>
    public required long UserId { get; init; }

    /// <summary>
    /// The username
    /// </summary>
    public required string Username { get; init; }

    /// <summary>
    /// Number of days analyzed
    /// </summary>
    public required int WindowDays { get; init; }

    /// <summary>
    /// Start date of the analysis period (UTC)
    /// </summary>
    public required DateTime WindowStart { get; init; }

    /// <summary>
    /// End date of the analysis period (UTC)
    /// </summary>
    public required DateTime WindowEnd { get; init; }

    /// <summary>
    /// Commit time distribution analysis
    /// </summary>
    public CommitTimeDistributionAnalysis? CommitTimeAnalysis { get; init; }

    /// <summary>
    /// MR cycle time metrics
    /// </summary>
    public MrCycleTimeResult? MrCycleTime { get; init; }

    /// <summary>
    /// Flow and throughput metrics
    /// </summary>
    public FlowMetricsResult? FlowMetrics { get; init; }

    /// <summary>
    /// Collaboration and review metrics
    /// </summary>
    public CollaborationMetricsResult? CollaborationMetrics { get; init; }

    /// <summary>
    /// Quality and reliability metrics
    /// </summary>
    public QualityMetricsResult? QualityMetrics { get; init; }

    /// <summary>
    /// Code characteristics metrics
    /// </summary>
    public CodeCharacteristicsResult? CodeCharacteristics { get; init; }

    /// <summary>
    /// Advanced metrics
    /// </summary>
    public AdvancedMetricsResult? AdvancedMetrics { get; init; }

    /// <summary>
    /// Errors encountered during metric calculation (if any)
    /// </summary>
    public Dictionary<string, string>? Errors { get; init; }
}
