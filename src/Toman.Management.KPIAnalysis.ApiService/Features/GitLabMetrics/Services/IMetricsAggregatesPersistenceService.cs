using System.Text.Json;

using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

/// <summary>
/// Service for persisting per-developer metrics aggregates with audit metadata
/// </summary>
public interface IMetricsAggregatesPersistenceService
{
    /// <summary>
    /// Persist a single per-developer metrics result to the database
    /// </summary>
    Task<long> PersistAggregateAsync(PerDeveloperMetricsResult result, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Persist multiple per-developer metrics results to the database
    /// </summary>
    Task<IReadOnlyList<long>> PersistAggregatesAsync(IEnumerable<PerDeveloperMetricsResult> results, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Retrieve persisted aggregate for a developer and window
    /// </summary>
    Task<PerDeveloperMetricsResult?> GetAggregateAsync(long developerId, int windowDays, DateTimeOffset windowEnd, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Retrieve multiple persisted aggregates
    /// </summary>
    Task<IReadOnlyList<PerDeveloperMetricsResult>> GetAggregatesAsync(IEnumerable<long> developerIds, int windowDays, DateTimeOffset windowEnd, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if aggregate already exists for the given parameters
    /// </summary>
    Task<bool> AggregateExistsAsync(long developerId, int windowDays, DateTimeOffset windowEnd, CancellationToken cancellationToken = default);
}