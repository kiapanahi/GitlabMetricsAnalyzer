namespace KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

/// <summary>
/// Service for calculating advanced metrics for deeper insights into team health, work patterns, and code ownership
/// </summary>
public interface IAdvancedMetricsService
{
    /// <summary>
    /// Calculates advanced metrics for a developer across all projects
    /// </summary>
    /// <param name="userId">The GitLab user ID</param>
    /// <param name="windowDays">Number of days to look back (default: 30)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Advanced metrics result</returns>
    Task<AdvancedMetricsResult> CalculateAdvancedMetricsAsync(
        long userId,
        int windowDays = 30,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of advanced metrics calculation
/// </summary>
public sealed class AdvancedMetricsResult
{
    /// <summary>
    /// One-line description of this metric
    /// </summary>
    public string Description => "Provides deeper insights into code ownership risk, work patterns, review responsiveness, and cross-team collaboration";

    /// <summary>
    /// Metric 1: Bus Factor - Code ownership concentration (Gini coefficient 0-1)
    /// Direction: ↓ good (lower = more distributed ownership, less risk)
    /// 0 = perfectly distributed, 1 = single person owns everything
    /// </summary>
    public decimal BusFactor { get; init; }

    /// <summary>
    /// Number of developers who contributed to the codebase
    /// </summary>
    public int ContributingDevelopersCount { get; init; }

    /// <summary>
    /// Percentage of file changes made by top 3 developers
    /// </summary>
    public decimal Top3DevelopersFileChangePercentage { get; init; }

    /// <summary>
    /// Metric 2: Response Time Distribution - When developers respond to reviews (hour of day)
    /// Key: Hour of day (0-23), Value: Count of responses
    /// </summary>
    public required Dictionary<int, int> ResponseTimeDistribution { get; init; }

    /// <summary>
    /// Peak response hour (hour with most review responses)
    /// </summary>
    public int? PeakResponseHour { get; init; }

    /// <summary>
    /// Total review responses analyzed for distribution
    /// </summary>
    public int TotalReviewResponses { get; init; }

    /// <summary>
    /// Metric 3: Batch Size - Commits per MR (P50 median)
    /// Direction: context-dependent
    /// </summary>
    public decimal? BatchSizeP50 { get; init; }

    /// <summary>
    /// Metric 3: Batch Size - Commits per MR (P95)
    /// Direction: context-dependent
    /// </summary>
    public decimal? BatchSizeP95 { get; init; }

    /// <summary>
    /// Total MRs analyzed for batch size
    /// </summary>
    public int BatchSizeMrCount { get; init; }

    /// <summary>
    /// Metric 4: Draft Duration - Median time MRs spend in draft/WIP state (hours)
    /// Direction: context-dependent
    /// </summary>
    public decimal? DraftDurationMedianH { get; init; }

    /// <summary>
    /// Number of MRs that had draft state
    /// </summary>
    public int DraftMrCount { get; init; }

    /// <summary>
    /// Metric 5: Iteration Count - Number of review cycles per MR (median)
    /// Direction: ↓ good (fewer iterations = clearer requirements)
    /// </summary>
    public decimal? IterationCountMedian { get; init; }

    /// <summary>
    /// Number of MRs with iterations analyzed
    /// </summary>
    public int IterationMrCount { get; init; }

    /// <summary>
    /// Metric 6: Idle Time in Review - Time MR waits with no activity after review comments (median hours)
    /// Direction: ↓ good
    /// </summary>
    public decimal? IdleTimeInReviewMedianH { get; init; }

    /// <summary>
    /// Number of MRs with idle time analyzed
    /// </summary>
    public int IdleTimeMrCount { get; init; }

    /// <summary>
    /// Metric 7: Cross-Team Collaboration Index - Percentage of MRs with reviewers from other teams
    /// Direction: ↑ good (knowledge sharing)
    /// Note: Requires team mapping configuration
    /// </summary>
    public decimal? CrossTeamCollaborationPercentage { get; init; }

    /// <summary>
    /// Number of MRs with cross-team reviewers
    /// </summary>
    public int CrossTeamMrCount { get; init; }

    /// <summary>
    /// Total MRs analyzed for cross-team collaboration
    /// </summary>
    public int TotalMrsForCrossTeam { get; init; }

    /// <summary>
    /// Whether team mapping configuration is available
    /// </summary>
    public bool TeamMappingAvailable { get; init; }

    /// <summary>
    /// Projects included in the analysis
    /// </summary>
    public required List<ProjectAdvancedMetricsSummary> Projects { get; init; }
}

/// <summary>
/// Summary of advanced metrics per project
/// </summary>
public sealed class ProjectAdvancedMetricsSummary
{
    public required long ProjectId { get; init; }
    public required string ProjectName { get; init; }
    public required int MrCount { get; init; }
    public required int CommitCount { get; init; }
    public required int FileChangeCount { get; init; }
}
