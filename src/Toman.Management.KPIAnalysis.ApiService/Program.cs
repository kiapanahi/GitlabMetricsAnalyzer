using Quartz;

using Toman.Management.KPIAnalysis.ApiService.Configuration;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Data;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire components.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

builder.AddGitLabMetricsServices();

builder.Services.Configure<ProcessingConfiguration>(builder.Configuration.GetSection(ProcessingConfiguration.SectionName));
builder.Services.Configure<ExportsConfiguration>(builder.Configuration.GetSection(ExportsConfiguration.SectionName));

builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

// Map endpoints
app.MapDefaultEndpoints();

app.MapGitlabCollectorEndpoints();

// Ensure database is created (in development)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<GitLabMetricsDbContext>();
    await context.Database.EnsureCreatedAsync();
}

app.Run();
