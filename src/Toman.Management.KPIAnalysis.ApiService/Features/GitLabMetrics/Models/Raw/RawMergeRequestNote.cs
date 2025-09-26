namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;

/// <summary>
/// Represents a raw merge request comment/note from GitLab API
/// </summary>
public sealed class RawMergeRequestNote
{
    public long Id { get; init; } // Auto-incrementing primary key
    public required long ProjectId { get; init; }
    public required string ProjectName { get; init; }
    public required long MergeRequestIid { get; init; }
    public required long NoteId { get; init; }
    public required long AuthorId { get; init; }
    public required string AuthorName { get; init; }
    public required string Body { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
    public bool System { get; init; } // System-generated notes (auto comments)
    public bool Resolvable { get; init; } // Can be resolved (code review comments)
    public bool Resolved { get; init; } // Is resolved
    public long? ResolvedById { get; init; }
    public string? ResolvedBy { get; init; }
    public string? NoteableType { get; init; } // "MergeRequest" 
    public DateTimeOffset IngestedAt { get; init; }
    
    // Helper properties
    public bool IsUserComment => !System; // Not a system-generated comment
    public bool IsCodeReviewComment => Resolvable; // Comment on code lines
}