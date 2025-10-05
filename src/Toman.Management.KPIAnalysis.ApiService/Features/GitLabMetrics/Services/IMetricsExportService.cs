namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

/// <summary>
/// Service for managing metrics export pipeline with immutable storage
/// </summary>
public interface IMetricsExportService
{
    /// <summary>
    /// Export metric catalog to JSON file
    /// </summary>
    Task<string> ExportCatalogAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Export per-developer metrics to JSON files with immutable naming
    /// </summary>
    Task<ExportResult> ExportPerDeveloperMetricsAsync(
        IEnumerable<long> developerIds,
        int windowDays,
        DateTime windowEnd,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Export per-developer metrics from computation results
    /// </summary>
    Task<ExportResult> ExportPerDeveloperMetricsFromResultsAsync(
        IEnumerable<PerDeveloperMetricsResult> results,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get list of available export files
    /// </summary>
    Task<IReadOnlyList<ExportFileInfo>> GetAvailableExportsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of export operation
/// </summary>
public sealed class ExportResult
{
    public required string CatalogFilePath { get; init; }
    public required IReadOnlyList<string> DataFilePaths { get; init; }
    public required int ExportedCount { get; init; }
    public required DateTime ExportedAt { get; init; }
    public required string SchemaVersion { get; init; }
}

/// <summary>
/// Information about an export file
/// </summary>
public sealed class ExportFileInfo
{
    public required string FilePath { get; init; }
    public required string FileName { get; init; }
    public required string FileType { get; init; } // "catalog", "per-developer"
    public required long FileSizeBytes { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required string SchemaVersion { get; init; }
    public int? WindowDays { get; init; }
    public DateTimeOffset? WindowEnd { get; init; }
}
