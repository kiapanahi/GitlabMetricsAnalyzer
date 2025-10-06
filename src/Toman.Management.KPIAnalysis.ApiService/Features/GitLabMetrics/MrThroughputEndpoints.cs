using Microsoft.AspNetCore.Mvc;

using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics;

internal static class MrThroughputEndpoints
{
    internal static void MapMrThroughputEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/metrics/mr-throughput")
            .WithTags("MR Throughput")
            .WithOpenApi();

        group.MapGet("/{userId:long}", CalculateMrThroughput)
            .WithName("CalculateMrThroughput")
            .WithSummary("Calculate MR throughput for a developer")
            .WithDescription("Fetches merge requests from GitLab for the specified time window and calculates throughput as MRs merged per week");
    }

    private static async Task<IResult> CalculateMrThroughput(
        long userId,
        [FromQuery] int? windowDays,
        IPerDeveloperMetricsService metricsService,
        CancellationToken cancellationToken)
    {
        try
        {
            var days = windowDays ?? 7;

            if (days <= 0)
            {
                return Results.BadRequest(new { Error = "windowDays must be greater than 0" });
            }

            if (days > 365)
            {
                return Results.BadRequest(new { Error = "windowDays cannot exceed 365 days" });
            }

            var result = await metricsService.CalculateMrThroughputAsync(
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
                title: "Error calculating MR throughput",
                statusCode: 500);
        }
    }
}
