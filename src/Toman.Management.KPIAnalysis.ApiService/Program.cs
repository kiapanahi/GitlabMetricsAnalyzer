using Toman.Management.KPIAnalysis.ApiService.Configuration;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire components.
builder.AddServiceDefaults();

// Add OpenAPI services
builder.Services.AddOpenApi("internal");
builder.Services.AddEndpointsApiExplorer();

// Add services to the container.
builder.Services.AddProblemDetails();

builder.AddGitLabMetricsServices();

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

app.MapGitlabMetricsEndpoints();

app.Run();

// Make Program class accessible for integration tests
public partial class Program { }
