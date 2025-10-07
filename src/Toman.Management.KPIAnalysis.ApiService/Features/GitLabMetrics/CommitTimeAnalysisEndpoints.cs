using Microsoft.AspNetCore.Mvc;

using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics;

internal static class CommitTimeAnalysisEndpoints
{
    internal static void MapCommitTimeAnalysisEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/{userId:long}/analysis/commit-time")
            .WithTags("Commit Time Analysis")
            .WithOpenApi();

        group.MapGet("", AnalyzeUserCommitTimeDistribution)
            .WithName("AnalyzeUserCommitTimeDistribution")
            .WithSummary("Analyze commit time distribution for a user")
            .WithDescription("Fetches commits from GitLab for the past N days and analyzes the distribution across 24 hours of the day");
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
}
