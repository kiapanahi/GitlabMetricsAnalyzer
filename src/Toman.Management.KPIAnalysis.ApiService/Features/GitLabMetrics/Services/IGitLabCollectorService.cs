namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

public interface IGitLabCollectorService
{
    Task RunIncrementalCollectionAsync(CancellationToken cancellationToken = default);
    Task RunBackfillCollectionAsync(CancellationToken cancellationToken = default);
}
