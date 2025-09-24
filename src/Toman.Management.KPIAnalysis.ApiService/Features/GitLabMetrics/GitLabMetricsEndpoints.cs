using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Data;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Export;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics;

public static class GitLabMetricsEndpoints
{
    public static WebApplication MapGitlabCollectorEndpoints(this WebApplication app)
    {
        app.MapGitLabMetricsEndpoints()
           .MapStatusEndpoints()
           .MapUserMetricsEndpoints()
           .MapUserMetricsCollectionEndpoints();

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
            CancellationToken cancellationToken) =>
        {
            await collectorService.RunBackfillCollectionAsync(cancellationToken);
            return Results.Ok(new { Message = "Backfill collection completed" });
        })
        .WithName("RunBackfillCollection")
        .WithSummary("Run backfill GitLab metrics collection")
        .Produces(200)
        .Produces(500);

        group.MapGet("/metrics/developer/{userId}", async (
            [FromServices] IMetricsCalculationService metricsService,
            [FromRoute] int userId,
            [FromQuery] string? fromDate = null,
            [FromQuery] string? toDate = null,
            CancellationToken cancellationToken = default) =>
        {
            var from = string.IsNullOrEmpty(fromDate)
                ? DateTimeOffset.UtcNow.AddDays(-30)
                : DateTimeOffset.Parse(fromDate);
            var to = string.IsNullOrEmpty(toDate)
                ? DateTimeOffset.UtcNow
                : DateTimeOffset.Parse(toDate);

            var metrics = await metricsService.CalculateDeveloperMetricsAsync(userId, from, to, cancellationToken);
            return Results.Ok(metrics);
        })
        .WithName("GetDeveloperMetrics")
        .WithSummary("Get developer productivity metrics")
        .Produces(200);

        group.MapGet("/metrics/project/{projectId}", async (
            [FromServices] IMetricsCalculationService metricsService,
            [FromRoute] int projectId,
            [FromQuery] string? fromDate = null,
            [FromQuery] string? toDate = null,
            CancellationToken cancellationToken = default) =>
        {
            var from = string.IsNullOrEmpty(fromDate)
                ? DateTimeOffset.UtcNow.AddDays(-30)
                : DateTimeOffset.Parse(fromDate);
            var to = string.IsNullOrEmpty(toDate)
                ? DateTimeOffset.UtcNow
                : DateTimeOffset.Parse(toDate);

            var metrics = await metricsService.CalculateProjectMetricsAsync(projectId, from, to, cancellationToken);
            return Results.Ok(metrics);
        })
        .WithName("GetProjectMetrics")
        .WithSummary("Get project metrics")
        .Produces(200);

        group.MapPost("/metrics/process-daily", async (
            [FromServices] IMetricsCalculationService metricsService,
            [FromQuery] string? targetDate = null,
            CancellationToken cancellationToken = default) =>
        {
            var date = string.IsNullOrEmpty(targetDate)
                ? DateTimeOffset.UtcNow.AddDays(-1)
                : DateTimeOffset.Parse(targetDate);

            await metricsService.ProcessDailyMetricsAsync(date, cancellationToken);
            return Results.Ok(new
            {
                message = "Daily metrics processing completed",
                targetDate = date.Date,
                timestamp = DateTimeOffset.UtcNow
            });
        })
        .WithName("ProcessDailyMetrics")
        .WithSummary("Process daily metrics for a specific date")
        .Produces(200);

        return endpoints;
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
