using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Data;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Operational;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics;

public static class GitLabMetricsEndpoints
{
    public static WebApplication MapGitlabCollectorEndpoints(this WebApplication app)
    {
        app.MapGitLabMetricsEndpoints()
           .MapStatusEndpoints()
           .MapUserMetricsEndpoints()
           .MapPerDeveloperMetricsEndpoints();
           
        app.MapDataQualityEndpoints();
           
        app.MapMetricsExportEndpoints();

        return app;
    }
    private static WebApplication MapGitLabMetricsEndpoints(this WebApplication endpoints)
    {
        var group = endpoints.MapGroup("/gitlab-metrics")
            .WithTags("GitLab Metrics (Legacy)")
            .WithOpenApi();

        group.MapPost("/collect/incremental", async (
            [FromServices] IGitLabCollectorService collectorService,
            [FromBody] StartCollectionRunRequest? request,
            CancellationToken cancellationToken) =>
        {
            request ??= new StartCollectionRunRequest { RunType = "incremental", TriggerSource = "api" };
            var result = await collectorService.StartCollectionRunAsync(request, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("RunIncrementalCollection")
        .WithSummary("Run incremental GitLab metrics collection with windowed support")
        .Produces<CollectionRunResponse>(200)
        .Produces(500);

        group.MapPost("/collect/backfill", async (
            [FromServices] IGitLabCollectorService collectorService,
            [FromBody] StartCollectionRunRequest? request,
            CancellationToken cancellationToken) =>
        {
            request ??= new StartCollectionRunRequest { RunType = "backfill", TriggerSource = "api" };
            var result = await collectorService.StartCollectionRunAsync(request, cancellationToken);
            return Results.Ok(result);
        })
        .WithName("RunBackfillCollection")
        .WithSummary("Run backfill GitLab metrics collection")
        .Produces<CollectionRunResponse>(200)
        .Produces(500);

        group.MapGet("/collect/runs/{runId:guid}", async (
            [FromServices] IGitLabCollectorService collectorService,
            [FromRoute] Guid runId,
            CancellationToken cancellationToken) =>
        {
            var result = await collectorService.GetCollectionRunStatusAsync(runId, cancellationToken);
            return result is not null ? Results.Ok(result) : Results.NotFound();
        })
        .WithName("GetCollectionRunStatus")
        .WithSummary("Get the status of a collection run")
        .Produces<CollectionRunResponse>(200)
        .Produces(404);

        group.MapGet("/collect/runs", async (
            [FromServices] IGitLabCollectorService collectorService,
            [FromQuery] string? runType,
            [FromQuery] int limit = 10,
            CancellationToken cancellationToken = default) =>
        {
            var runs = await collectorService.GetRecentCollectionRunsAsync(runType, limit, cancellationToken);
            return Results.Ok(runs);
        })
        .WithName("GetRecentCollectionRuns")
        .WithSummary("Get recent collection runs")
        .Produces<IReadOnlyList<CollectionRunResponse>>(200);

        // Legacy endpoints for backward compatibility
        group.MapPost("/collect/incremental/simple", async (
            [FromServices] IGitLabCollectorService collectorService,
            CancellationToken cancellationToken) =>
        {
            await collectorService.RunIncrementalCollectionAsync(cancellationToken);
            return Results.Ok(new { Message = "Incremental collection completed" });
        })
        .WithName("RunSimpleIncrementalCollection")
        .WithSummary("Run simple incremental GitLab metrics collection (legacy)")
        .Produces(200)
        .Produces(500);

        group.MapPost("/collect/backfill/simple", async (
            [FromServices] IGitLabCollectorService collectorService,
            CancellationToken cancellationToken) =>
        {
            await collectorService.RunBackfillCollectionAsync(cancellationToken);
            return Results.Ok(new { Message = "Backfill collection completed" });
        })
        .WithName("RunSimpleBackfillCollection")
        .WithSummary("Run simple backfill GitLab metrics collection (legacy)")
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
            [FromServices] IDataQualityService dataQualityService,
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

                // Get recent collection runs
                var recentRuns = await dbContext.CollectionRuns
                    .OrderByDescending(r => r.StartedAt)
                    .Take(5)
                    .Select(r => new
                    {
                        r.Id,
                        r.RunType,
                        r.Status,
                        r.StartedAt,
                        r.CompletedAt,
                        Duration = r.CompletedAt.HasValue ? (TimeSpan?)(r.CompletedAt.Value - r.StartedAt) : null,
                        r.ProjectsProcessed,
                        r.CommitsCollected,
                        r.MergeRequestsCollected,
                        r.PipelinesCollected,
                        r.ErrorMessage
                    })
                    .ToListAsync(cancellationToken);

                // Calculate coverage
                var totalProjects = await dbContext.Projects.CountAsync(cancellationToken);
                var activeProjects = await dbContext.Projects
                    .CountAsync(p => !p.Archived, cancellationToken);

                var coveragePercent = totalProjects > 0 ? activeProjects / (decimal)totalProjects * 100 : 0;

                // Calculate lag
                var lastUpdateTime = lastIncrementalRun?.LastRunAt ?? DateTimeOffset.MinValue;
                var lagMinutes = (DateTimeOffset.UtcNow - lastUpdateTime).TotalMinutes;

                // Get recent data counts
                var recentMRs = await dbContext.RawMergeRequests
                    .CountAsync(mr => mr.CreatedAt > DateTimeOffset.UtcNow.AddDays(-7), cancellationToken);

                var recentPipelines = await dbContext.RawPipelines
                    .CountAsync(p => p.CreatedAt > DateTimeOffset.UtcNow.AddDays(-7), cancellationToken);

                var recentCommits = await dbContext.RawCommits
                    .CountAsync(c => c.CommittedAt > DateTimeOffset.UtcNow.AddDays(-7), cancellationToken);

                // Get API error counts from recent runs
                var failedRuns = await dbContext.CollectionRuns
                    .CountAsync(r => r.Status == "failed" && r.StartedAt > DateTimeOffset.UtcNow.AddDays(-1), cancellationToken);

                // Perform latest data quality check
                var dataQualityReport = await dataQualityService.PerformDataQualityChecksAsync(cancellationToken: cancellationToken);

                return Results.Ok(new
                {
                    timestamp = DateTimeOffset.UtcNow,
                    lastRuns = new
                    {
                        incremental = lastIncrementalRun?.LastRunAt,
                        backfill = lastBackfillRun?.LastRunAt,
                        recent = recentRuns
                    },
                    coverage = new
                    {
                        totalProjects,
                        activeProjects,
                        coveragePercent = Math.Round(coveragePercent, 2)
                    },
                    lag = new
                    {
                        lagMinutes = Math.Round(lagMinutes, 2),
                        status = lagMinutes > 120 ? "stale" : "fresh"
                    },
                    recentActivity = new
                    {
                        mergeRequestsLast7Days = recentMRs,
                        pipelinesLast7Days = recentPipelines,
                        commitsLast7Days = recentCommits
                    },
                    apiErrors = new
                    {
                        failedRunsLast24h = failedRuns,
                        // Note: More detailed API error tracking would require instrumenting the GitLabService
                        rateLimitHits429 = 0, 
                        serverErrors5xx = 0   
                    },
                    dataQuality = new
                    {
                        overallStatus = dataQualityReport.OverallStatus,
                        overallScore = Math.Round(dataQualityReport.OverallScore, 3),
                        generatedAt = dataQualityReport.GeneratedAt,
                        checks = dataQualityReport.Checks.Select(c => new
                        {
                            checkType = c.CheckType,
                            status = c.Status,
                            score = c.Score.HasValue ? Math.Round(c.Score.Value, 3) : (double?)null,
                            description = c.Description
                        })
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
