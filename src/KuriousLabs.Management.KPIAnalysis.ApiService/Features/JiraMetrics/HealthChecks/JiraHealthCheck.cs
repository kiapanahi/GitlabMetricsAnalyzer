using Microsoft.Extensions.Diagnostics.HealthChecks;

using KuriousLabs.Management.KPIAnalysis.ApiService.Features.JiraMetrics.Infrastructure;

namespace KuriousLabs.Management.KPIAnalysis.ApiService.Features.JiraMetrics.HealthChecks;

public sealed class JiraHealthCheck : IHealthCheck
{
    private readonly IJiraHttpClient _jiraHttpClient;
    private readonly ILogger<JiraHealthCheck> _logger;

    public JiraHealthCheck(IJiraHttpClient jiraHttpClient, ILogger<JiraHealthCheck> logger)
    {
        _jiraHttpClient = jiraHttpClient;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var serverInfo = await _jiraHttpClient.GetServerInfoAsync(cancellationToken);

            if (serverInfo is not null)
            {
                _logger.LogDebug("Jira connection test successful. Server: {ServerTitle}, Version: {Version}", 
                    serverInfo.ServerTitle, serverInfo.Version);
                
                var data = new Dictionary<string, object>
                {
                    ["version"] = serverInfo.Version ?? "unknown",
                    ["deploymentType"] = serverInfo.DeploymentType ?? "unknown",
                    ["serverTitle"] = serverInfo.ServerTitle ?? "unknown"
                };
                
                return HealthCheckResult.Healthy("Jira API is accessible", data);
            }
            else
            {
                _logger.LogWarning("Jira connection test failed");
                return HealthCheckResult.Unhealthy("Jira API is not accessible");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Jira health check failed with exception");
            return HealthCheckResult.Unhealthy("Jira API health check failed", ex);
        }
    }
}
