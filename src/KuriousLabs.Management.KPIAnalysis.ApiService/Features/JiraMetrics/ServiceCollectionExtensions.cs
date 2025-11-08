using System.Net.Http.Headers;

using Microsoft.Extensions.Options;

using KuriousLabs.Management.KPIAnalysis.ApiService.Features.JiraMetrics.Configuration;
using KuriousLabs.Management.KPIAnalysis.ApiService.Features.JiraMetrics.HealthChecks;
using KuriousLabs.Management.KPIAnalysis.ApiService.Features.JiraMetrics.Infrastructure;

namespace KuriousLabs.Management.KPIAnalysis.ApiService.Features.JiraMetrics;

internal static class ServiceCollectionExtensions
{
    internal static IHostApplicationBuilder AddJiraMetricsServices(this IHostApplicationBuilder builder)
    {
        // Add OpenTelemetry for tracing and metrics
        builder.Services.AddOpenTelemetry()
            .WithTracing(tracing => tracing.AddSource(Diagnostics.ActivitySource.Name))
            .WithMetrics(metrics => metrics.AddMeter(Diagnostics.Meter.Name));

        // Add Jira health check
        builder.Services.AddHealthChecks().AddJiraHealthCheck();

        // Add configuration
        builder.Services.Configure<JiraConfiguration>(
            builder.Configuration.GetSection(JiraConfiguration.SectionName));

        // Add HTTP client for Jira API calls with resilience policies
        builder.Services
            .AddHttpClient<IJiraHttpClient, JiraHttpClient>((sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<JiraConfiguration>>();
                var configuration = options.Value;
                
                client.BaseAddress = new Uri(configuration.BaseUrl.TrimEnd('/') + "/");
                client.DefaultRequestHeaders.Authorization = 
                    new AuthenticationHeaderValue("Bearer", configuration.Token);
                client.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));
                
                // Add User-Agent header
                client.DefaultRequestHeaders.Add("User-Agent", "GitLabMetricsAnalyzer/1.0");
                
                // Set reasonable timeout for Jira API calls
                client.Timeout = TimeSpan.FromMinutes(2);
            })
            .AddStandardResilienceHandler(options =>
            {
                // Configure retry policy for Jira API
                options.Retry.MaxRetryAttempts = 3;
                options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
                options.Retry.UseJitter = true;
                options.Retry.Delay = TimeSpan.FromSeconds(1);

                // Configure circuit breaker for Jira API  
                options.CircuitBreaker.FailureRatio = 0.3; // Break if 30% of requests fail
                options.CircuitBreaker.MinimumThroughput = 10; // At least 10 requests needed
                options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);

                // Configure total request timeout
                options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(5);
            });

        // Services will be registered here as they are implemented
        
        return builder;
    }

    internal static IHealthChecksBuilder AddJiraHealthCheck(this IHealthChecksBuilder builder)
    {
        return builder.AddCheck<JiraHealthCheck>("jira", tags: ["ready"]);
    }
}
