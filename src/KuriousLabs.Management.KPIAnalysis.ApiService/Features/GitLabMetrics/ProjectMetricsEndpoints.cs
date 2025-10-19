using Microsoft.AspNetCore.Mvc;

using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics;

internal static class ProjectMetricsEndpoints
{
    internal static void MapProjectMetricsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/projects/{projectId:long}")
            .WithTags("Project-level aggregation metrics")
            .WithOpenApi();

        group.MapGet("/metrics", CalculateProjectMetrics)
            .WithName("CalculateProjectMetrics")
            .WithSummary("Calculate aggregated metrics for a project")
            .WithDescription("Calculates project-level metrics including activity score, branch lifecycle, label usage, milestone completion, and review coverage");
    }

    private static async Task<IResult> CalculateProjectMetrics(
        long projectId,
        [FromQuery] int? windowDays,
        IProjectMetricsService metricsService,
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

            var result = await metricsService.CalculateProjectMetricsAsync(
                projectId,
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
                title: "Error calculating project metrics",
                statusCode: 500);
        }
    }
}
