namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Dimensions;

public sealed class DimBranch
{
    public required int ProjectId { get; init; }
    public required string Branch { get; init; }
    public bool ProtectedFlag { get; init; }
}
