namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Entities;

/// <summary>
/// Pipeline execution with merge request links
/// </summary>
public sealed class PipelineFact
{
    public long Id { get; init; }
    public long ProjectId { get; init; }
    public long PipelineId { get; init; } // GitLab pipeline ID
    public long? MergeRequestFactId { get; init; }
    public long DeveloperId { get; init; }
    public required string RefName { get; init; } // branch/tag name
    public required string Sha { get; init; }
    public required string Status { get; init; } // 'success', 'failed', 'canceled', etc.
    public string? Source { get; init; } // 'push', 'merge_request_event', etc.
    public DateTime CreatedAt { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? FinishedAt { get; init; }
    public int? DurationSeconds { get; init; }

    public DateTimeOffset CreatedAtFact { get; init; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public Project Project { get; init; } = null!;
    public MergeRequestFact? MergeRequestFact { get; init; }
    public Developer Developer { get; init; } = null!;
}
