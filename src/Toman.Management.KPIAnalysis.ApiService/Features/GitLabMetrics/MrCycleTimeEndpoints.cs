using Microsoft.AspNetCore.Mvc;

using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics;

internal static class MrCycleTimeEndpoints
{
    internal static void MapMrCycleTimeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/metrics/mr-cycle-time")
            .WithTags("MR Cycle Time Metrics")
            .WithOpenApi();

        group.MapGet("/{userId:long}", CalculateMrCycleTime)
            .WithName("CalculateMrCycleTime")
            .WithSummary("Calculate MR cycle time (P50) for a developer")
            .WithDescription("Fetches merge requests from GitLab for the past N days and calculates the median cycle time from first commit to merge");
    }

    private static async Task<IResult> CalculateMrCycleTime(
        long userId,
        [FromQuery] int? windowDays,
        IPerDeveloperMetricsService metricsService,
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

            var result = await metricsService.CalculateMrCycleTimeAsync(
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
                title: "Error calculating MR cycle time",
                statusCode: 500);
        }
    }
}
