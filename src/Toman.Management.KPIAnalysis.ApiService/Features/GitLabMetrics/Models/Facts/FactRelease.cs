namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Facts;

public sealed class FactRelease
{
    public required string TagName { get; init; }
    public required int ProjectId { get; init; }
    public bool IsSemver { get; init; }
    public required string CadenceBucket { get; init; }
}
