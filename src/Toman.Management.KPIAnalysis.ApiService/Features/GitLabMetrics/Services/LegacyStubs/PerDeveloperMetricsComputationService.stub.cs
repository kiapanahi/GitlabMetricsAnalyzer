namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services.LegacyStubs;

internal sealed class PerDeveloperMetricsComputationService : IPerDeveloperMetricsComputationService
{
    public Task<PerDeveloperMetricsResult> ComputeMetricsAsync(long developerId, MetricsComputationOptions options, CancellationToken cancellationToken = default)
    {
        var result = new PerDeveloperMetricsResult
        {
            DeveloperId = developerId,
            DeveloperName = "Unknown",
            DeveloperEmail = "unknown@example.com",
            ComputationDate = DateTime.UtcNow,
            WindowStart = options.EndDate.AddDays(-options.WindowDays),
            WindowEnd = options.EndDate,
            WindowDays = options.WindowDays,
            Metrics = new PerDeveloperMetrics(),
            Audit = new MetricsAudit()
        };

        return Task.FromResult(result);
    }

    public Task<Dictionary<long, PerDeveloperMetricsResult>> ComputeMetricsAsync(IEnumerable<long> developerIds, MetricsComputationOptions options, CancellationToken cancellationToken = default)
    {
        var dict = developerIds.ToDictionary(id => id, id => new PerDeveloperMetricsResult
        {
            DeveloperId = id,
            DeveloperName = "Unknown",
            DeveloperEmail = "unknown@example.com",
            ComputationDate = DateTime.UtcNow,
            WindowStart = options.EndDate.AddDays(-options.WindowDays),
            WindowEnd = options.EndDate,
            WindowDays = options.WindowDays,
            Metrics = new PerDeveloperMetrics(),
            Audit = new MetricsAudit()
        });

        return Task.FromResult(dict);
    }

    public IReadOnlyList<int> GetSupportedWindowDays()
    {
        return new[] { 14, 28, 90 };
    }
}
