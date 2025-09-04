namespace Toman.Management.KPIAnalysis.ApiService.Models.Facts;

public sealed class FactPipeline
{
    public required int PipelineId { get; init; }
    public required int ProjectId { get; init; }
    public int MtgSeconds { get; init; }
    public bool IsProd { get; init; }
    public bool IsRollback { get; init; }
    public bool IsFlakyCandidate { get; init; }
    public int DurationSec { get; init; }
}
