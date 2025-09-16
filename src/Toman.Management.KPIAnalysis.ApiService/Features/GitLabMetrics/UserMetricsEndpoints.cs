using Microsoft.AspNetCore.Mvc;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics;

public static class UserMetricsEndpoints
{
    public static WebApplication MapUserMetricsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/users")
            .WithTags("User Metrics")
            .WithOpenApi();

        group.MapGet("/{userId}/metrics", GetUserMetrics)
            .WithName("GetUserMetrics")
            .WithSummary("Get comprehensive metrics for a specific user")
            .WithDescription("Returns detailed productivity, collaboration, and quality metrics for the specified user within the given date range.")
            .Produces<UserMetricsResponse>(200)
            .Produces(400)
            .Produces(404)
            .Produces(500);

        group.MapGet("/{userId}/metrics/summary", GetUserMetricsSummary)
            .WithName("GetUserMetricsSummary")
            .WithSummary("Get a summary of key metrics for a user")
            .WithDescription("Returns a high-level overview of the most important productivity indicators for the specified user.")
            .Produces<UserMetricsSummaryResponse>(200)
            .Produces(400)
            .Produces(404)
            .Produces(500);

        group.MapGet("/{userId}/metrics/trends", GetUserMetricsTrends)
            .WithName("GetUserMetricsTrends")
            .WithSummary("Get trend data for user metrics over time")
            .WithDescription("Returns time-series data showing how the user's metrics have changed over the specified period.")
            .Produces<UserMetricsTrendsResponse>(200)
            .Produces(400)
            .Produces(404)
            .Produces(500);

        group.MapGet("/{userId}/metrics/comparison", GetUserMetricsComparison)
            .WithName("GetUserMetricsComparison")
            .WithSummary("Compare user metrics with team/organization averages")
            .WithDescription("Returns comparative analysis showing how the user's performance compares to their peers and team averages.")
            .Produces<UserMetricsComparisonResponse>(200)
            .Produces(400)
            .Produces(404)
            .Produces(500);

        return app;
    }

    private static async Task<IResult> GetUserMetrics(
        [FromServices] IUserMetricsService userMetricsService,
        [FromRoute] long userId,
        [FromQuery] string? fromDate = null,
        [FromQuery] string? toDate = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var from = ParseDateOrDefault(fromDate, DateTimeOffset.UtcNow.AddDays(-30));
            var to = ParseDateOrDefault(toDate, DateTimeOffset.UtcNow);

            var metrics = await userMetricsService.GetUserMetricsAsync(userId, from, to, cancellationToken);
            return Results.Ok(metrics);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { Error = ex.Message });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Internal Server Error",
                detail: ex.Message,
                statusCode: 500
            );
        }
    }

    private static async Task<IResult> GetUserMetricsSummary(
        [FromServices] IUserMetricsService userMetricsService,
        [FromRoute] long userId,
        [FromQuery] string? fromDate = null,
        [FromQuery] string? toDate = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var from = ParseDateOrDefault(fromDate, DateTimeOffset.UtcNow.AddDays(-30));
            var to = ParseDateOrDefault(toDate, DateTimeOffset.UtcNow);

            var summary = await userMetricsService.GetUserMetricsSummaryAsync(userId, from, to, cancellationToken);
            return Results.Ok(summary);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { Error = ex.Message });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Internal Server Error",
                detail: ex.Message,
                statusCode: 500
            );
        }
    }

    private static async Task<IResult> GetUserMetricsTrends(
        [FromServices] IUserMetricsService userMetricsService,
        [FromRoute] long userId,
        [FromQuery] string? fromDate = null,
        [FromQuery] string? toDate = null,
        [FromQuery] string? period = "Weekly",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var from = ParseDateOrDefault(fromDate, DateTimeOffset.UtcNow.AddDays(-90));
            var to = ParseDateOrDefault(toDate, DateTimeOffset.UtcNow);
            
            if (!Enum.TryParse<TrendPeriod>(period, true, out var trendPeriod))
            {
                return Results.BadRequest(new { Error = "Invalid period. Valid values are: Daily, Weekly, Monthly" });
            }

            var trends = await userMetricsService.GetUserMetricsTrendsAsync(userId, from, to, trendPeriod, cancellationToken);
            return Results.Ok(trends);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { Error = ex.Message });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Internal Server Error",
                detail: ex.Message,
                statusCode: 500
            );
        }
    }

    private static async Task<IResult> GetUserMetricsComparison(
        [FromServices] IUserMetricsService userMetricsService,
        [FromRoute] long userId,
        [FromQuery] string? fromDate = null,
        [FromQuery] string? toDate = null,
        [FromQuery] string? compareWith = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var from = ParseDateOrDefault(fromDate, DateTimeOffset.UtcNow.AddDays(-30));
            var to = ParseDateOrDefault(toDate, DateTimeOffset.UtcNow);

            // Parse comparison user IDs from comma-separated string
            var comparisonUserIds = ParseUserIds(compareWith);

            var comparison = await userMetricsService.GetUserMetricsComparisonAsync(
                userId, 
                comparisonUserIds, 
                from, 
                to, 
                cancellationToken);
            
            return Results.Ok(comparison);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { Error = ex.Message });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Internal Server Error",
                detail: ex.Message,
                statusCode: 500
            );
        }
    }

    private static DateTimeOffset ParseDateOrDefault(string? dateString, DateTimeOffset defaultValue)
    {
        if (string.IsNullOrEmpty(dateString))
            return defaultValue;

        if (DateTimeOffset.TryParse(dateString, out var result))
            return result;

        throw new ArgumentException($"Invalid date format: {dateString}. Use ISO 8601 format (e.g., 2024-01-01T00:00:00Z).");
    }

    private static long[] ParseUserIds(string? userIdsString)
    {
        if (string.IsNullOrEmpty(userIdsString))
            return Array.Empty<long>();

        try
        {
            return userIdsString
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(id => long.Parse(id.Trim()))
                .ToArray();
        }
        catch (FormatException)
        {
            throw new ArgumentException("Invalid user IDs format. Provide comma-separated numeric user IDs (e.g., 1,2,3).");
        }
    }
}