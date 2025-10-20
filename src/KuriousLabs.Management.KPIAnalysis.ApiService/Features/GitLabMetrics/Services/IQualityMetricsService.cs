namespace KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

/// <summary>
/// Service for calculating quality and reliability metrics for a developer
/// </summary>
public interface IQualityMetricsService
{
    /// <summary>
    /// Calculates quality and reliability metrics for a developer across all projects
    /// </summary>
    /// <param name="userId">The GitLab user ID</param>
    /// <param name="windowDays">Number of days to look back (default: 30)</param>
    /// <param name="revertDetectionDays">Number of days to check for reverts (default: 30)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Quality metrics result</returns>
    Task<QualityMetricsResult> CalculateQualityMetricsAsync(
        long userId,
        int windowDays = 30,
        int revertDetectionDays = 30,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of quality and reliability metrics calculation
/// </summary>
public sealed class QualityMetricsResult
{
    /// <summary>
    /// One-line description of this metric
    /// </summary>
    public string Description => "Evaluates code quality, test coverage, CI success rates, and frequency of issues requiring fixes or reverts";

    /// <summary>
    /// Metric 1: Rework Ratio - MRs with commits after review started
    /// Formula: (count(MRs with commits_after_first_review > 0)) / merged_mrs
    /// Direction: ↓ good
    /// </summary>
    public decimal ReworkRatio { get; init; }

    /// <summary>
    /// Number of MRs with rework (commits after first review)
    /// </summary>
    public int ReworkMrCount { get; init; }

    /// <summary>
    /// Metric 2: Revert Rate - Merged MRs later reverted within timeframe
    /// Formula: (count(MRs with revert_of_mr_iid within Nd)) / merged_mrs
    /// Direction: ↓ good
    /// </summary>
    public decimal RevertRate { get; init; }

    /// <summary>
    /// Number of MRs that were reverted
    /// </summary>
    public int RevertedMrCount { get; init; }

    /// <summary>
    /// Number of days used for revert detection
    /// </summary>
    public int RevertDetectionDays { get; init; }

    /// <summary>
    /// Metric 3: CI Success Rate - First-time pipeline success rate
    /// Formula: (successful_pipelines_first_run) / total_pipelines
    /// Direction: ↑ good
    /// </summary>
    public decimal? CiSuccessRate { get; init; }

    /// <summary>
    /// Number of successful pipelines on first run
    /// </summary>
    public int SuccessfulPipelinesFirstRun { get; init; }

    /// <summary>
    /// Total number of first-run pipelines
    /// </summary>
    public int TotalFirstRunPipelines { get; init; }

    /// <summary>
    /// Metric 4: Pipeline Duration P50 (median) in minutes
    /// Direction: ↓ good
    /// </summary>
    public decimal? PipelineDurationP50Min { get; init; }

    /// <summary>
    /// Metric 4: Pipeline Duration P95 in minutes
    /// Direction: ↓ good
    /// </summary>
    public decimal? PipelineDurationP95Min { get; init; }

    /// <summary>
    /// Total number of pipelines with duration data
    /// </summary>
    public int PipelinesWithDurationCount { get; init; }

    /// <summary>
    /// Metric 5: Test Coverage - Average coverage percentage from pipeline reports
    /// Direction: ↑ good
    /// </summary>
    public decimal? TestCoveragePercent { get; init; }

    /// <summary>
    /// Number of pipelines with coverage data
    /// </summary>
    public int PipelinesWithCoverageCount { get; init; }

    /// <summary>
    /// Metric 6: Hotfix Rate - MRs labeled as hotfixes vs. total MRs
    /// Formula: (count(MRs with 'hotfix' label)) / merged_mrs
    /// Direction: ↓ good
    /// </summary>
    public decimal HotfixRate { get; init; }

    /// <summary>
    /// Number of hotfix MRs
    /// </summary>
    public int HotfixMrCount { get; init; }

    /// <summary>
    /// Metric 7: Merge Conflicts Frequency - Frequency of conflict resolution needed
    /// Direction: ↓ good
    /// </summary>
    public decimal ConflictRate { get; init; }

    /// <summary>
    /// Number of MRs that had conflicts
    /// </summary>
    public int ConflictMrCount { get; init; }

    /// <summary>
    /// Total number of merged MRs analyzed
    /// </summary>
    public required int MergedMrCount { get; init; }

    /// <summary>
    /// Projects included in the analysis
    /// </summary>
    public required List<ProjectQualitySummary> Projects { get; init; }
}

/// <summary>
/// Summary of quality metrics per project
/// </summary>
public sealed class ProjectQualitySummary
{
    public required long ProjectId { get; init; }
    public required string ProjectName { get; init; }
    public required int MergedMrCount { get; init; }
    public required int PipelineCount { get; init; }
}
