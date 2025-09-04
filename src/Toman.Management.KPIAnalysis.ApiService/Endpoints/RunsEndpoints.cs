using Microsoft.AspNetCore.Mvc;
using Toman.Management.KPIAnalysis.ApiService.Services;

namespace Toman.Management.KPIAnalysis.ApiService.Endpoints;

public static class RunsEndpoints
{
    public static void MapRunsEndpoints(this WebApplication app)
    {
        app.MapPost("/runs/backfill", async (
            [FromQuery] int days,
            [FromServices] IGitLabCollectorService collectorService,
            [FromServices] IMetricsProcessorService processorService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                await collectorService.RunBackfillCollectionAsync(days, cancellationToken);
                await processorService.ProcessFactsAsync(cancellationToken);
                
                return Results.Ok(new { 
                    message = "Backfill completed successfully", 
                    days,
                    timestamp = DateTimeOffset.UtcNow 
                });
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    statusCode: 500,
                    title: "Backfill failed",
                    detail: ex.Message
                );
            }
        })
        .WithName("RunBackfill")
        .WithTags("Runs");

        app.MapPost("/runs/incremental", async (
            [FromServices] IGitLabCollectorService collectorService,
            [FromServices] IMetricsProcessorService processorService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                await collectorService.RunIncrementalCollectionAsync(cancellationToken);
                await processorService.ProcessFactsAsync(cancellationToken);
                
                return Results.Ok(new { 
                    message = "Incremental run completed successfully", 
                    timestamp = DateTimeOffset.UtcNow 
                });
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    statusCode: 500,
                    title: "Incremental run failed",
                    detail: ex.Message
                );
            }
        })
        .WithName("RunIncremental")
        .WithTags("Runs");
    }
}
