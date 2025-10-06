namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

/// <summary>
/// Service for calculating per-developer metrics from live GitLab data
/// </summary>
public interface IPerDeveloperMetricsService
{
    /// <summary>
    /// Calculates pipeline success rate for a specific developer
    /// </summary>
    /// <param name="userId">The GitLab user ID</param>
    /// <param name="lookbackDays">Number of days to look back (default: 30)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Pipeline success rate metric result</returns>
    Task<PipelineSuccessRateResult> GetPipelineSuccessRateAsync(
        long userId,
        int lookbackDays = 30,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of pipeline success rate calculation
/// </summary>
public sealed class PipelineSuccessRateResult
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
    /// The email address
    /// </summary>
    public required string Email { get; init; }

    /// <summary>
    /// Number of days analyzed
    /// </summary>
    public required int LookbackDays { get; init; }

    /// <summary>
    /// Start date of the analysis period (UTC)
    /// </summary>
    public required DateTime AnalysisStartDate { get; init; }

    /// <summary>
    /// End date of the analysis period (UTC)
    /// </summary>
    public required DateTime AnalysisEndDate { get; init; }

    /// <summary>
    /// Total number of pipelines triggered by the developer
    /// </summary>
    public required int TotalPipelines { get; init; }

    /// <summary>
    /// Number of successful pipelines
    /// </summary>
    public required int SuccessfulPipelines { get; init; }

    /// <summary>
    /// Number of failed pipelines
    /// </summary>
    public required int FailedPipelines { get; init; }

    /// <summary>
    /// Number of pipelines with other statuses (running, pending, canceled, etc.)
    /// </summary>
    public required int OtherStatusPipelines { get; init; }

    /// <summary>
    /// Pipeline success rate as a ratio (0.0-1.0)
    /// </summary>
    public decimal? PipelineSuccessRate { get; init; }

    /// <summary>
    /// Projects included in the analysis
    /// </summary>
    public required List<ProjectPipelineSummary> Projects { get; init; }
}

/// <summary>
/// Summary of pipelines per project
/// </summary>
public sealed class ProjectPipelineSummary
{
    public required long ProjectId { get; init; }
    public required string ProjectName { get; init; }
    public required int TotalPipelines { get; init; }
    public required int SuccessfulPipelines { get; init; }
    public required int FailedPipelines { get; init; }
}
