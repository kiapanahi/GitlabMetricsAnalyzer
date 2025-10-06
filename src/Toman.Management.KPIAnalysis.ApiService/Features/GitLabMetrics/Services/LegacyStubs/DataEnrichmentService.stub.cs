namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services.LegacyStubs;

internal sealed class DataEnrichmentService : IDataEnrichmentService
{
    public Task EnrichAsync(CancellationToken cancellationToken = default)
    {
        // No-op stub
        return Task.CompletedTask;
    }
}
