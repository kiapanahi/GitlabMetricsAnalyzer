namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Entities;

/// <summary>
/// MR-level metrics with timeline and flags
/// </summary>
public sealed class MergeRequestFact
{
    public long Id { get; init; }
    public long ProjectId { get; init; }
    public int MrIid { get; init; } // Internal ID within project
    public long AuthorDeveloperId { get; init; }
    public required string TargetBranch { get; init; }
    public required string SourceBranch { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? MergedAt { get; init; }
    public DateTimeOffset? ClosedAt { get; init; }
    public DateTimeOffset? FirstReviewAt { get; init; }
    public required string State { get; init; } // 'opened', 'closed', 'merged'

    // Metrics
    public int LinesAdded { get; init; } = 0;
    public int LinesDeleted { get; init; } = 0;
    public int CommitsCount { get; init; } = 0;
    public int FilesChanged { get; init; } = 0;

    // Timeline metrics (hours)
    public decimal? CycleTimeHours { get; init; } // created to merged
    public decimal? ReviewTimeHours { get; init; } // created to first review

    // Flags
    public bool HasPipeline { get; init; } = false;
    public bool IsDraft { get; init; } = false;
    public bool IsWip { get; init; } = false;
    public bool HasConflicts { get; init; } = false;

    public DateTimeOffset CreatedAtFact { get; init; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public Project Project { get; init; } = null!;
    public Developer AuthorDeveloper { get; init; } = null!;
    public ICollection<PipelineFact> Pipelines { get; init; } = new List<PipelineFact>();
    public ICollection<ReviewEvent> ReviewEvents { get; init; } = new List<ReviewEvent>();
}