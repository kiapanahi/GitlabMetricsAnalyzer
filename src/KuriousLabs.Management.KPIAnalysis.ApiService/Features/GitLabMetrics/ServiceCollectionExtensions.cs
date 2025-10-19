using System.Net.Http.Headers;

using Microsoft.Extensions.Options;

using KuriousLabs.Management.KPIAnalysis.ApiService.Configuration;
using KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Configuration;
using KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.HealthChecks;
using KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure;
using KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

namespace KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics;

internal static class ServiceCollectionExtensions
{
    internal static IHostApplicationBuilder AddGitLabMetricsServices(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOpenTelemetry()
        .WithTracing(tracing => tracing.AddSource(Diagnostics.ActivitySource.Name))
        .WithMetrics(metrics => metrics.AddMeter("KuriousLabs.Management.KPIAnalysis.GitLabMetrics"));

        // Add GitLab health check
        builder.Services.AddHealthChecks().AddGitLabHealthCheck();

        // Add configurations
        builder.Services.Configure<GitLabConfiguration>(builder.Configuration.GetSection(GitLabConfiguration.SectionName));
        builder.Services.Configure<MetricsConfiguration>(builder.Configuration.GetSection(MetricsConfiguration.SectionName));

        // Add services
        // We'll keep CommitTimeAnalysis and shared infra always registered.
        builder.Services.AddScoped<ICommitTimeAnalysisService, CommitTimeAnalysisService>();
        builder.Services.AddScoped<IPerDeveloperMetricsService, PerDeveloperMetricsService>();
        builder.Services.AddScoped<ICollaborationMetricsService, CollaborationMetricsService>();
        builder.Services.AddScoped<IQualityMetricsService, QualityMetricsService>();
        builder.Services.AddScoped<ICodeCharacteristicsService, CodeCharacteristicsService>();
        builder.Services.AddScoped<IPipelineMetricsService, PipelineMetricsService>();
        builder.Services.AddScoped<IAdvancedMetricsService, AdvancedMetricsService>();
        builder.Services.AddScoped<ITeamMetricsService, TeamMetricsService>();
        builder.Services.AddScoped<IProjectMetricsService, ProjectMetricsService>();

        // Add HTTP client for GitLab API calls
        builder.Services
            .AddHttpClient<IGitLabHttpClient, GitLabHttpClient>((sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<GitLabConfiguration>>();
                var configuration = options.Value;
                client.BaseAddress = new Uri(configuration.BaseUrl.TrimEnd('/') + "/api/v4/");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", configuration.Token);

                // Add GitLab-specific headers
                client.DefaultRequestHeaders.Add("User-Agent", "GitLabMetricsAnalyzer/1.0");

                // Set reasonable timeout for GitLab API calls
                client.Timeout = TimeSpan.FromMinutes(2);
            })
            .AddStandardResilienceHandler(options =>
            {
                // Configure retry policy for GitLab API
                options.Retry.MaxRetryAttempts = 3;
                options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
                options.Retry.UseJitter = true;
                options.Retry.Delay = TimeSpan.FromSeconds(1);

                // Configure circuit breaker for GitLab API  
                options.CircuitBreaker.FailureRatio = 0.3; // Break if 30% of requests fail
                options.CircuitBreaker.MinimumThroughput = 10; // At least 10 requests needed
                options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);

                // Configure total request timeout
                options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(5);
            });

        return builder;
    }

    internal static IHealthChecksBuilder AddGitLabHealthCheck(this IHealthChecksBuilder builder)
    {
        return builder.AddCheck<GitLabHealthCheck>("gitlab", tags: ["ready"]);
    }
}
