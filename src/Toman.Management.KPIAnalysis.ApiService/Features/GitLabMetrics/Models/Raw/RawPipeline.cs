namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;

public sealed class RawPipeline
{
    public required int ProjectId { get; init; }
    public required int PipelineId { get; init; }
    public required string Sha { get; init; }
    public required string Ref { get; init; }
    public required string Status { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public int DurationSec { get; init; }
    public string? Environment { get; init; }
}
