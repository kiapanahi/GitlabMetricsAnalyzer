using Quartz;

using Toman.Management.KPIAnalysis.ApiService.Services;

namespace Toman.Management.KPIAnalysis.ApiService.Jobs;

public sealed class NightlyProcessingJob : IJob
{
    private readonly IGitLabCollectorService _collectorService;
    private readonly IMetricsProcessorService _processorService;
    private readonly IMetricsExportService _exportService;
    private readonly ILogger<NightlyProcessingJob> _logger;

    public NightlyProcessingJob(
        IGitLabCollectorService collectorService,
        IMetricsProcessorService processorService,
        IMetricsExportService exportService,
        ILogger<NightlyProcessingJob> logger)
    {
        _collectorService = collectorService;
        _processorService = processorService;
        _exportService = exportService;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("Starting nightly processing job");

        try
        {
            // Run full collection
            await _collectorService.RunIncrementalCollectionAsync(context.CancellationToken);
            
            // Process facts
            await _processorService.ProcessFactsAsync(context.CancellationToken);
            
            // Generate exports for yesterday (since we run at 02:00)
            var exportDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
            await _exportService.WriteExportsAsync(exportDate, context.CancellationToken);
            
            _logger.LogInformation("Completed nightly processing job");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during nightly processing job");
            throw;
        }
    }
}
