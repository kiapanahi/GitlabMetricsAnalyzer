using KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics;
using KuriousLabs.Management.KPIAnalysis.ApiService.Features.JiraMetrics;
using KuriousLabs.Management.KPIAnalysis.ApiService.Features.CrossSystemMetrics;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire components.
builder.AddServiceDefaults();

// Add OpenAPI services
builder.Services.AddOpenApi("internal");
builder.Services.AddEndpointsApiExplorer();

// Add services to the container.
builder.Services.AddProblemDetails();

builder.AddGitLabMetricsServices();
builder.AddJiraMetricsServices();
builder.Services.AddCrossSystemMetricsServices();

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

app.MapGitlabMetricsEndpoints();
app.MapJiraMetricsEndpoints();
app.MapCrossSystemMetricsEndpoints();

app.Run();

// Make Program class accessible for integration tests
public partial class Program { }
