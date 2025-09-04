using Microsoft.AspNetCore.Mvc;

using Toman.Management.KPIAnalysis.ApiService.Data;

namespace Toman.Management.KPIAnalysis.ApiService.Features.Health;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/healthz", () => Results.Ok(new { status = "healthy", timestamp = DateTimeOffset.UtcNow }))
            .WithName("Health")
            .WithTags("Health");

        app.MapGet("/readyz", async ([FromServices] GitLabMetricsDbContext dbContext) =>
        {
            try
            {
                // Check database connectivity
                await dbContext.Database.CanConnectAsync();
                
                return Results.Ok(new { 
                    status = "ready", 
                    timestamp = DateTimeOffset.UtcNow,
                    database = "connected"
                });
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    statusCode: 503,
                    title: "Service Unavailable",
                    detail: ex.Message
                );
            }
        })
        .WithName("Readiness")
        .WithTags("Health");
    }
}
