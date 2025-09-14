using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Data;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Export;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics;

public static class GitLabMetricsEndpoints
{
    public static WebApplication MapGitlabCollectorEndpoints(this WebApplication app)
    {
        app.MapGitLabMetricsEndpoints()
           .MapHealthEndpoints()
           .MapStatusEndpoints();

        return app;
    }
    private static WebApplication MapGitLabMetricsEndpoints(this WebApplication endpoints)
    {
        var group = endpoints.MapGroup("/gitlab-metrics")
            .WithTags("GitLab Metrics");

        group.MapPost("/collect/incremental", async (
            [FromServices] IGitLabCollectorService collectorService,
            CancellationToken cancellationToken) =>
        {
            await collectorService.RunIncrementalCollectionAsync(cancellationToken);
            return Results.Ok(new { Message = "Incremental collection completed" });
        })
        .WithName("RunIncrementalCollection")
        .WithSummary("Run incremental GitLab metrics collection")
        .Produces(200)
        .Produces(500);

        group.MapPost("/collect/backfill", async (
            [FromServices] IGitLabCollectorService collectorService,
            [FromQuery] int days,
            CancellationToken cancellationToken) =>
        {
            await collectorService.RunBackfillCollectionAsync(cancellationToken);
            return Results.Ok(new { Message = $"Backfill collection for {days} days completed" });
        })
        .WithName("RunBackfillCollection")
        .WithSummary("Run backfill GitLab metrics collection")
        .Produces(200)
        .Produces(500);

        return endpoints;
    }

    private static WebApplication MapHealthEndpoints(this WebApplication app)
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

                return Results.Ok(new
                {
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

        return app;
    }

    private static WebApplication MapStatusEndpoints(this WebApplication app)
    {
        app.MapGet("/status", async (
            [FromServices] GitLabMetricsDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            try
            {
                // Get last run stats
                var lastIncrementalRun = await dbContext.IngestionStates
                    .Where(s => s.Entity == "incremental")
                    .FirstOrDefaultAsync(cancellationToken);

                var lastBackfillRun = await dbContext.IngestionStates
                    .Where(s => s.Entity == "backfill")
                    .FirstOrDefaultAsync(cancellationToken);

                // Calculate coverage
                //var totalProjects = await dbContext.DimProjects.CountAsync(cancellationToken);
                //var activeProjects = await dbContext.DimProjects
                //    .CountAsync(p => !p.archived, cancellationToken);

                //var coveragePercent = totalProjects > 0 ? activeProjects / (decimal)totalProjects * 100 : 0;

                // Calculate lag
                var lastUpdateTime = lastIncrementalRun?.LastRunAt ?? DateTimeOffset.MinValue;
                var lagMinutes = (DateTimeOffset.UtcNow - lastUpdateTime).TotalMinutes;

                // Get recent data counts
                var recentMRs = await dbContext.RawMergeRequests
                    .CountAsync(mr => mr.CreatedAt > DateTimeOffset.UtcNow.AddDays(-7), cancellationToken);

                var recentPipelines = await dbContext.RawPipelines
                    .CountAsync(p => p.CreatedAt > DateTimeOffset.UtcNow.AddDays(-7), cancellationToken);

                return Results.Ok(new
                {
                    timestamp = DateTimeOffset.UtcNow,
                    lastRuns = new
                    {
                        incremental = lastIncrementalRun?.LastRunAt,
                        backfill = lastBackfillRun?.LastRunAt
                    },
                    //coverage = new
                    //{
                    //    totalProjects,
                    //    activeProjects,
                    //    coveragePercent = Math.Round(coveragePercent, 2)
                    //},
                    lag = new
                    {
                        lagMinutes = Math.Round(lagMinutes, 2),
                        status = lagMinutes > 120 ? "stale" : "fresh"
                    },
                    recentActivity = new
                    {
                        mergeRequestsLast7Days = recentMRs,
                        pipelinesLast7Days = recentPipelines
                    },
                    apiErrors = new
                    {
                        rateLimitHits429 = 0, // Would need to implement tracking
                        serverErrors5xx = 0   // Would need to implement tracking
                    }
                });
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    statusCode: 500,
                    title: "Failed to get status",
                    detail: ex.Message
                );
            }
        })
        .WithName("GetStatus")
        .WithTags("Status");

        return app;
    }
}
