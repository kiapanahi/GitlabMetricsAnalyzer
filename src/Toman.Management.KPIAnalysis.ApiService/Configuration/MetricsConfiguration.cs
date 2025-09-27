namespace Toman.Management.KPIAnalysis.ApiService.Configuration;

/// <summary>
/// Main configuration class that holds all metrics collection settings
/// </summary>
public sealed class MetricsConfiguration
{
    public const string SectionName = "Metrics";

    /// <summary>
    /// Optional project filtering and scoping configuration. If not provided, all projects accessible by the GitLab PAT will be included.
    /// </summary>
    public ProjectScopeConfiguration? ProjectScope { get; init; }

    /// <summary>
    /// Identity mapping and filtering configuration
    /// </summary>
    public required IdentityConfiguration Identity { get; init; }

    /// <summary>
    /// Various exclusion rules
    /// </summary>
    public required ExclusionConfiguration Excludes { get; init; }
}

/// <summary>
/// Configuration for project scoping and filtering. Optional - if not provided, all projects accessible by the GitLab PAT will be included.
/// </summary>
public sealed class ProjectScopeConfiguration
{
    /// <summary>
    /// List of project IDs to include (null/empty = include all accessible projects)
    /// </summary>
    public List<long> IncludeProjects { get; init; } = [];

    /// <summary>
    /// List of project IDs to exclude
    /// </summary>
    public List<long> ExcludeProjects { get; init; } = [];

    /// <summary>
    /// List of project name patterns to include (regex patterns)
    /// </summary>
    public List<string> IncludeProjectPatterns { get; init; } = [];

    /// <summary>
    /// List of project name patterns to exclude (regex patterns)
    /// </summary>
    public List<string> ExcludeProjectPatterns { get; init; } = [];
}

/// <summary>
/// Configuration for developer identity mapping and bot detection
/// </summary>
public sealed class IdentityConfiguration
{
    /// <summary>
    /// Regex patterns to identify bot accounts
    /// </summary>
    public List<string> BotRegexPatterns { get; init; } = [];
}

/// <summary>
/// Configuration for various exclusion rules
/// </summary>
public sealed class ExclusionConfiguration
{
    /// <summary>
    /// Commit patterns to exclude (e.g., merge commits, automated commits)
    /// </summary>
    public List<string> CommitPatterns { get; init; } = [];

    /// <summary>
    /// Branch patterns to exclude from metrics
    /// </summary>
    public List<string> BranchPatterns { get; init; } = [];

    /// <summary>
    /// File patterns to exclude from line count metrics
    /// </summary>
    public List<string> FilePatterns { get; init; } = [];
}