namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

/// <summary>
/// Service for calculating per-developer metrics from live GitLab data
/// </summary>
public interface IPerDeveloperMetricsService
{
    /// <summary>
    /// Calculates deployment frequency for a specific developer
    /// </summary>
    /// <param name="userId">The GitLab user ID</param>
    /// <param name="windowDays">Number of days to look back (default: 30)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Deployment frequency analysis</returns>
    Task<DeploymentFrequencyAnalysis> CalculateDeploymentFrequencyAsync(
        long userId,
        int windowDays = 30,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of deployment frequency analysis
/// </summary>
public sealed class DeploymentFrequencyAnalysis
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
    public required DateTime AnalysisStartDate { get; init; }

    /// <summary>
    /// End date of the analysis period (UTC)
    /// </summary>
    public required DateTime AnalysisEndDate { get; init; }

    /// <summary>
    /// Total number of successful production deployments found
    /// </summary>
    public required int TotalDeployments { get; init; }

    /// <summary>
    /// Deployment frequency (deployments per week)
    /// </summary>
    public required int DeploymentFrequencyWk { get; init; }

    /// <summary>
    /// Projects included in the analysis with deployment counts
    /// </summary>
    public required List<ProjectDeploymentSummary> Projects { get; init; }
}

/// <summary>
/// Summary of deployments per project
/// </summary>
public sealed class ProjectDeploymentSummary
{
    public required long ProjectId { get; init; }
    public required string ProjectName { get; init; }
    public required int DeploymentCount { get; init; }
}
