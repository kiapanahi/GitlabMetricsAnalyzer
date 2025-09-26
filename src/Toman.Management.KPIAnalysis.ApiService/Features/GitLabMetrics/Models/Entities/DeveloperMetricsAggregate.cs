namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Entities;

/// <summary>
/// Pre-calculated aggregated metrics by time periods
/// </summary>
public sealed class DeveloperMetricsAggregate
{
    public long Id { get; init; }
    public long DeveloperId { get; init; }
    public required string PeriodType { get; init; } // 'daily', 'weekly', 'monthly', 'quarterly'
    public DateOnly PeriodStart { get; init; }
    public DateOnly PeriodEnd { get; init; }

    // Commit metrics
    public int CommitsCount { get; init; } = 0;
    public int LinesAdded { get; init; } = 0;
    public int LinesDeleted { get; init; } = 0;
    public int FilesChanged { get; init; } = 0;

    // MR metrics
    public int MrsCreated { get; init; } = 0;
    public int MrsMerged { get; init; } = 0;
    public int MrsReviewed { get; init; } = 0;
    public decimal? AvgCycleTimeHours { get; init; }

    // Pipeline metrics
    public int PipelinesTriggered { get; init; } = 0;
    public int SuccessfulPipelines { get; init; } = 0;
    public decimal? PipelineSuccessRate { get; init; }

    // Collaboration metrics
    public int ReviewsGiven { get; init; } = 0;
    public int UniqueCollaborators { get; init; } = 0;

    public DateTimeOffset CalculatedAt { get; init; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public Developer Developer { get; init; } = null!;
}