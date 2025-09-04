namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;

public sealed class RawJob
{
    public required int ProjectId { get; init; }
    public required int JobId { get; init; }
    public required int PipelineId { get; init; }
    public required string Name { get; init; }
    public required string Status { get; init; }
    public int DurationSec { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? FinishedAt { get; init; }
    public bool RetriedFlag { get; init; }
}
