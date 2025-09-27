using System.Text.Json;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Entities;

/// <summary>
/// Pre-calculated aggregated metrics by time periods with full PRD metrics support
/// </summary>
public sealed class DeveloperMetricsAggregate
{
    public long Id { get; init; }
    public long DeveloperId { get; init; }
    
    // Window information
    public DateTimeOffset WindowStart { get; init; }
    public DateTimeOffset WindowEnd { get; init; }
    public int WindowDays { get; init; }
    
    // Schema versioning
    public required string SchemaVersion { get; init; }
    
    // PRD Metrics - Cycle time and review metrics (medians)
    public decimal? MrCycleTimeP50H { get; init; }
    public decimal? TimeToFirstReviewP50H { get; init; }
    public decimal? TimeInReviewP50H { get; init; }
    public decimal? WipAgeP50H { get; init; }
    public decimal? WipAgeP90H { get; init; }
    public decimal? BranchTtlP50H { get; init; }
    public decimal? BranchTtlP90H { get; init; }

    // Rate and ratio metrics
    public decimal? PipelineSuccessRate { get; init; }
    public decimal? ApprovalBypassRatio { get; init; }
    public decimal? ReworkRate { get; init; }
    public decimal? FlakyJobRate { get; init; }
    public decimal? SignedCommitRatio { get; init; }
    public decimal? IssueSlaBreachRate { get; init; }
    public decimal? ReopenedIssueRate { get; init; }
    public decimal? DefectEscapeRate { get; init; }

    // Count-based metrics
    public int DeploymentFrequencyWk { get; init; }
    public int MrThroughputWk { get; init; }
    public int WipMrCount { get; init; }
    public int ReleasesCadenceWk { get; init; }
    public int RollbackIncidence { get; init; }
    public int DirectPushesDefault { get; init; }
    public int ForcePushesProtected { get; init; }

    // Duration metrics (seconds)
    public decimal? MeanTimeToGreenSec { get; init; }
    public decimal? AvgPipelineDurationSec { get; init; }

    // Audit metadata stored as JSON
    public JsonDocument? AuditMetadata { get; init; }
    
    // Null reasons stored as JSON
    public JsonDocument? NullReasons { get; init; }

    public DateTimeOffset CalculatedAt { get; init; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public Developer Developer { get; init; } = null!;
}
