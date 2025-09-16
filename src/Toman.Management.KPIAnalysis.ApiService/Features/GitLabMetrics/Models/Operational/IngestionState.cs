namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Operational;

public sealed class IngestionState
{
    public int Id { get; init; }
    public required string Entity { get; init; }
    public DateTimeOffset LastSeenUpdatedAt { get; init; }
    public DateTimeOffset LastRunAt { get; init; }
}
