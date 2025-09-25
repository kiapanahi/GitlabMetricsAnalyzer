namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Methodology;

/// <summary>
/// Comprehensive methodology information for a specific metric
/// </summary>
public sealed record MethodologyInfo
{
    public required string MetricName { get; init; }
    public required string Definition { get; init; }
    public required string Calculation { get; init; }
    public required List<DataSource> DataSources { get; init; }
    public required List<string> Limitations { get; init; }
    public required InterpretationGuide Interpretation { get; init; }
    public string? IndustryContext { get; init; }
    public required DateTimeOffset LastUpdated { get; init; }
    public required string Version { get; init; }
}

/// <summary>
/// Data source information for a metric
/// </summary>
public sealed record DataSource
{
    public required string Source { get; init; }
    public required string Type { get; init; } // "exact", "approximation", "derived"
    public string? Description { get; init; }
}

/// <summary>
/// Interpretation guide for metric values
/// </summary>
public sealed record InterpretationGuide
{
    public required Dictionary<string, string> Ranges { get; init; }
    public required List<InterpretationNote> Notes { get; init; }
}

/// <summary>
/// Interpretation note with context
/// </summary>
public sealed record InterpretationNote
{
    public required string Context { get; init; }
    public required string Explanation { get; init; }
}

/// <summary>
/// Methodology change log entry
/// </summary>
public sealed record MethodologyChange
{
    public required string Metric { get; init; }
    public required string Version { get; init; }
    public required DateTimeOffset ChangeDate { get; init; }
    public required List<string> Changes { get; init; }
    public required string Rationale { get; init; }
    public string? ApprovedBy { get; init; }
}

/// <summary>
/// Audit trail entry for methodology compliance
/// </summary>
public sealed record AuditTrailEntry
{
    public required string Metric { get; init; }
    public required DateTimeOffset CalculatedAt { get; init; }
    public required string AlgorithmVersion { get; init; }
    public required double DataQualityScore { get; init; }
    public List<string>? ManualAdjustments { get; init; }
    public string? Notes { get; init; }
}