using Microsoft.AspNetCore.Mvc;

using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics;

/// <summary>
/// Endpoints for collecting and storing user metrics snapshots over time
/// </summary>
public static class UserMetricsCollectionEndpoints
{
    public static WebApplication MapUserMetricsCollectionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/users/{userId}/metrics")
            .WithTags("User Metrics Collection")
            .WithOpenApi();

        group.MapPost("/collect", CollectUserMetrics)
            .WithName("CollectUserMetrics")
            .WithSummary("Collect and store user metrics for a specific period")
            .WithDescription("Collects comprehensive user metrics from GitLab API and stores them with a timestamp for historical comparison. Default period is 3 months.")
            .Produces<UserMetricsCollectionResponse>(201)
            .Produces(400)
            .Produces(404)
            .Produces(500);

        group.MapGet("/history", GetUserMetricsHistory)
            .WithName("GetUserMetricsHistory")
            .WithSummary("Get historical user metrics snapshots")
            .WithDescription("Retrieves stored user metrics snapshots ordered by collection date for historical analysis and comparison.")
            .Produces<UserMetricsHistoryResponse>(200)
            .Produces(400)
            .Produces(404)
            .Produces(500);

        group.MapGet("/history/range", GetUserMetricsInRange)
            .WithName("GetUserMetricsInRange")
            .WithSummary("Get user metrics snapshots within a specific time range")
            .WithDescription("Retrieves stored user metrics snapshots collected within the specified date range.")
            .Produces<UserMetricsHistoryResponse>(200)
            .Produces(400)
            .Produces(404)
            .Produces(500);

        group.MapGet("/compare", CompareUserMetrics)
            .WithName("CompareUserMetrics")
            .WithSummary("Compare user metrics between two time periods")
            .WithDescription("Compares user metrics between two specific collection dates to show trends and changes over time.")
            .Produces<UserMetricsComparison>(200)
            .Produces(400)
            .Produces(404)
            .Produces(500);

        return app;
    }

    private static async Task<IResult> CollectUserMetrics(
        [FromServices] IUserMetricsCollectionService userMetricsCollectionService,
        [FromRoute] long userId,
        [FromQuery] string? fromDate = null,
        [FromQuery] string? toDate = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var from = ParseDateOrDefault(fromDate, DateTimeOffset.UtcNow.AddMonths(-3));
            var to = ParseDateOrDefault(toDate, DateTimeOffset.UtcNow);

            if (from >= to)
            {
                return Results.BadRequest(new { Error = "fromDate must be earlier than toDate" });
            }

            var metrics = await userMetricsCollectionService.CollectAndStoreUserMetricsAsync(userId, from, to, cancellationToken);

            var response = new UserMetricsCollectionResponse
            {
                UserId = metrics.UserId,
                Username = metrics.Username,
                CollectedAt = metrics.CollectedAt,
                FromDate = metrics.FromDate,
                ToDate = metrics.ToDate,
                PeriodDays = metrics.PeriodDays,
                Message = $"Successfully collected and stored metrics for user {metrics.Username} over {metrics.PeriodDays} days",
                Metrics = metrics
            };

            return Results.Created($"/api/users/{userId}/metrics/history", response);
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

    private static async Task<IResult> GetUserMetricsHistory(
        [FromServices] IUserMetricsCollectionService userMetricsCollectionService,
        [FromRoute] long userId,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (limit <= 0 || limit > 100)
            {
                return Results.BadRequest(new { Error = "Limit must be between 1 and 100" });
            }

            var snapshots = await userMetricsCollectionService.GetUserMetricsHistoryAsync(userId, limit, cancellationToken);

            if (!snapshots.Any())
            {
                return Results.NotFound(new { Error = $"No metrics snapshots found for user {userId}" });
            }

            var response = new UserMetricsHistoryResponse
            {
                UserId = userId,
                Username = snapshots.First().Username,
                TotalSnapshots = snapshots.Count,
                EarliestSnapshot = snapshots.LastOrDefault()?.CollectedAt,
                LatestSnapshot = snapshots.FirstOrDefault()?.CollectedAt,
                Snapshots = snapshots
            };

            return Results.Ok(response);
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

    private static async Task<IResult> GetUserMetricsInRange(
        [FromServices] IUserMetricsCollectionService userMetricsCollectionService,
        [FromRoute] long userId,
        [FromQuery] string fromDate,
        [FromQuery] string toDate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(fromDate) || string.IsNullOrWhiteSpace(toDate))
            {
                return Results.BadRequest(new { Error = "Both fromDate and toDate are required" });
            }

            var from = ParseDateOrDefault(fromDate, DateTimeOffset.MinValue);
            var to = ParseDateOrDefault(toDate, DateTimeOffset.MaxValue);

            if (from >= to)
            {
                return Results.BadRequest(new { Error = "fromDate must be earlier than toDate" });
            }

            var snapshots = await userMetricsCollectionService.GetUserMetricsInRangeAsync(userId, from, to, cancellationToken);

            if (!snapshots.Any())
            {
                return Results.NotFound(new { Error = $"No metrics snapshots found for user {userId} in the specified date range" });
            }

            var response = new UserMetricsHistoryResponse
            {
                UserId = userId,
                Username = snapshots.First().Username,
                TotalSnapshots = snapshots.Count,
                EarliestSnapshot = snapshots.LastOrDefault()?.CollectedAt,
                LatestSnapshot = snapshots.FirstOrDefault()?.CollectedAt,
                Snapshots = snapshots
            };

            return Results.Ok(response);
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

    private static async Task<IResult> CompareUserMetrics(
        [FromServices] IUserMetricsCollectionService userMetricsCollectionService,
        [FromRoute] long userId,
        [FromQuery] string baselineDate,
        [FromQuery] string currentDate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(baselineDate) || string.IsNullOrWhiteSpace(currentDate))
            {
                return Results.BadRequest(new { Error = "Both baselineDate and currentDate are required" });
            }

            var baseline = ParseDateOrDefault(baselineDate, DateTimeOffset.MinValue);
            var current = ParseDateOrDefault(currentDate, DateTimeOffset.MaxValue);

            if (baseline >= current)
            {
                return Results.BadRequest(new { Error = "baselineDate must be earlier than currentDate" });
            }

            var comparison = await userMetricsCollectionService.CompareUserMetricsAsync(userId, baseline, current, cancellationToken);

            if (comparison is null)
            {
                return Results.NotFound(new { Error = $"Could not find metrics snapshots for user {userId} at the specified dates" });
            }

            return Results.Ok(comparison);
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
        if (string.IsNullOrWhiteSpace(dateString))
            return defaultValue;

        if (DateTimeOffset.TryParse(dateString, out var result))
            return result;

        throw new ArgumentException($"Invalid date format: {dateString}. Use ISO 8601 format (e.g., '2024-01-01T00:00:00Z')");
    }
}
