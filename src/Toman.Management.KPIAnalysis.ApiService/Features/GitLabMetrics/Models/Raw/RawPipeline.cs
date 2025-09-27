namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;

public sealed class RawPipeline
{
    public long Id { get; init; } // Auto-incrementing primary key
    public required long ProjectId { get; init; }
    public required string ProjectName { get; init; }
    public required long PipelineId { get; init; }
    public required string Sha { get; init; }
    public required string Ref { get; init; }
    public required string Status { get; init; }
    public required long AuthorUserId { get; init; }
    public required string AuthorName { get; init; }
    public required string TriggerSource { get; init; } // push, web, schedule, api, etc.
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? FinishedAt { get; init; }
    public int DurationSec { get; init; }
    public string? Environment { get; init; }
    public DateTimeOffset IngestedAt { get; init; }

    // Calculated success rate helper
    public bool IsSuccessful => Status.Equals("success", StringComparison.OrdinalIgnoreCase);
}
