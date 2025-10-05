namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Configuration;

/// <summary>
/// Configuration for GitLab data collection behavior
/// </summary>
public sealed class CollectionConfiguration
{
    public const string SectionName = "GitLab:Collection";

    /// <summary>
    /// Default window size for incremental collection (in hours)
    /// </summary>
    public int DefaultWindowSizeHours { get; init; } = 24;

    /// <summary>
    /// Maximum window size for incremental collection (in hours)
    /// </summary>
    public int MaxWindowSizeHours { get; init; } = 168; // 7 days

    /// <summary>
    /// Overlap between windows to ensure we don't miss data (in hours)
    /// </summary>
    public int WindowOverlapHours { get; init; } = 1;

    /// <summary>
    /// Maximum number of projects to process in parallel
    /// </summary>
    public int MaxParallelProjects { get; init; } = 1;

    /// <summary>
    /// Delay between processing projects (in milliseconds)
    /// </summary>
    public int ProjectProcessingDelayMs { get; init; } = 100;

    /// <summary>
    /// Maximum number of retries for failed operations
    /// </summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>
    /// Base delay for exponential backoff (in milliseconds)
    /// </summary>
    public int RetryDelayMs { get; init; } = 1000;

    /// <summary>
    /// Whether to collect review events and discussions
    /// </summary>
    public bool CollectReviewEvents { get; init; } = true;

    /// <summary>
    /// Whether to collect commit statistics (additions/deletions)
    /// </summary>
    public bool CollectCommitStats { get; init; } = true;

    /// <summary>
    /// Whether to enrich merge request data with additional information
    /// </summary>
    public bool EnrichMergeRequestData { get; init; } = true;
}
