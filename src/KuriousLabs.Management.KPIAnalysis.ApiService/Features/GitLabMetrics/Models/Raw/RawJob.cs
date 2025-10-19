namespace KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;

public sealed class RawJob
{
    public long Id { get; init; } // Auto-incrementing primary key
    public required long ProjectId { get; init; }
    public required long JobId { get; init; }
    public required long PipelineId { get; init; }
    public required string Name { get; init; }
    public required string Status { get; init; }
    public int DurationSec { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? FinishedAt { get; init; }
    public bool RetriedFlag { get; init; }
}
