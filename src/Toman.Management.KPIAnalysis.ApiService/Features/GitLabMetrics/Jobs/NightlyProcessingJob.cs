using Quartz;

using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Jobs;

public sealed class NightlyProcessingJob : IJob
{
    private readonly IGitLabCollectorService _collectorService;
    private readonly ILogger<NightlyProcessingJob> _logger;

    public NightlyProcessingJob(
        IGitLabCollectorService collectorService,
        ILogger<NightlyProcessingJob> logger)
    {
        _collectorService = collectorService;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("Starting nightly processing job");

        try
        {
            // Run backfill collection
            await _collectorService.RunBackfillCollectionAsync(context.CancellationToken);

            _logger.LogInformation("Completed nightly processing job");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during nightly processing job");
            throw;
        }
    }
}
