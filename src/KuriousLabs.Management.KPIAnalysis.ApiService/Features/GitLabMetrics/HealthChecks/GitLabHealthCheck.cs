using Microsoft.Extensions.Diagnostics.HealthChecks;

using KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure;

namespace KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.HealthChecks;

public sealed class GitLabHealthCheck : IHealthCheck
{
    private readonly IGitLabService _gitLabService;
    private readonly ILogger<GitLabHealthCheck> _logger;

    public GitLabHealthCheck(IGitLabService gitLabService, ILogger<GitLabHealthCheck> logger)
    {
        _gitLabService = gitLabService;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var isConnected = await _gitLabService.TestConnectionAsync(cancellationToken);

            if (isConnected)
            {
                _logger.LogDebug("GitLab connection test successful");
                return HealthCheckResult.Healthy("GitLab API is accessible");
            }
            else
            {
                _logger.LogWarning("GitLab connection test failed");
                return HealthCheckResult.Unhealthy("GitLab API is not accessible");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GitLab health check failed with exception");
            return HealthCheckResult.Unhealthy("GitLab API health check failed", ex);
        }
    }
}
