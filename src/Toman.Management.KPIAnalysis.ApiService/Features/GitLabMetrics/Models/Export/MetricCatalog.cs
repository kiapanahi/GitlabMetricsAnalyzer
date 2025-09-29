using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Export;

/// <summary>
/// Metric catalog structure as defined in PRD
/// </summary>
public sealed class MetricCatalog
{
    public required string Version { get; init; }
    public required DateTime GeneratedAt { get; init; }
    public required string Description { get; init; }
    public required IReadOnlyList<MetricDefinition> Metrics { get; init; }
}

/// <summary>
/// Individual metric definition in the catalog
/// </summary>
public sealed class MetricDefinition
{
    public required string Name { get; init; }
    public required string DisplayName { get; init; }
    public required string Description { get; init; }
    public required string DataType { get; init; }
    public required string Unit { get; init; }
    public string? Category { get; init; }
    public bool IsNullable { get; init; }
    public string? NullReason { get; init; }
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Per-developer metrics export matching PRD structure with schema versioning
/// </summary>
public sealed class PerDeveloperMetricsExport
{
    public required string SchemaVersion { get; init; }
    public required long DeveloperId { get; init; }
    public required string DeveloperName { get; init; }
    public required string DeveloperEmail { get; init; }
    public required DateTime ComputationDate { get; init; }
    public required DateTime WindowStart { get; init; }
    public required DateTime WindowEnd { get; init; }
    public required int WindowDays { get; init; }
    public required PerDeveloperMetrics Metrics { get; init; }
    public required MetricsAudit Audit { get; init; }
}
