namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Operational;

/// <summary>
/// Tracks the state of data ingestion for different entities and run types
/// </summary>
public sealed class IngestionState
{
    public int Id { get; init; }
    public required string Entity { get; init; }
    public DateTimeOffset LastSeenUpdatedAt { get; init; }
    public DateTimeOffset LastRunAt { get; init; }
    /// <summary>
    /// Size of the window for incremental runs (in hours). Used for windowed collection.
    /// </summary>
    public int? WindowSizeHours { get; init; }
    /// <summary>
    /// The end time of the last successfully processed window for incremental runs.
    /// </summary>
    public DateTimeOffset? LastWindowEnd { get; init; }
}
