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

        group.MapGet("/deployment-frequency/{userId:long}", GetDeploymentFrequency)
            .WithName("GetDeploymentFrequency")
            .WithSummary("Calculate deployment frequency for a developer")
            .WithDescription("Fetches live data from GitLab and calculates the number of successful production deployments per week for a specific developer");
    }

    private static async Task<IResult> GetDeploymentFrequency(
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

            var analysis = await metricsService.CalculateDeploymentFrequencyAsync(
                userId,
                days,
                cancellationToken);

            return Results.Ok(analysis);
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
                title: "Error calculating deployment frequency",
                statusCode: 500);
        }
    }
}
