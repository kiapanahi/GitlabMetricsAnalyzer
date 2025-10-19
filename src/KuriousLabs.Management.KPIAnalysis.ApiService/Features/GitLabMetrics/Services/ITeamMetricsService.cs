namespace KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

/// <summary>
/// Service for calculating team-level aggregation metrics
/// </summary>
public interface ITeamMetricsService
{
    /// <summary>
    /// Calculates aggregated metrics for a team
    /// </summary>
    /// <param name="teamId">The team identifier</param>
    /// <param name="windowDays">Number of days to look back (default: 30)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Team metrics result</returns>
    Task<TeamMetricsResult> CalculateTeamMetricsAsync(
        string teamId,
        int windowDays = 30,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of team metrics calculation
/// </summary>
public sealed class TeamMetricsResult
{
    /// <summary>
    /// The team identifier
    /// </summary>
    public required string TeamId { get; init; }

    /// <summary>
    /// The team name
    /// </summary>
    public required string TeamName { get; init; }

    /// <summary>
    /// Number of team members
    /// </summary>
    public required int MemberCount { get; init; }

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
    /// Team velocity: Total merged MRs by team
    /// </summary>
    public required int TotalMergedMrs { get; init; }

    /// <summary>
    /// Team velocity: Total commits by team
    /// </summary>
    public required int TotalCommits { get; init; }

    /// <summary>
    /// Team velocity: Total lines changed by team
    /// </summary>
    public required int TotalLinesChanged { get; init; }

    /// <summary>
    /// Team velocity: Average MR cycle time in hours (P50)
    /// </summary>
    public decimal? AvgMrCycleTimeP50H { get; init; }

    /// <summary>
    /// Cross-project contributors: Number of team members contributing to multiple projects
    /// </summary>
    public required int CrossProjectContributors { get; init; }

    /// <summary>
    /// Cross-project contributors: Total unique projects touched by team
    /// </summary>
    public required int TotalProjectsTouched { get; init; }

    /// <summary>
    /// Team review coverage: Percentage of MRs with at least N reviewers
    /// </summary>
    public decimal? TeamReviewCoveragePercentage { get; init; }

    /// <summary>
    /// Team review coverage: Minimum number of reviewers required (configurable)
    /// </summary>
    public required int MinReviewersRequired { get; init; }

    /// <summary>
    /// Team review coverage: Count of MRs meeting the review coverage threshold
    /// </summary>
    public required int MrsWithSufficientReviewers { get; init; }

    /// <summary>
    /// Project activity scores for projects the team contributes to
    /// </summary>
    public required List<ProjectActivityScore> ProjectActivities { get; init; }
}

/// <summary>
/// Project activity score
/// </summary>
public sealed class ProjectActivityScore
{
    /// <summary>
    /// Project ID
    /// </summary>
    public required long ProjectId { get; init; }

    /// <summary>
    /// Project name
    /// </summary>
    public required string ProjectName { get; init; }

    /// <summary>
    /// Number of commits in the project
    /// </summary>
    public required int CommitCount { get; init; }

    /// <summary>
    /// Number of merged MRs in the project
    /// </summary>
    public required int MergedMrCount { get; init; }

    /// <summary>
    /// Number of team members contributing to this project
    /// </summary>
    public required int ContributorCount { get; init; }
}
