namespace KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

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

    /// <summary>
    /// Calculates flow and throughput metrics for a developer across all projects
    /// </summary>
    /// <param name="userId">The GitLab user ID</param>
    /// <param name="windowDays">Number of days to look back (default: 30)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Flow metrics result</returns>
    Task<FlowMetricsResult> CalculateFlowMetricsAsync(
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
    /// One-line description of this metric
    /// </summary>
    public string Description => "Measures the median time from first commit to merge, indicating how quickly code flows through the development pipeline";

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

/// <summary>
/// Result of flow and throughput metrics calculation
/// </summary>
public sealed class FlowMetricsResult
{
    /// <summary>
    /// One-line description of this metric
    /// </summary>
    public string Description => "Tracks development velocity, throughput, and efficiency through the entire development lifecycle from coding to merge";

    /// <summary>
    /// Metric 1: Total merged MRs count
    /// </summary>
    public required int MergedMrsCount { get; init; }

    /// <summary>
    /// Metric 2: Total lines changed (additions + deletions) in merged MRs
    /// </summary>
    public required int LinesChanged { get; init; }

    /// <summary>
    /// Metric 3: Median coding time (first commit → MR open) in hours
    /// </summary>
    public decimal? CodingTimeMedianH { get; init; }

    /// <summary>
    /// Metric 4: Median time to first review (MR open → first non-author comment) in hours
    /// </summary>
    public decimal? TimeToFirstReviewMedianH { get; init; }

    /// <summary>
    /// Metric 5: Median review time (first review → approval) in hours
    /// </summary>
    public decimal? ReviewTimeMedianH { get; init; }

    /// <summary>
    /// Metric 6: Median merge time (approval → merged) in hours
    /// </summary>
    public decimal? MergeTimeMedianH { get; init; }

    /// <summary>
    /// Metric 7: Count of open/draft MRs at snapshot time
    /// </summary>
    public required int WipOpenMrsCount { get; init; }

    /// <summary>
    /// Metric 8: Context switching index (distinct projects touched)
    /// </summary>
    public required int ContextSwitchingIndex { get; init; }

    /// <summary>
    /// Projects included in the analysis
    /// </summary>
    public required List<ProjectMrSummary> Projects { get; init; }
}
