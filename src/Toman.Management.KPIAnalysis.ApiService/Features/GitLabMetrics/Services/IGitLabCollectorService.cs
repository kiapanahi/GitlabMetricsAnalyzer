using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Operational;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

public interface IGitLabCollectorService
{
    Task RunIncrementalCollectionAsync(CancellationToken cancellationToken = default);
    Task RunBackfillCollectionAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Start a new collection run with windowed incremental support
    /// </summary>
    Task<CollectionRunResponse> StartCollectionRunAsync(StartCollectionRunRequest request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get the status of a collection run
    /// </summary>
    Task<CollectionRunResponse?> GetCollectionRunStatusAsync(Guid runId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get recent collection runs with optional filtering
    /// </summary>
    Task<IReadOnlyList<CollectionRunResponse>> GetRecentCollectionRunsAsync(string? runType = null, int limit = 10, CancellationToken cancellationToken = default);
}
