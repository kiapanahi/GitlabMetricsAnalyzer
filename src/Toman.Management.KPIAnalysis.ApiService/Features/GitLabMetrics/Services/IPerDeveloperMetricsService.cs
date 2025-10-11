namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

/// <summary>
/// Service for calculating per-developer metrics from live GitLab data
/// </summary>
public interface IPerDeveloperMetricsService
{
    /// <summary>
    /// Calculates MR cycle time (P50/median) for a developer across all projects
    /// </summary>
    /// <param name="userId">The GitLab user ID</param>
    /// <param name="windowDays">Number of days to look back (default: 30)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>MR cycle time analysis result</returns>
    Task<MrCycleTimeResult> CalculateMrCycleTimeAsync(
        long userId,
        int windowDays = 30,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of MR cycle time calculation
/// </summary>
public sealed class MrCycleTimeResult
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
    /// Median MR cycle time in hours (P50)
    /// </summary>
    public decimal? MrCycleTimeP50H { get; init; }

    /// <summary>
    /// 90th percentile MR cycle time in hours (P90)
    /// </summary>
    public decimal? MrCycleTimeP90H { get; init; }

    /// <summary>
    /// Total number of merged MRs analyzed
    /// </summary>
    public required int MergedMrCount { get; init; }

    /// <summary>
    /// Number of MRs excluded due to missing first_commit_at timestamp
    /// </summary>
    public required int ExcludedMrCount { get; init; }

    /// <summary>
    /// Projects included in the analysis
    /// </summary>
    public required List<ProjectMrSummary> Projects { get; init; }
}

/// <summary>
/// Summary of MRs per project
/// </summary>
public sealed class ProjectMrSummary
{
    public required long ProjectId { get; init; }
    public required string ProjectName { get; init; }
    public required int MergedMrCount { get; init; }
}
