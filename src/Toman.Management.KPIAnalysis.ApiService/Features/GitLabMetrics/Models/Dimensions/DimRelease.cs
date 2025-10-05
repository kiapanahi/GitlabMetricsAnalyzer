namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Dimensions;

public sealed class DimRelease
{
    public required int ProjectId { get; init; }
    public required string TagName { get; init; }
    public DateTime ReleasedAt { get; init; }
    public bool SemverValid { get; init; }
}
