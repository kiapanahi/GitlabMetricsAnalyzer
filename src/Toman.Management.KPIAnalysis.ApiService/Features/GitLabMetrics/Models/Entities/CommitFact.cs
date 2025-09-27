namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Entities;

/// <summary>
/// Commit-level metrics and facts
/// </summary>
public sealed class CommitFact
{
    public long Id { get; init; }
    public long ProjectId { get; init; }
    public long DeveloperId { get; init; }
    public required string Sha { get; init; }
    public DateTimeOffset CommittedAt { get; init; }
    public int LinesAdded { get; init; } = 0;
    public int LinesDeleted { get; init; } = 0;
    public int FilesChanged { get; init; } = 0;
    public bool IsSigned { get; init; } = false;
    public bool IsMergeCommit { get; init; } = false;
    public int ParentCount { get; init; } = 1;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public Project Project { get; init; } = null!;
    public Developer Developer { get; init; } = null!;
}
