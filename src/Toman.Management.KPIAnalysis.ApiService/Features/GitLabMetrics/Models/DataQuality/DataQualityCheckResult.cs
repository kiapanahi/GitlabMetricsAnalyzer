namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.DataQuality;

/// <summary>
/// Represents the result of a data quality check
/// </summary>
public sealed class DataQualityCheckResult
{
    public required string CheckType { get; init; }
    public required string Status { get; init; } // "passed", "failed", "warning"
    public double? Score { get; init; } // 0.0 to 1.0
    public required string Description { get; init; }
    public string? Details { get; init; }
    public DateTime CheckedAt { get; init; } = DateTime.UtcNow;
    public Guid? RunId { get; init; }
}

/// <summary>
/// Overall data quality report
/// </summary>
public sealed class DataQualityReport
{
    public required IReadOnlyList<DataQualityCheckResult> Checks { get; init; }
    public required string OverallStatus { get; init; } // "healthy", "warning", "critical"
    public double OverallScore { get; init; } // Average of all check scores
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
    public Guid? RunId { get; init; }

    /// <summary>
    /// Get checks by status
    /// </summary>
    public IReadOnlyList<DataQualityCheckResult> GetChecksByStatus(string status) =>
        Checks.Where(c => c.Status.Equals(status, StringComparison.OrdinalIgnoreCase)).ToList();

    /// <summary>
    /// Check if the overall quality is acceptable
    /// </summary>
    public bool IsHealthy => OverallStatus == "healthy";
}
