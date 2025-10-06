namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

/// <summary>
/// Service for computing per-developer metrics from live GitLab data
/// </summary>
public interface IPerDeveloperMetricsService
{
    /// <summary>
    /// Calculates MR throughput for a specific developer from live GitLab data
    /// </summary>
    /// <param name="developerId">The GitLab user ID</param>
    /// <param name="windowDays">Number of days to analyze (default: 7)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>MR throughput analysis result</returns>
    Task<MrThroughputResult> CalculateMrThroughputAsync(
        long developerId,
        int windowDays = 7,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of MR throughput calculation
/// </summary>
public sealed class MrThroughputResult
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
    /// Number of days in the analysis window
    /// </summary>
    public required int WindowDays { get; init; }

    /// <summary>
    /// Start date of the analysis period (UTC)
    /// </summary>
    public required DateTime AnalysisStartDate { get; init; }

    /// <summary>
    /// End date of the analysis period (UTC)
    /// </summary>
    public required DateTime AnalysisEndDate { get; init; }

    /// <summary>
    /// Total number of merge requests merged in the window
    /// </summary>
    public required int TotalMergedMrs { get; init; }

    /// <summary>
    /// MR throughput as MRs merged per week
    /// </summary>
    public required int MrThroughputWk { get; init; }

    /// <summary>
    /// Projects included in the analysis
    /// </summary>
    public required List<ProjectMrSummary> Projects { get; init; }
}

/// <summary>
/// Summary of merge requests per project
/// </summary>
public sealed class ProjectMrSummary
{
    public required long ProjectId { get; init; }
    public required string ProjectName { get; init; }
    public required int MergedMrCount { get; init; }
}
