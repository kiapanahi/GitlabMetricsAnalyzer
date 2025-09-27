using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Facts;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

/// <summary>
/// Service for collecting and storing user metrics snapshots over time
/// </summary>
public interface IUserMetricsCollectionService
{
    /// <summary>
    /// Collect and store user metrics for a specific user over a given period
    /// </summary>
    /// <param name="userId">GitLab user ID</param>
    /// <param name="fromDate">Start date for metrics calculation (optional, defaults to 3 months ago)</param>
    /// <param name="toDate">End date for metrics calculation (optional, defaults to now)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The stored user metrics snapshot</returns>
    Task<FactUserMetrics> CollectAndStoreUserMetricsAsync(long userId, DateTimeOffset? fromDate = null, DateTimeOffset? toDate = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get historical user metrics snapshots for comparison
    /// </summary>
    /// <param name="userId">GitLab user ID</param>
    /// <param name="limit">Maximum number of snapshots to return (optional, defaults to 10)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of user metrics snapshots ordered by collection date (newest first)</returns>
    Task<List<FactUserMetrics>> GetUserMetricsHistoryAsync(long userId, int limit = 10, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get user metrics snapshots within a specific time range
    /// </summary>
    /// <param name="userId">GitLab user ID</param>
    /// <param name="fromDate">Start date for collection period</param>
    /// <param name="toDate">End date for collection period</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of user metrics snapshots within the specified range</returns>
    Task<List<FactUserMetrics>> GetUserMetricsInRangeAsync(long userId, DateTimeOffset fromDate, DateTimeOffset toDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Compare user metrics between two time periods
    /// </summary>
    /// <param name="userId">GitLab user ID</param>
    /// <param name="baselineCollectedAt">Collection date for baseline metrics</param>
    /// <param name="currentCollectedAt">Collection date for current metrics</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Comparison between baseline and current metrics</returns>
    Task<UserMetricsComparison?> CompareUserMetricsAsync(long userId, DateTimeOffset baselineCollectedAt, DateTimeOffset currentCollectedAt, CancellationToken cancellationToken = default);
}

/// <summary>
/// Response model for user metrics collection
/// </summary>
public sealed record UserMetricsCollectionResponse
{
    public required long UserId { get; init; }
    public required string Username { get; init; }
    public required DateTimeOffset CollectedAt { get; init; }
    public required DateTimeOffset FromDate { get; init; }
    public required DateTimeOffset ToDate { get; init; }
    public required int PeriodDays { get; init; }
    public required string Message { get; init; }
    public required FactUserMetrics Metrics { get; init; }
}

/// <summary>
/// Response model for user metrics history
/// </summary>
public sealed record UserMetricsHistoryResponse
{
    public required long UserId { get; init; }
    public required string Username { get; init; }
    public required int TotalSnapshots { get; init; }
    public required DateTimeOffset? EarliestSnapshot { get; init; }
    public required DateTimeOffset? LatestSnapshot { get; init; }
    public required List<FactUserMetrics> Snapshots { get; init; }
}

/// <summary>
/// Comparison between two user metrics snapshots
/// </summary>
public sealed record UserMetricsComparison
{
    public required long UserId { get; init; }
    public required string Username { get; init; }
    public required FactUserMetrics BaselineMetrics { get; init; }
    public required FactUserMetrics CurrentMetrics { get; init; }
    public required UserMetricsChanges Changes { get; init; }
}

/// <summary>
/// Changes between baseline and current metrics
/// </summary>
public sealed record UserMetricsChanges
{
    // Code Contribution Changes
    public int CommitsChange { get; init; }
    public double CommitsChangePercent { get; init; }
    public int LinesChangedChange { get; init; }
    public double LinesChangedChangePercent { get; init; }
    public double CommitsPerDayChange { get; init; }
    public double CommitsPerDayChangePercent { get; init; }

    // Code Review Changes
    public int MergeRequestsCreatedChange { get; init; }
    public double MergeRequestsCreatedChangePercent { get; init; }
    public double CycleTimeChange { get; init; }
    public double CycleTimeChangePercent { get; init; }
    public double MergeRateChange { get; init; }
    public double MergeRateChangePercent { get; init; }

    // Quality Changes
    public double PipelineSuccessRateChange { get; init; }
    public double PipelineSuccessRateChangePercent { get; init; }
    public int PipelinesTriggeredChange { get; init; }
    public double PipelinesTriggeredChangePercent { get; init; }

    // Productivity Changes
    public double ProductivityScoreChange { get; init; }
    public double ProductivityScoreChangePercent { get; init; }
    public string? ProductivityLevelChange { get; init; } // e.g., "Low → High", "High → Medium"

    // Overall assessment
    public string OverallTrend { get; init; } = string.Empty; // Improving, Declining, Stable
    public List<string> KeyImprovements { get; init; } = [];
    public List<string> AreasOfConcern { get; init; } = [];
}
