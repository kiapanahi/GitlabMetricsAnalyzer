using Microsoft.AspNetCore.Mvc;

using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Operational;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics;

/// <summary>
/// API endpoints for GitLab data collection operations
/// </summary>
internal static class GitLabCollectionEndpoints
{
    internal static void MapGitLabCollectionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/gitlab-metrics/collect")
            .WithTags("GitLab Collection")
            .WithOpenApi();

        group.MapPost("/backfill", TriggerBackfillCollection)
            .WithName("TriggerBackfillCollection")
            .WithSummary("Trigger backfill data collection")
            .WithDescription("Starts a backfill collection run to fetch historical data from GitLab for a specified date range");

        group.MapGet("/runs/{runId:guid}", GetCollectionRunStatus)
            .WithName("GetCollectionRunStatus")
            .WithSummary("Get collection run status")
            .WithDescription("Gets the status and details of a specific collection run");

        group.MapGet("/runs", GetRecentCollectionRuns)
            .WithName("GetRecentCollectionRuns")
            .WithSummary("Get recent collection runs")
            .WithDescription("Gets a list of recent collection runs with optional filtering");

        group.MapPost("/reset", ResetRawData)
            .WithName("ResetRawData")
            .WithSummary("Reset all raw data")
            .WithDescription("Clears all raw data tables and resets ingestion state for complete re-seeding");
    }

    private static async Task<IResult> TriggerBackfillCollection(
        [FromBody] BackfillCollectionRequest? request,
        IGitLabCollectorService collectorService,
        CancellationToken cancellationToken)
    {
        try
        {
            var collectionRequest = new StartCollectionRunRequest
            {
                RunType = "backfill",
                TriggerSource = request?.TriggerSource ?? "api",
                BackfillStartDate = request?.BackfillStartDate,
                BackfillEndDate = request?.BackfillEndDate
            };

            var response = await collectorService.StartCollectionRunAsync(collectionRequest, cancellationToken);
            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Backfill Failed",
                detail: ex.Message,
                statusCode: 500
            );
        }
    }

    private static async Task<IResult> GetCollectionRunStatus(
        [FromRoute] Guid runId,
        IGitLabCollectorService collectorService,
        CancellationToken cancellationToken)
    {
        try
        {
            var run = await collectorService.GetCollectionRunStatusAsync(runId, cancellationToken);

            if (run is null)
            {
                return Results.NotFound($"Collection run {runId} not found");
            }

            return Results.Ok(run);
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Failed to Get Run Status",
                detail: ex.Message,
                statusCode: 500
            );
        }
    }

    private static async Task<IResult> GetRecentCollectionRuns(
        IGitLabCollectorService collectorService,
        CancellationToken cancellationToken,
        [FromQuery] string? runType = null,
        [FromQuery] int limit = 10)
    {
        try
        {
            var runs = await collectorService.GetRecentCollectionRunsAsync(runType, limit, cancellationToken);
            return Results.Ok(runs);
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Failed to Get Recent Runs",
                detail: ex.Message,
                statusCode: 500
            );
        }
    }

    private static async Task<IResult> ResetRawData(
        [FromBody] ResetDataRequest request,
        IDataResetService dataResetService,
        ILogger<DataResetService> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogWarning("Initiating complete raw data reset. Trigger source: {TriggerSource}",
                request?.TriggerSource ?? "api");

            // Clear all raw data tables
            await dataResetService.ClearAllRawDataAsync(cancellationToken);

            // Reset ingestion states
            await dataResetService.ResetIngestionStateAsync(cancellationToken);

            logger.LogInformation("Raw data reset completed successfully");

            return Results.Ok(new
            {
                Message = "Raw data has been successfully reset. You can now trigger a backfill collection to re-seed the data.",
                ResetAt = DateTime.UtcNow,
                TriggerSource = request?.TriggerSource ?? "api"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to reset raw data");
            return Results.Problem(
                title: "Reset Failed",
                detail: ex.Message,
                statusCode: 500
            );
        }
    }
}

/// <summary>
/// Request model for triggering collection operations
/// </summary>
public sealed class TriggerCollectionRequest
{
    /// <summary>
    /// Source that triggered this collection (e.g., "manual", "api", "scheduled")
    /// </summary>
    public string? TriggerSource { get; init; }
}

/// <summary>
/// Request model for backfill collection operations
/// </summary>
public sealed class BackfillCollectionRequest
{
    /// <summary>
    /// Source that triggered this collection (e.g., "manual", "api", "scheduled")
    /// </summary>
    public string? TriggerSource { get; init; }

    /// <summary>
    /// Start date for backfill collection (optional, defaults to beginning of time)
    /// </summary>
    public DateTime? BackfillStartDate { get; init; }

    /// <summary>
    /// End date for backfill collection (optional, defaults to now)
    /// </summary>
    public DateTime? BackfillEndDate { get; init; }
}

/// <summary>
/// Request model for data reset operations
/// </summary>
public sealed class ResetDataRequest
{
    /// <summary>
    /// Source that triggered this reset (e.g., "manual", "api", "maintenance")
    /// </summary>
    public string? TriggerSource { get; init; }

    /// <summary>
    /// Confirmation flag to prevent accidental resets
    /// </summary>
    public bool ConfirmReset { get; init; } = false;
}
