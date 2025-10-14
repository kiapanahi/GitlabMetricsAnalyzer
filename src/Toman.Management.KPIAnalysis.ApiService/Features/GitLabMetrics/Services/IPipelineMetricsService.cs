namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

/// <summary>
/// Service for calculating CI/CD pipeline metrics
/// </summary>
public interface IPipelineMetricsService
{
    /// <summary>
    /// Calculates comprehensive pipeline metrics for a project
    /// </summary>
    /// <param name="projectId">The GitLab project ID</param>
    /// <param name="windowDays">Number of days to look back (default: 30)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Pipeline metrics result</returns>
    Task<PipelineMetricsResult> CalculatePipelineMetricsAsync(
        long projectId,
        int windowDays = 30,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of pipeline metrics calculation
/// </summary>
public sealed class PipelineMetricsResult
{
    /// <summary>
    /// The project ID
    /// </summary>
    public required long ProjectId { get; init; }

    /// <summary>
    /// The project name
    /// </summary>
    public required string ProjectName { get; init; }

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
    /// Metric 1: Failed Job Rate - Most frequently failing pipeline jobs
    /// Direction: ↓ good (lower is better)
    /// </summary>
    public required List<FailedJobSummary> FailedJobs { get; init; }

    /// <summary>
    /// Metric 2: Pipeline Retry Rate - Pipelines requiring manual retry
    /// Direction: ↓ good (lower is better)
    /// Unit: percentage
    /// </summary>
    public decimal? PipelineRetryRate { get; init; }

    /// <summary>
    /// Number of pipelines that were retried
    /// </summary>
    public int RetriedPipelineCount { get; init; }

    /// <summary>
    /// Total number of pipelines analyzed
    /// </summary>
    public int TotalPipelineCount { get; init; }

    /// <summary>
    /// Metric 3: Pipeline Wait Time (P50/median) - Queue time before pipeline starts
    /// Direction: ↓ good (lower is better)
    /// Unit: minutes
    /// </summary>
    public decimal? PipelineWaitTimeP50Min { get; init; }

    /// <summary>
    /// Metric 3: Pipeline Wait Time (P95) - Queue time before pipeline starts
    /// Direction: ↓ good (lower is better)
    /// Unit: minutes
    /// </summary>
    public decimal? PipelineWaitTimeP95Min { get; init; }

    /// <summary>
    /// Number of pipelines with wait time data
    /// </summary>
    public int PipelinesWithWaitTimeCount { get; init; }

    /// <summary>
    /// Metric 4: Deployment Frequency - Merges to main/production branches
    /// Direction: ↑ good (higher is better) - DORA metric
    /// Unit: count per period
    /// </summary>
    public int DeploymentFrequency { get; init; }

    /// <summary>
    /// Metric 5: Job Duration Trends - Track duration changes over time
    /// Direction: ↓ good (lower is better)
    /// </summary>
    public required List<JobDurationTrend> JobDurationTrends { get; init; }

    /// <summary>
    /// Metric 6: Pipeline Success Rate by Branch Type
    /// Direction: ↑ good (higher is better)
    /// </summary>
    public required BranchTypeMetrics BranchTypeMetrics { get; init; }

    /// <summary>
    /// Metric 7: Coverage Trend - Test coverage change over time
    /// Direction: ↑ good (higher is better)
    /// Unit: percentage with trend
    /// </summary>
    public decimal? AverageCoveragePercent { get; init; }

    /// <summary>
    /// Coverage trend indicator (improving, stable, degrading)
    /// </summary>
    public string? CoverageTrend { get; init; }

    /// <summary>
    /// Number of pipelines with coverage data
    /// </summary>
    public int PipelinesWithCoverageCount { get; init; }
}

/// <summary>
/// Summary of a frequently failing job
/// </summary>
public sealed class FailedJobSummary
{
    public required string JobName { get; init; }
    public required int FailureCount { get; init; }
    public required int TotalRuns { get; init; }
    public required decimal FailureRate { get; init; }
}

/// <summary>
/// Job duration trend over time
/// </summary>
public sealed class JobDurationTrend
{
    public required string JobName { get; init; }
    public required decimal AverageDurationMin { get; init; }
    public required decimal DurationP50Min { get; init; }
    public required decimal DurationP95Min { get; init; }
    public required string Trend { get; init; } // "improving", "stable", "degrading"
    public required int RunCount { get; init; }
}

/// <summary>
/// Pipeline success metrics by branch type
/// </summary>
public sealed class BranchTypeMetrics
{
    /// <summary>
    /// Success rate on main/master/production branches
    /// </summary>
    public decimal? MainBranchSuccessRate { get; init; }

    /// <summary>
    /// Number of successful pipelines on main branches
    /// </summary>
    public int MainBranchSuccessCount { get; init; }

    /// <summary>
    /// Total number of pipelines on main branches
    /// </summary>
    public int MainBranchTotalCount { get; init; }

    /// <summary>
    /// Success rate on feature branches
    /// </summary>
    public decimal? FeatureBranchSuccessRate { get; init; }

    /// <summary>
    /// Number of successful pipelines on feature branches
    /// </summary>
    public int FeatureBranchSuccessCount { get; init; }

    /// <summary>
    /// Total number of pipelines on feature branches
    /// </summary>
    public int FeatureBranchTotalCount { get; init; }
}
