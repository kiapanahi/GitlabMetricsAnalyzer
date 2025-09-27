namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Entities;

/// <summary>
/// Track all review-related activities
/// </summary>
public sealed class ReviewEvent
{
    public long Id { get; init; }
    public long MergeRequestFactId { get; init; }
    public long ReviewerDeveloperId { get; init; }
    public required string EventType { get; init; } // 'reviewed', 'approved', 'requested_changes', 'comment'
    public DateTimeOffset OccurredAt { get; init; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public MergeRequestFact MergeRequestFact { get; init; } = null!;
    public Developer ReviewerDeveloper { get; init; } = null!;
}
