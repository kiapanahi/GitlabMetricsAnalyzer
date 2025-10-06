using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services.LegacyStubs;

internal sealed class MetricsAggregatesPersistenceService : IMetricsAggregatesPersistenceService
{
    public Task<long> PersistAggregateAsync(PerDeveloperMetricsResult result, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(0L);
    }

    public Task<IReadOnlyList<long>> PersistAggregatesAsync(IEnumerable<PerDeveloperMetricsResult> results, CancellationToken cancellationToken = default)
    {
        return Task.FromResult((IReadOnlyList<long>)Array.Empty<long>());
    }

    public Task<PerDeveloperMetricsResult?> GetAggregateAsync(long developerId, int windowDays, DateTime windowEnd, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<PerDeveloperMetricsResult?>(null);
    }

    public Task<IReadOnlyList<PerDeveloperMetricsResult>> GetAggregatesAsync(IEnumerable<long> developerIds, int windowDays, DateTime windowEnd, CancellationToken cancellationToken = default)
    {
        return Task.FromResult((IReadOnlyList<PerDeveloperMetricsResult>)Array.Empty<PerDeveloperMetricsResult>());
    }

    public Task<bool> AggregateExistsAsync(long developerId, int windowDays, DateTime windowEnd, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }
}
