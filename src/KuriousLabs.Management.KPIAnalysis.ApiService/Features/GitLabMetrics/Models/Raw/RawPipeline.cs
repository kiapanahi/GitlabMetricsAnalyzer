namespace KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;

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
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? FinishedAt { get; init; }
    public int DurationSec { get; init; }
    public string? Environment { get; init; }
    public DateTime IngestedAt { get; init; }

    // Calculated success rate helper
    public bool IsSuccessful => Status.Equals("success", StringComparison.OrdinalIgnoreCase);
}
