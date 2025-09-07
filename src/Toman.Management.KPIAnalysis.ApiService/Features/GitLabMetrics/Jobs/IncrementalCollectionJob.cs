using Quartz;

using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Jobs;

public sealed class IncrementalCollectionJob : IJob
{
    private readonly IGitLabCollectorService _collectorService;
    private readonly IMetricsProcessorService _processorService;
    private readonly ILogger<IncrementalCollectionJob> _logger;

    public IncrementalCollectionJob(
        IGitLabCollectorService collectorService,
        IMetricsProcessorService processorService,
        ILogger<IncrementalCollectionJob> logger)
    {
        _collectorService = collectorService;
        _processorService = processorService;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("Starting incremental collection job");

        try
        {
            await _collectorService.RunIncrementalCollectionAsync(context.CancellationToken);
            await _processorService.ProcessFactsAsync(context.CancellationToken);

            _logger.LogInformation("Completed incremental collection job");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during incremental collection job");
            throw;
        }
    }
}
