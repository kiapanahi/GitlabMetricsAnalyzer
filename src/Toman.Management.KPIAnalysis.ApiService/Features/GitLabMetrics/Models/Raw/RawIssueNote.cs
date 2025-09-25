namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;

/// <summary>
/// Represents a raw issue comment/note from GitLab API
/// </summary>
public sealed class RawIssueNote
{
    public long Id { get; init; } // Auto-incrementing primary key
    public required long ProjectId { get; init; }
    public required string ProjectName { get; init; }
    public required long IssueIid { get; init; }
    public required long NoteId { get; init; }
    public required long AuthorId { get; init; }
    public required string AuthorName { get; init; }
    public required string Body { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
    public bool System { get; init; } // System-generated notes (auto comments)
    public string? NoteableType { get; init; } // "Issue"
    public DateTimeOffset IngestedAt { get; init; }
    
    // Helper properties
    public bool IsUserComment => !System; // Not a system-generated comment
}