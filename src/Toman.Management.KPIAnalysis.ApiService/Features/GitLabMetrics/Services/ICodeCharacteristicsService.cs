namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

/// <summary>
/// Service for calculating code characteristics metrics for a developer
/// </summary>
public interface ICodeCharacteristicsService
{
    /// <summary>
    /// Calculates code characteristics metrics for a developer across all projects
    /// </summary>
    /// <param name="userId">The GitLab user ID</param>
    /// <param name="windowDays">Number of days to look back (default: 30)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Code characteristics metrics result</returns>
    Task<CodeCharacteristicsResult> CalculateCodeCharacteristicsAsync(
        long userId,
        int windowDays = 30,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of code characteristics metrics calculation
/// </summary>
public sealed class CodeCharacteristicsResult
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
    /// Metric 1: Commit Frequency - Average commits per day
    /// Direction: context-dependent
    /// </summary>
    public decimal CommitsPerDay { get; init; }

    /// <summary>
    /// Metric 1: Commit Frequency - Average commits per week
    /// Direction: context-dependent
    /// </summary>
    public decimal CommitsPerWeek { get; init; }

    /// <summary>
    /// Total number of commits in the period
    /// </summary>
    public int TotalCommits { get; init; }

    /// <summary>
    /// Number of distinct days with commits
    /// Direction: contextual
    /// </summary>
    public int CommitDaysCount { get; init; }

    /// <summary>
    /// Metric 2: Commit Size Distribution - Median lines per commit (P50)
    /// Direction: context-dependent (smaller often preferred)
    /// </summary>
    public decimal? CommitSizeMedian { get; init; }

    /// <summary>
    /// Metric 2: Commit Size Distribution - P95 lines per commit
    /// Direction: context-dependent
    /// </summary>
    public decimal? CommitSizeP95 { get; init; }

    /// <summary>
    /// Average lines per commit
    /// </summary>
    public decimal? CommitSizeAverage { get; init; }

    /// <summary>
    /// Metric 3: MR Size Distribution - Percentage breakdown by size category
    /// Direction: More small MRs = ↑ good
    /// </summary>
    public required MrSizeDistribution MrSizeDistribution { get; init; }

    /// <summary>
    /// Total number of merged MRs in the period
    /// </summary>
    public int TotalMergedMrs { get; init; }

    /// <summary>
    /// Metric 4: File Churn Analysis - Most frequently modified files
    /// Direction: context-dependent (identifies ownership and hotspots)
    /// </summary>
    public required List<FileChurnInfo> TopFilesByChurn { get; init; }

    /// <summary>
    /// Metric 5: Squash vs Merge Strategy - Percentage of MRs using squash merge
    /// Formula: (squashed_mrs / merged_mrs)
    /// Direction: context-dependent
    /// </summary>
    public decimal SquashMergeRate { get; init; }

    /// <summary>
    /// Number of MRs merged with squash
    /// </summary>
    public int SquashedMrsCount { get; init; }

    /// <summary>
    /// Metric 6: Commit Message Quality - Average commit message length in characters
    /// Direction: ↑ good (better documentation)
    /// </summary>
    public decimal AverageCommitMessageLength { get; init; }

    /// <summary>
    /// Metric 6: Commit Message Quality - Percentage of commits following conventional commit format
    /// Direction: ↑ good
    /// </summary>
    public decimal ConventionalCommitRate { get; init; }

    /// <summary>
    /// Number of commits with good/conventional messages
    /// </summary>
    public int ConventionalCommitsCount { get; init; }

    /// <summary>
    /// Metric 7: Branch Naming Patterns - Percentage of MRs with compliant branch names
    /// Direction: ↑ good
    /// </summary>
    public decimal BranchNamingComplianceRate { get; init; }

    /// <summary>
    /// Number of MRs with compliant branch names
    /// </summary>
    public int CompliantBranchesCount { get; init; }

    /// <summary>
    /// Projects included in the analysis
    /// </summary>
    public required List<ProjectCodeCharacteristicsSummary> Projects { get; init; }
}

/// <summary>
/// MR size distribution across different size categories
/// </summary>
public sealed class MrSizeDistribution
{
    /// <summary>
    /// Small MRs: &lt; 100 lines changed
    /// </summary>
    public int SmallCount { get; init; }

    /// <summary>
    /// Medium MRs: 100-500 lines changed
    /// </summary>
    public int MediumCount { get; init; }

    /// <summary>
    /// Large MRs: 500-1000 lines changed
    /// </summary>
    public int LargeCount { get; init; }

    /// <summary>
    /// Extra Large MRs: &gt; 1000 lines changed
    /// </summary>
    public int ExtraLargeCount { get; init; }

    /// <summary>
    /// Percentage of small MRs
    /// </summary>
    public decimal SmallPercentage { get; init; }

    /// <summary>
    /// Percentage of medium MRs
    /// </summary>
    public decimal MediumPercentage { get; init; }

    /// <summary>
    /// Percentage of large MRs
    /// </summary>
    public decimal LargePercentage { get; init; }

    /// <summary>
    /// Percentage of extra large MRs
    /// </summary>
    public decimal ExtraLargePercentage { get; init; }
}

/// <summary>
/// Information about a file's churn (modification frequency)
/// </summary>
public sealed class FileChurnInfo
{
    /// <summary>
    /// File path
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Number of times the file was modified
    /// </summary>
    public int ModificationCount { get; init; }

    /// <summary>
    /// Total lines added to this file
    /// </summary>
    public int TotalAdditions { get; init; }

    /// <summary>
    /// Total lines deleted from this file
    /// </summary>
    public int TotalDeletions { get; init; }
}

/// <summary>
/// Summary of code characteristics per project
/// </summary>
public sealed class ProjectCodeCharacteristicsSummary
{
    public required long ProjectId { get; init; }
    public required string ProjectName { get; init; }
    public required int CommitCount { get; init; }
    public required int MergedMrCount { get; init; }
}
