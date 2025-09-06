namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Dimensions;

public sealed class DimGroup
{
    public required int GroupId { get; init; }
    public required string Name { get; init; }
    public required string Path { get; init; }
    public required string FullName { get; init; }
    public required string FullPath { get; init; }
    public int? ParentId { get; init; }
    public required string Visibility { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset LastDiscoveredAt { get; init; }
}
