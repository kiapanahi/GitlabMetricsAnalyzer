using System.Net.Http.Headers;

using Microsoft.Extensions.Options;

using Quartz;

using Toman.Management.KPIAnalysis.ApiService.Configuration;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Configuration;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Data;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.HealthChecks;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Jobs;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics;

internal static class ServiceCollectionExtensions
{
    internal static IHostApplicationBuilder AddGitLabMetricsServices(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOpenTelemetry()
        .WithTracing(tracing => tracing.AddSource(Diagnostics.ActivitySource.Name))
        .WithMetrics(metrics => metrics.AddMeter("Toman.Management.KPIAnalysis.GitLabMetrics"));

        // Add GitLab health check
        builder.Services.AddHealthChecks().AddGitLabHealthCheck();

        // Add configurations
        builder.Services.Configure<GitLabConfiguration>(builder.Configuration.GetSection(GitLabConfiguration.SectionName));
        builder.Services.Configure<MetricsConfiguration>(builder.Configuration.GetSection(MetricsConfiguration.SectionName));
        builder.Services.Configure<CollectionConfiguration>(builder.Configuration.GetSection(CollectionConfiguration.SectionName));
        builder.Services.Configure<ExportsConfiguration>(builder.Configuration.GetSection(ExportsConfiguration.SectionName));

        // Add database
        builder.AddNpgsqlDbContext<GitLabMetricsDbContext>(Constants.Keys.PostgresDatabase, configureDbContextOptions: options =>
        {
            options.EnableSensitiveDataLogging();
            options.EnableDetailedErrors();
        });

        // Add DbContextFactory for parallel operations
        builder.Services.AddDbContextFactory<GitLabMetricsDbContext>();

        builder.Services.AddHostedService<MigratorBackgroundService>();

        // Add services
        // We'll keep CommitTimeAnalysis and shared infra always registered.
        // Other, legacy/experimental services are gated behind a feature flag
        // so we can safely remove their implementations without breaking DI.
        builder.Services.AddScoped<ICommitTimeAnalysisService, CommitTimeAnalysisService>();

    var enableLegacy = builder.Configuration.GetSection(MetricsConfiguration.SectionName).GetValue<bool>("EnableLegacyMetrics", false);

        if (enableLegacy)
        {
            // NOTE: many legacy implementations are being removed in staged commits.
            // Register only services that still have implementations. Others will be
            // re-introduced or replaced by new implementations later.

            // Keep user sync and identity mapping if implementations exist
            builder.Services.AddScoped<IUserSyncService, UserSyncService>();
            builder.Services.AddScoped<IIdentityMappingService, IdentityMappingService>();

            // Data reset, observability and data quality remain implemented
            builder.Services.AddScoped<IDataResetService, DataResetService>();
            builder.Services.AddSingleton<IObservabilityMetricsService, ObservabilityMetricsService>();
            builder.Services.AddScoped<IDataQualityService, DataQualityService>();
        }

        // Add HTTP client for GitLab API calls (configurable via GitLab:UseMockClient)
        var gitLabConfig = builder.Configuration.GetSection(GitLabConfiguration.SectionName).Get<GitLabConfiguration>();
        if (gitLabConfig?.UseMockClient == true)
        {
            builder.Services.AddSingleton<IGitLabHttpClient, MockGitLabHttpClient>();
        }
        else
        {
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
        }


        if (!builder.Environment.IsDevelopment())
        {
            // Add Quartz for scheduling
            builder.Services.AddQuartz(q =>
            {
                q.UseSimpleTypeLoader();
                q.UseInMemoryStore();

                // Nightly processing job (daily at 02:00)
                var nightlyJobKey = new JobKey("NightlyProcessing");
                q.AddJob<NightlyProcessingJob>(opts => opts.WithIdentity(nightlyJobKey));
                q.AddTrigger(opts => opts
                    .ForJob(nightlyJobKey)
                    .WithIdentity("NightlyProcessing-trigger")
                    .WithCronSchedule("0 0 2 * * ?")); // Daily at 02:00
            });

            builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);
        }

        return builder;
    }

    internal static IHealthChecksBuilder AddGitLabHealthCheck(this IHealthChecksBuilder builder)
    {
        return builder.AddCheck<GitLabHealthCheck>("gitlab", tags: ["ready"]);
    }
}
