using Microsoft.AspNetCore.Mvc;

using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics;

internal static class AdvancedMetricsEndpoints
{
    internal static void MapAdvancedMetricsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/{userId:long}")
            .WithTags("Advanced Metrics")
            .WithOpenApi();

        group.MapGet("/metrics/advanced", CalculateAdvancedMetrics)
            .WithName("CalculateAdvancedMetrics")
            .WithSummary("Calculate advanced metrics for a developer")
            .WithDescription("Calculates 7 advanced metrics: Bus Factor, Response Time Distribution, Batch Size, Draft Duration, Iteration Count, Idle Time in Review, and Cross-Team Collaboration Index");
    }

    private static async Task<IResult> CalculateAdvancedMetrics(
        long userId,
        [FromQuery] int? windowDays,
        IAdvancedMetricsService metricsService,
        CancellationToken cancellationToken)
    {
        try
        {
            var days = windowDays ?? 30;

            if (days <= 0)
            {
                return Results.BadRequest(new { Error = "windowDays must be greater than 0" });
            }

            if (days > 365)
            {
                return Results.BadRequest(new { Error = "windowDays cannot exceed 365 days" });
            }

            var result = await metricsService.CalculateAdvancedMetricsAsync(
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
                title: "Error calculating advanced metrics",
                statusCode: 500);
        }
    }
}
