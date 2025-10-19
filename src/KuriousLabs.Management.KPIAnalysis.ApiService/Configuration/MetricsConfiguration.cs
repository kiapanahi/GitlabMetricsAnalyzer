namespace KuriousLabs.Management.KPIAnalysis.ApiService.Configuration;

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
    public required IdentityConfiguration Identity { get; init; } = new IdentityConfiguration();

    /// <summary>
    /// Various exclusion rules
    /// </summary>
    public required ExclusionConfiguration Excludes { get; init; } = new ExclusionConfiguration();

    /// <summary>
    /// When true, register legacy/experimental metrics services. Defaults to false to allow safe incremental removal.
    /// </summary>
    public bool EnableLegacyMetrics { get; init; } = false;

    /// <summary>
    /// Configuration for code characteristics metrics
    /// </summary>
    public CodeCharacteristicsConfiguration CodeCharacteristics { get; init; } = new CodeCharacteristicsConfiguration();

    /// <summary>
    /// Team mapping configuration for team-level metrics
    /// </summary>
    public TeamMappingConfiguration? TeamMapping { get; init; }
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

/// <summary>
/// Configuration for code characteristics metrics
/// </summary>
public sealed class CodeCharacteristicsConfiguration
{
    /// <summary>
    /// Threshold for small MRs in lines changed (default: 100)
    /// </summary>
    public int SmallMrThreshold { get; init; } = 100;

    /// <summary>
    /// Threshold for medium MRs in lines changed (default: 500)
    /// </summary>
    public int MediumMrThreshold { get; init; } = 500;

    /// <summary>
    /// Threshold for large MRs in lines changed (default: 1000)
    /// </summary>
    public int LargeMrThreshold { get; init; } = 1000;

    /// <summary>
    /// Number of top files to include in churn analysis (default: 10)
    /// </summary>
    public int TopFilesChurnCount { get; init; } = 10;

    /// <summary>
    /// Minimum commit message length to be considered good quality (default: 15)
    /// </summary>
    public int MinCommitMessageLength { get; init; } = 15;

    /// <summary>
    /// Regex patterns for conventional commit format validation
    /// Default: (feat|fix|refactor|docs|test|chore|style|perf|ci|build|revert)(:|\\()
    /// </summary>
    public List<string> ConventionalCommitPatterns { get; init; } =
    [
        @"^(feat|fix|refactor|docs|test|chore|style|perf|ci|build|revert)(\([\w\-]+\))?:\s+.+",
    ];

    /// <summary>
    /// Regex patterns for compliant branch names
    /// Default patterns for feature/, bugfix/, hotfix/, etc.
    /// </summary>
    public List<string> BranchNamingPatterns { get; init; } =
    [
        @"^(feature|feat)\/[\w\-]+",
        @"^(bugfix|fix)\/[\w\-]+",
        @"^(hotfix|hf)\/[\w\-]+",
        @"^(release|rel)\/[\w\-]+",
        @"^(chore|task)\/[\w\-]+",
        @"^(refactor|refac)\/[\w\-]+",
    ];

    /// <summary>
    /// Commit message patterns to exclude from quality checks (e.g., "wip", "fix", etc.)
    /// </summary>
    public List<string> ExcludedCommitMessagePatterns { get; init; } =
    [
        @"^wip$",
        @"^fix$",
        @"^typo$",
        @"^merge\s+",
        @"^revert\s+",
    ];
}

/// <summary>
/// Configuration for team mapping
/// </summary>
public sealed class TeamMappingConfiguration
{
    /// <summary>
    /// List of teams with their member user IDs
    /// </summary>
    public List<TeamDefinition> Teams { get; init; } = [];
}

/// <summary>
/// Definition of a team
/// </summary>
public sealed class TeamDefinition
{
    /// <summary>
    /// Team identifier
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Team display name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// List of GitLab user IDs that belong to this team
    /// </summary>
    public List<long> Members { get; init; } = [];
}
