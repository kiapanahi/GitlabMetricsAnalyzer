using Microsoft.AspNetCore.Mvc;

using KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

namespace KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics;

internal static class PipelineMetricsEndpoints
{
    internal static void MapPipelineMetricsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/metrics/pipelines")
            .WithTags("Pipeline & CI/CD Metrics")
            .WithOpenApi();

        group.MapGet("/{projectId:long}", CalculatePipelineMetrics)
            .WithName("CalculatePipelineMetrics")
            .WithSummary("Calculate comprehensive CI/CD pipeline metrics for a project")
            .WithDescription("Calculates 7 pipeline metrics: Failed Job Rate, Pipeline Retry Rate, Pipeline Wait Time, Deployment Frequency, Job Duration Trends, Pipeline Success Rate by Branch Type, and Coverage Trend");
    }

    private static async Task<IResult> CalculatePipelineMetrics(
        long projectId,
        [FromQuery] int? windowDays,
        IPipelineMetricsService metricsService,
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

            var result = await metricsService.CalculatePipelineMetricsAsync(
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
                title: "Error calculating pipeline metrics",
                statusCode: 500);
        }
    }
}
