namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Facts;

public sealed class FactMergeRequest
{
    public required int MrId { get; init; }
    public required int ProjectId { get; init; }
    public decimal CycleTimeHours { get; init; }
    public decimal ReviewWaitHours { get; init; }
    public int ReworkCount { get; init; }
    public int LinesAdded { get; init; }
    public int LinesRemoved { get; init; }
}
