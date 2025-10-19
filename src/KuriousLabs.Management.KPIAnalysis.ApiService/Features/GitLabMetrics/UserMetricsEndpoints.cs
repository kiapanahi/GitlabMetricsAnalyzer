using Microsoft.AspNetCore.Mvc;

using KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

namespace KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics;

internal static class UserMetricsEndpoints
{
    internal static void MapUserMetricsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/{userId:long}")
            .WithTags("GitLab user analytics and metrics")
            .WithOpenApi();

        group.MapGet("/analysis/commit-time", AnalyzeUserCommitTimeDistribution)
            .WithName("AnalyzeUserCommitTimeDistribution")
            .WithSummary("Analyze commit time distribution for a user")
            .WithDescription("Fetches commits from GitLab for the past N days and analyzes the distribution across 24 hours of the day");

        group.MapGet("/metrics/mr-cycle-time", CalculateMrCycleTime)
            .WithName("CalculateMrCycleTime")
            .WithSummary("Calculate MR cycle time (P50) for a developer")
            .WithDescription("Fetches merge requests from GitLab for the past N days and calculates the median cycle time from first commit to merge");

        group.MapGet("/metrics/flow", CalculateFlowMetrics)
            .WithName("CalculateFlowMetrics")
            .WithSummary("Calculate flow and throughput metrics for a developer")
            .WithDescription("Calculates comprehensive flow metrics including merged MRs count, lines changed, coding time, review time, merge time, WIP MRs, and context switching index");

        group.MapGet("/metrics/collaboration", CalculateCollaborationMetrics)
            .WithName("CalculateCollaborationMetrics")
            .WithSummary("Calculate collaboration and review metrics for a developer")
            .WithDescription("Calculates collaboration metrics including review comments given/received, approvals, discussion threads, self-merged MRs, review turnaround time, and review depth");

        group.MapGet("/metrics/quality", CalculateQualityMetrics)
            .WithName("CalculateQualityMetrics")
            .WithSummary("Calculate quality and reliability metrics for a developer")
            .WithDescription("Calculates quality metrics including rework ratio, revert rate, CI success rate, pipeline duration, test coverage, hotfix rate, and merge conflict frequency");

        group.MapGet("/metrics/code-characteristics", CalculateCodeCharacteristics)
            .WithName("CalculateCodeCharacteristics")
            .WithSummary("Calculate code characteristics metrics for a developer")
            .WithDescription("Calculates code characteristics including commit frequency, commit size distribution, MR size distribution, file churn, squash merge rate, commit message quality, and branch naming compliance");

        group.MapGet("/metrics/advanced", CalculateAdvancedMetrics)
            .WithName("CalculateAdvancedMetrics")
            .WithSummary("Calculate advanced metrics for a developer")
            .WithDescription("Calculates 7 advanced metrics: Bus Factor, Response Time Distribution, Batch Size, Draft Duration, Iteration Count, Idle Time in Review, and Cross-Team Collaboration Index");
    }

    private static async Task<IResult> AnalyzeUserCommitTimeDistribution(
        long userId,
        [FromQuery] int? lookbackDays,
        ICommitTimeAnalysisService analysisService,
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

            var analysis = await analysisService.AnalyzeCommitTimeDistributionAsync(
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
                title: "Error analyzing commit time distribution",
                statusCode: 500);
        }
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

    private static async Task<IResult> CalculateFlowMetrics(
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

            var result = await metricsService.CalculateFlowMetricsAsync(
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
                title: "Error calculating flow metrics",
                statusCode: 500);
        }
    }

    private static async Task<IResult> CalculateCollaborationMetrics(
        long userId,
        [FromQuery] int? windowDays,
        ICollaborationMetricsService metricsService,
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

            var result = await metricsService.CalculateCollaborationMetricsAsync(
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
                title: "Error calculating collaboration metrics",
                statusCode: 500);
        }
    }

    private static async Task<IResult> CalculateQualityMetrics(
        long userId,
        [FromQuery] int? windowDays,
        [FromQuery] int? revertDetectionDays,
        IQualityMetricsService metricsService,
        CancellationToken cancellationToken)
    {
        try
        {
            var days = windowDays ?? 30;
            var revertDays = revertDetectionDays ?? 30;

            if (days <= 0)
            {
                return Results.BadRequest(new { Error = "windowDays must be greater than 0" });
            }

            if (days > 365)
            {
                return Results.BadRequest(new { Error = "windowDays cannot exceed 365 days" });
            }

            if (revertDays <= 0)
            {
                return Results.BadRequest(new { Error = "revertDetectionDays must be greater than 0" });
            }

            if (revertDays > 90)
            {
                return Results.BadRequest(new { Error = "revertDetectionDays cannot exceed 90 days" });
            }

            var result = await metricsService.CalculateQualityMetricsAsync(
                userId,
                days,
                revertDays,
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
                title: "Error calculating quality metrics",
                statusCode: 500);
        }
    }

    private static async Task<IResult> CalculateCodeCharacteristics(
        long userId,
        [FromQuery] int? windowDays,
        ICodeCharacteristicsService metricsService,
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

            var result = await metricsService.CalculateCodeCharacteristicsAsync(
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
                title: "Error calculating code characteristics metrics",
                statusCode: 500);
        }
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
