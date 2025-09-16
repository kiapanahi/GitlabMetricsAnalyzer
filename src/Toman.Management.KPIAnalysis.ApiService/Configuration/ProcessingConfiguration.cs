namespace Toman.Management.KPIAnalysis.ApiService.Configuration;

public sealed class ProcessingConfiguration
{
    public const string SectionName = "Processing";

    public int MaxDegreeOfParallelism { get; init; } = 8;
    public int BackfillDays { get; init; } = 180;
}
