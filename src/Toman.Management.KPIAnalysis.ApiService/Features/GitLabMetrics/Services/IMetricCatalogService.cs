using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Export;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

/// <summary>
/// Service for generating metric catalogs and exports
/// </summary>
public interface IMetricCatalogService
{
    /// <summary>
    /// Generate metric catalog JSON from current schema
    /// </summary>
    Task<MetricCatalog> GenerateCatalogAsync();
    
    /// <summary>
    /// Generate per-developer metrics export from persisted aggregates
    /// </summary>
    Task<IReadOnlyList<PerDeveloperMetricsExport>> GeneratePerDeveloperExportsAsync(
        IEnumerable<long> developerIds, 
        int windowDays, 
        DateTimeOffset windowEnd,
        CancellationToken cancellationToken = default);
        
    /// <summary>
    /// Generate per-developer metrics export from computation results
    /// </summary>
    IReadOnlyList<PerDeveloperMetricsExport> GeneratePerDeveloperExportsFromResults(
        IEnumerable<PerDeveloperMetricsResult> results);
}