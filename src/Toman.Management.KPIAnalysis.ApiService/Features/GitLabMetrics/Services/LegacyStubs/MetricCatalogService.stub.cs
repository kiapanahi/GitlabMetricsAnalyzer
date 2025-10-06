using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Export;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services.LegacyStubs;

// TODO: Temporary stub to allow staged deletions. Replace with real implementation or remove when tests updated.
internal sealed class MetricCatalogService : IMetricCatalogService
{
    public Task<MetricCatalog> GenerateCatalogAsync()
    {
        var catalog = new MetricCatalog
        {
            Version = SchemaVersion.Current,
            GeneratedAt = DateTime.UtcNow,
            Description = "Temporary stub catalog",
            Metrics = new List<MetricDefinition>()
        };

        return Task.FromResult(catalog);
    }

    public Task<IReadOnlyList<PerDeveloperMetricsExport>> GeneratePerDeveloperExportsAsync(IEnumerable<long> developerIds, int windowDays, DateTime windowEnd, CancellationToken cancellationToken = default)
    {
        return Task.FromResult((IReadOnlyList<PerDeveloperMetricsExport>)Array.Empty<PerDeveloperMetricsExport>());
    }

    public IReadOnlyList<PerDeveloperMetricsExport> GeneratePerDeveloperExportsFromResults(IEnumerable<PerDeveloperMetricsResult> results)
    {
        return Array.Empty<PerDeveloperMetricsExport>();
    }
}
