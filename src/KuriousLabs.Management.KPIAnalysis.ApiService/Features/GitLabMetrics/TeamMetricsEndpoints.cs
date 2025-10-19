using Microsoft.AspNetCore.Mvc;

using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics;

internal static class TeamMetricsEndpoints
{
    internal static void MapTeamMetricsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/teams/{teamId}")
            .WithTags("Team-level aggregation metrics")
            .WithOpenApi();

        group.MapGet("/metrics", CalculateTeamMetrics)
            .WithName("CalculateTeamMetrics")
            .WithSummary("Calculate aggregated metrics for a team")
            .WithDescription("Calculates team-level metrics including velocity, cross-project contributions, and review coverage");
    }

    private static async Task<IResult> CalculateTeamMetrics(
        string teamId,
        [FromQuery] int? windowDays,
        ITeamMetricsService metricsService,
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

            var result = await metricsService.CalculateTeamMetricsAsync(
                teamId,
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
                title: "Error calculating team metrics",
                statusCode: 500);
        }
    }
}
