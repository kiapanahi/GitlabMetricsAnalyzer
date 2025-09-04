namespace Toman.Management.KPIAnalysis.ApiService.Models.Dimensions;

public sealed class DimUser
{
    public required int UserId { get; init; }
    public required string Username { get; init; }
    public required string Name { get; init; }
    public required string State { get; init; }
    public bool IsBot { get; init; }
    public required string EmailHash { get; init; }
}
