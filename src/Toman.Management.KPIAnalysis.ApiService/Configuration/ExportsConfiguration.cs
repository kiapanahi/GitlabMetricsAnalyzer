namespace Toman.Management.KPIAnalysis.ApiService.Configuration;

public sealed class ExportsConfiguration
{
    public const string SectionName = "Exports";

    public required string Directory { get; init; }
}
