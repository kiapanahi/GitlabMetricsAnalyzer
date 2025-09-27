using Microsoft.AspNetCore.Mvc;

using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics;

/// <summary>
/// Endpoints for data quality monitoring
/// </summary>
public static class DataQualityEndpoints
{
    public static WebApplication MapDataQualityEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/data-quality")
            .WithTags("Data Quality")
            .WithDescription("Data quality monitoring and reporting endpoints");

        group.MapGet("/", GetDataQualityReport)
            .WithName("GetDataQualityReport")
            .WithSummary("Get comprehensive data quality report")
            .WithDescription("Returns a comprehensive report of all data quality checks including referential integrity, completeness, and latency");

        group.MapGet("/health", GetDataQualityHealth)
            .WithName("GetDataQualityHealth")
            .WithSummary("Get data quality health status")
            .WithDescription("Returns a simple health status for monitoring systems");

        return app;
    }

    private static async Task<IResult> GetDataQualityReport(
        [FromServices] IDataQualityService dataQualityService,
        [FromQuery] Guid? runId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var report = await dataQualityService.PerformDataQualityChecksAsync(runId, cancellationToken);

            return Results.Ok(new
            {
                timestamp = report.GeneratedAt,
                runId = report.RunId,
                overallStatus = report.OverallStatus,
                overallScore = Math.Round(report.OverallScore, 3),
                isHealthy = report.IsHealthy,
                checks = report.Checks.Select(c => new
                {
                    checkType = c.CheckType,
                    status = c.Status,
                    score = c.Score.HasValue ? Math.Round(c.Score.Value, 3) : (double?)null,
                    description = c.Description,
                    details = c.Details,
                    checkedAt = c.CheckedAt
                }),
                summary = new
                {
                    totalChecks = report.Checks.Count,
                    passedChecks = report.GetChecksByStatus("passed").Count,
                    warningChecks = report.GetChecksByStatus("warning").Count,
                    failedChecks = report.GetChecksByStatus("failed").Count
                }
            });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                statusCode: 500,
                title: "Failed to generate data quality report",
                detail: ex.Message
            );
        }
    }

    private static async Task<IResult> GetDataQualityHealth(
        [FromServices] IDataQualityService dataQualityService,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var report = await dataQualityService.PerformDataQualityChecksAsync(cancellationToken: cancellationToken);

            var healthResponse = new
            {
                status = report.OverallStatus,
                healthy = report.IsHealthy,
                score = Math.Round(report.OverallScore, 2),
                timestamp = report.GeneratedAt,
                issues = report.Checks
                    .Where(c => c.Status != "passed")
                    .Select(c => new { type = c.CheckType, status = c.Status, description = c.Description })
                    .ToList()
            };

            // Return appropriate HTTP status based on health
            return report.OverallStatus switch
            {
                "healthy" => Results.Ok(healthResponse),
                "warning" => Results.Ok(healthResponse), // 200 but with warnings
                "critical" => Results.Json(healthResponse, statusCode: 503), // Service Unavailable
                _ => Results.Json(healthResponse, statusCode: 500)
            };
        }
        catch (Exception ex)
        {
            return Results.Problem(
                statusCode: 500,
                title: "Failed to check data quality health",
                detail: ex.Message
            );
        }
    }
}