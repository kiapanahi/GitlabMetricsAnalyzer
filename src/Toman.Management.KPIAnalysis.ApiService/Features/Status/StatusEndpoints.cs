using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Toman.Management.KPIAnalysis.ApiService.Data;

namespace Toman.Management.KPIAnalysis.ApiService.Features.Status;

public static class StatusEndpoints
{
    public static void MapStatusEndpoints(this WebApplication app)
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
                var totalProjects = await dbContext.DimProjects.CountAsync(cancellationToken);
                var activeProjects = await dbContext.DimProjects
                    .CountAsync(p => p.ActiveFlag, cancellationToken);

                var coveragePercent = totalProjects > 0 ? activeProjects / (decimal)totalProjects * 100 : 0;

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
    }
}
