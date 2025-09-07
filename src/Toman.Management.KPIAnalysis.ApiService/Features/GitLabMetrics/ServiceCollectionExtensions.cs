using System.Net.Http.Headers;

using Microsoft.Extensions.Options;

using Polly;

using Quartz;

using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Configuration;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Data;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Jobs;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics;

internal static class ServiceCollectionExtensions
{
    internal static IHostApplicationBuilder AddGitLabMetricsServices(this IHostApplicationBuilder builder)
    {
        // Add configurations
        builder.Services.Configure<GitLabConfiguration>(builder.Configuration.GetSection(GitLabConfiguration.SectionName));

        // Add database
        builder.AddNpgsqlDbContext<GitLabMetricsDbContext>(Constants.Keys.PostgresDatabase);

        // Add services
        builder.Services.AddScoped<IGitLabCollectorService, GitLabCollectorService>();
        builder.Services.AddScoped<IMetricsProcessorService, MetricsProcessorService>();
        builder.Services.AddScoped<IMetricsExportService, MetricsExportService>();


        // Add HTTP client with resilience for GitLab API
        builder.Services.AddHttpClient<IGitLabApiService, GitLabApiService>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<GitLabConfiguration>>();
            var configuration = options.Value;
            client.BaseAddress = new Uri(configuration.BaseUrl);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", configuration.Token);

        })
        .AddResilienceHandler("gitlab-retry", static resilienceBuilder => resilienceBuilder
        .AddRetry(new()
        {
            MaxRetryAttempts = 3,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true
        }));


        if (!builder.Environment.IsDevelopment())
        {
            // Add Quartz for scheduling
            builder.Services.AddQuartz(q =>
            {
                q.UseSimpleTypeLoader();
                q.UseInMemoryStore();

                // Incremental collection job (hourly at :15)
                var incrementalJobKey = new JobKey("IncrementalCollection");
                q.AddJob<IncrementalCollectionJob>(opts => opts.WithIdentity(incrementalJobKey));
                q.AddTrigger(opts => opts
                    .ForJob(incrementalJobKey)
                    .WithIdentity("IncrementalCollection-trigger")
                    .WithCronSchedule("0 15 * * * ?")); // Every hour at :15

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
}
