namespace Toman.Management.KPIAnalysis.ApiService.Models.Dimensions;

public sealed class DimRelease
{
    public required int ProjectId { get; init; }
    public required string TagName { get; init; }
    public DateTimeOffset ReleasedAt { get; init; }
    public bool SemverValid { get; init; }
}
