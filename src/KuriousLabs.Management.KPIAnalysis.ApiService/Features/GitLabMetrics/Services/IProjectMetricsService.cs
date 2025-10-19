namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

/// <summary>
/// Service for calculating project-level aggregation metrics
/// </summary>
public interface IProjectMetricsService
{
    /// <summary>
    /// Calculates aggregated metrics for a project
    /// </summary>
    /// <param name="projectId">The project ID</param>
    /// <param name="windowDays">Number of days to look back (default: 30)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Project metrics result</returns>
    Task<ProjectMetricsResult> CalculateProjectMetricsAsync(
        long projectId,
        int windowDays = 30,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of project metrics calculation
/// </summary>
public sealed class ProjectMetricsResult
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
    /// Project activity score: Total commits
    /// </summary>
    public required int TotalCommits { get; init; }

    /// <summary>
    /// Project activity score: Total merged MRs
    /// </summary>
    public required int TotalMergedMrs { get; init; }

    /// <summary>
    /// Project activity score: Total lines changed
    /// </summary>
    public required int TotalLinesChanged { get; init; }

    /// <summary>
    /// Number of unique contributors to the project
    /// </summary>
    public required int UniqueContributors { get; init; }

    /// <summary>
    /// Cross-project contributors: Number of contributors also working on other projects
    /// </summary>
    public required int CrossProjectContributors { get; init; }

    /// <summary>
    /// Branch lifecycle: Number of long-lived branches (> 30 days)
    /// </summary>
    public required int LongLivedBranchCount { get; init; }

    /// <summary>
    /// Branch lifecycle: Average age of long-lived branches in days
    /// </summary>
    public decimal? AvgLongLivedBranchAgeDays { get; init; }

    /// <summary>
    /// Branch lifecycle: Details of long-lived branches
    /// </summary>
    public required List<LongLivedBranchInfo> LongLivedBranches { get; init; }

    /// <summary>
    /// Label usage distribution
    /// </summary>
    public required Dictionary<string, int> LabelUsageDistribution { get; init; }

    /// <summary>
    /// Milestone completion rate: Percentage of milestones completed on time
    /// </summary>
    public decimal? MilestoneCompletionRate { get; init; }

    /// <summary>
    /// Milestone completion: Number of completed milestones
    /// </summary>
    public required int CompletedMilestones { get; init; }

    /// <summary>
    /// Milestone completion: Number of milestones completed on time
    /// </summary>
    public required int OnTimeMilestones { get; init; }

    /// <summary>
    /// Milestone completion: Total milestones tracked
    /// </summary>
    public required int TotalMilestones { get; init; }

    /// <summary>
    /// Review coverage: Percentage of MRs with at least N reviewers
    /// </summary>
    public decimal? ReviewCoveragePercentage { get; init; }

    /// <summary>
    /// Review coverage: Minimum reviewers required (configurable)
    /// </summary>
    public required int MinReviewersRequired { get; init; }

    /// <summary>
    /// Review coverage: Count of MRs with sufficient reviewers
    /// </summary>
    public required int MrsWithSufficientReviewers { get; init; }
}

/// <summary>
/// Information about a long-lived branch
/// </summary>
public sealed class LongLivedBranchInfo
{
    /// <summary>
    /// Branch name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Age in days
    /// </summary>
    public required int AgeDays { get; init; }

    /// <summary>
    /// Last commit date
    /// </summary>
    public required DateTime LastCommitDate { get; init; }

    /// <summary>
    /// Whether the branch is merged
    /// </summary>
    public required bool IsMerged { get; init; }
}
