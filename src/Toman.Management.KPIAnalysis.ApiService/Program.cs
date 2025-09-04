using Microsoft.EntityFrameworkCore;

using Polly;

using Quartz;

using Toman.Management.KPIAnalysis.ApiService.Configuration;
using Toman.Management.KPIAnalysis.ApiService.Data;
using Toman.Management.KPIAnalysis.ApiService.Features.Collection;
using Toman.Management.KPIAnalysis.ApiService.Features.Exports;
using Toman.Management.KPIAnalysis.ApiService.Features.Health;
using Toman.Management.KPIAnalysis.ApiService.Features.Status;
using Toman.Management.KPIAnalysis.ApiService.GitLab;
using Toman.Management.KPIAnalysis.ApiService.Jobs;
using Toman.Management.KPIAnalysis.ApiService.Services;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire components.
builder.AddServiceDefaults();

// Add configurations
builder.Services.Configure<GitLabConfiguration>(
    builder.Configuration.GetSection(GitLabConfiguration.SectionName));
builder.Services.Configure<ProcessingConfiguration>(
    builder.Configuration.GetSection(ProcessingConfiguration.SectionName));
builder.Services.Configure<ExportsConfiguration>(
    builder.Configuration.GetSection(ExportsConfiguration.SectionName));

// Add database
builder.AddNpgsqlDbContext<GitLabMetricsDbContext>(Toman.Management.KPIAnalysis.Constants.Keys.PostgresDatabase);

// Add HTTP client with resilience for GitLab API
builder.Services.AddHttpClient<IGitLabApiService, GitLabApiService>()
.AddResilienceHandler("gitlab-retry", static resilienceBuilder => resilienceBuilder
    .AddRetry(new()
    {
        MaxRetryAttempts = 3,
        BackoffType = Polly.DelayBackoffType.Exponential,
        UseJitter = true
    }));

// Add services
builder.Services.AddScoped<IGitLabCollectorService, GitLabCollectorService>();
builder.Services.AddScoped<IMetricsProcessorService, MetricsProcessorService>();
builder.Services.AddScoped<IMetricsExportService, MetricsExportService>();

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

// Add services to the container.
builder.Services.AddProblemDetails();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

// Map endpoints
app.MapDefaultEndpoints();
app.MapHealthEndpoints();
app.MapCollectionEndpoints();
app.MapExportsEndpoints();
app.MapStatusEndpoints();

// Ensure database is created (in development)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<GitLabMetricsDbContext>();
    await context.Database.EnsureCreatedAsync();
}

app.Run();
