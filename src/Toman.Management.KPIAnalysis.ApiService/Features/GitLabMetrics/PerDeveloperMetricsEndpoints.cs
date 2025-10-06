using Microsoft.AspNetCore.Mvc;

using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics;

internal static class PerDeveloperMetricsEndpoints
{
    internal static void MapPerDeveloperMetricsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/metrics/per-developer")
            .WithTags("Per-Developer Metrics")
            .WithOpenApi();

        group.MapGet("/{userId:long}/pipeline-success-rate", GetPipelineSuccessRate)
            .WithName("GetPipelineSuccessRate")
            .WithSummary("Get pipeline success rate for a developer")
            .WithDescription("Fetches pipeline data from GitLab for the past N days and calculates the success rate as a ratio (0.0-1.0)");
    }

    private static async Task<IResult> GetPipelineSuccessRate(
        long userId,
        [FromQuery] int? lookbackDays,
        IPerDeveloperMetricsService metricsService,
        CancellationToken cancellationToken)
    {
        try
        {
            var days = lookbackDays ?? 30;

            if (days <= 0)
            {
                return Results.BadRequest(new { Error = "lookbackDays must be greater than 0" });
            }

            if (days > 365)
            {
                return Results.BadRequest(new { Error = "lookbackDays cannot exceed 365 days" });
            }

            var result = await metricsService.GetPipelineSuccessRateAsync(
                userId,
                days,
                cancellationToken);

            return Results.Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Results.NotFound(new { Error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { Error = ex.Message });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail: ex.Message,
                title: "Error calculating pipeline success rate",
                statusCode: 500);
        }
    }
}
