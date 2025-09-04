namespace Toman.Management.KPIAnalysis.ApiService.Models.Dimensions;

public sealed class DimProject
{
    public required int ProjectId { get; init; }
    public required string PathWithNamespace { get; init; }
    public required string DefaultBranch { get; init; }
    public required string Visibility { get; init; }
    public bool ActiveFlag { get; init; } = true;
}
