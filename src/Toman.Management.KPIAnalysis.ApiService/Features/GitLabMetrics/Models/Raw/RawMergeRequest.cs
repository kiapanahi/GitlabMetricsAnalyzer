namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;

public sealed class RawMergeRequest
{
    public long Id { get; init; } // Auto-incrementing primary key
    public required long ProjectId { get; init; }
    public required string ProjectName { get; init; }
    public required long MrId { get; init; }
    public required long AuthorUserId { get; init; }
    public required string AuthorName { get; init; }
    public required string Title { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? MergedAt { get; init; }
    public DateTimeOffset? ClosedAt { get; init; }
    public required string State { get; init; }
    public int ChangesCount { get; init; }
    public required string SourceBranch { get; init; }
    public required string TargetBranch { get; init; }
    public int ApprovalsRequired { get; init; }
    public int ApprovalsGiven { get; init; }
    public DateTimeOffset? FirstReviewAt { get; init; }
    public string? ReviewerIds { get; init; } // JSON array of reviewer IDs
    public DateTimeOffset IngestedAt { get; init; }

    // Enhanced fields for MR data enrichment
    public string? Labels { get; init; } // JSON array of labels
    public string? FirstCommitSha { get; init; }
    public DateTimeOffset? FirstCommitAt { get; init; }
    public string? FirstCommitMessage { get; init; }
    public bool IsHotfix { get; init; } // Derived from labels or branch patterns
    public bool IsRevert { get; init; } // Derived from title or commit patterns
    public bool IsDraft { get; init; }
    public bool HasConflicts { get; init; }
    public int CommitsCount { get; init; }
    public int LinesAdded { get; init; }
    public int LinesDeleted { get; init; }
    public string? WebUrl { get; init; }

    // Calculated fields for cycle time
    public TimeSpan? CycleTime => MergedAt.HasValue ? MergedAt.Value - CreatedAt : null;
    public TimeSpan? ReviewTime => FirstReviewAt.HasValue ? FirstReviewAt.Value - CreatedAt : null;
}
