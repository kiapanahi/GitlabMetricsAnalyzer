using Toman.Management.KPIAnalysis.ApiService.Configuration;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Data;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire components.
builder.AddServiceDefaults();

builder.Services.AddOpenApi("internal");

// Add services to the container.
builder.Services.AddProblemDetails();

// Add Swagger/OpenAPI services
builder.Services.AddEndpointsApiExplorer();

// builder.Services.AddSwaggerGen(c =>
// {
//     c.SwaggerDoc("internal", new()
//     {
//         Title = "Toman KPI Analysis API",
//         Version = "v1",
//         Description = "API for analyzing engineering KPIs and metrics from GitLab",
//         Contact = new()
//         {
//             Name = "Kia Raad",
//             Email = "k.raad@toman.ir"
//         },
//         License = new()
//         {
//             Name = "MIT",
//             Url = new("https://opensource.org/license/mit/")
//         }
//     });
// });

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

// Ensure database is created (in development)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<GitLabMetricsDbContext>();
    await context.Database.EnsureCreatedAsync();
}

app.Run();
