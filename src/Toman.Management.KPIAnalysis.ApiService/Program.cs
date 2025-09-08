using Microsoft.EntityFrameworkCore;

using Toman.Management.KPIAnalysis.ApiService.Configuration;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Data;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Dimensions;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire components.
builder.AddServiceDefaults();

// Add OpenAPI services
builder.Services.AddOpenApi("internal");
builder.Services.AddEndpointsApiExplorer();

// Add services to the container.
builder.Services.AddProblemDetails();

builder.AddGitLabMetricsServices();

builder.Services.Configure<ProcessingConfiguration>(builder.Configuration.GetSection(ProcessingConfiguration.SectionName));
builder.Services.Configure<ExportsConfiguration>(builder.Configuration.GetSection(ExportsConfiguration.SectionName));

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

// Configure Swagger UI (only in development)
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi(); // /openapi/internal.json
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/internal.json", "internal"));
}

// Map endpoints
app.MapDefaultEndpoints();

app.MapGitlabCollectorEndpoints();

// Ensure database is created and seeded (in development)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<GitLabMetricsDbContext>();
    await context.Database.EnsureCreatedAsync();
}

app.Run();
