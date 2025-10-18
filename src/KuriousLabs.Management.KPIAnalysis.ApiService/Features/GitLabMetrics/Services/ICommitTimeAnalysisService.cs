namespace KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

/// <summary>
/// Service for analyzing commit time patterns and distributions
/// </summary>
public interface ICommitTimeAnalysisService
{
    /// <summary>
    /// Analyzes the distribution of commits across 24 hours for a specific user
    /// </summary>
    /// <param name="userId">The GitLab user ID</param>
    /// <param name="lookbackDays">Number of days to look back (default: 30)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Commit time distribution analysis</returns>
    Task<CommitTimeDistributionAnalysis> AnalyzeCommitTimeDistributionAsync(
        long userId,
        int lookbackDays = 30,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of commit time distribution analysis
/// </summary>
public sealed class CommitTimeDistributionAnalysis
{
    /// <summary>
    /// One-line description of this metric
    /// </summary>
    public string Description => "Analyzes when commits are made throughout the day to identify work patterns and peak productivity hours";

    /// <summary>
    /// The GitLab user ID
    /// </summary>
    public required long UserId { get; init; }

    /// <summary>
    /// The username
    /// </summary>
    public required string Username { get; init; }

    /// <summary>
    /// The email address used for the analysis
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
    /// Total number of commits found
    /// </summary>
    public required int TotalCommits { get; init; }

    /// <summary>
    /// Distribution of commits by hour of day (0-23)
    /// Key: Hour (0-23), Value: Number of commits in that hour
    /// </summary>
    public required Dictionary<int, int> HourlyDistribution { get; init; }

    /// <summary>
    /// Distribution of commits by time period
    /// </summary>
    public required TimePeriodDistribution TimePeriods { get; init; }

    /// <summary>
    /// Projects included in the analysis
    /// </summary>
    public required List<ProjectCommitSummary> Projects { get; init; }

    /// <summary>
    /// Peak activity hour (0-23)
    /// </summary>
    public int PeakActivityHour { get; init; }

    /// <summary>
    /// Percentage of commits during peak hour
    /// </summary>
    public decimal PeakActivityPercentage { get; init; }
}

/// <summary>
/// Distribution across time periods
/// </summary>
public sealed class TimePeriodDistribution
{
    /// <summary>
    /// Night commits (00:00-05:59)
    /// </summary>
    public required int Night { get; init; }

    /// <summary>
    /// Morning commits (06:00-11:59)
    /// </summary>
    public required int Morning { get; init; }

    /// <summary>
    /// Afternoon commits (12:00-17:59)
    /// </summary>
    public required int Afternoon { get; init; }

    /// <summary>
    /// Evening commits (18:00-23:59)
    /// </summary>
    public required int Evening { get; init; }

    /// <summary>
    /// Percentage distribution
    /// </summary>
    public required TimePeriodPercentages Percentages { get; init; }
}

/// <summary>
/// Percentage distribution across time periods
/// </summary>
public sealed class TimePeriodPercentages
{
    public required decimal Night { get; init; }
    public required decimal Morning { get; init; }
    public required decimal Afternoon { get; init; }
    public required decimal Evening { get; init; }
}

/// <summary>
/// Summary of commits per project
/// </summary>
public sealed class ProjectCommitSummary
{
    public required long ProjectId { get; init; }
    public required string ProjectName { get; init; }
    public required int CommitCount { get; init; }
}
