using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Data;
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

        group.MapPost("/sync", SyncUsers)
            .WithName("SyncUsers")
            .WithSummary("Synchronize users from GitLab to the system")
            .WithDescription("Manually trigger user synchronization from GitLab API to populate the DimUsers table.")
            .Produces<SyncUsersResponse>(200)
            .Produces(500);

        group.MapGet("/debug/{userId}/commits", GetUserCommitsByEmail)
            .WithName("GetUserCommitsByEmail")
            .WithSummary("Debug endpoint to test email-based commit filtering")
            .WithDescription("Returns commits for a user using email-based filtering to verify the new approach works.")
            .Produces<UserCommitsDebugResponse>(200)
            .Produces(404)
            .Produces(500);

        return app;
    }

    private static async Task<IResult> GetUserCommitsByEmail(
        [FromServices] GitLabMetricsDbContext dbContext,
        [FromRoute] long userId,
        [FromQuery] string? fromDate = null,
        [FromQuery] string? toDate = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var fromDateParsed = ParseDateOrDefault(fromDate, DateTimeOffset.UtcNow.AddDays(-30));
            var toDateParsed = ParseDateOrDefault(toDate, DateTimeOffset.UtcNow);

            // Get user info
            var user = await dbContext.DimUsers
                .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

            if (user is null)
            {
                return Results.NotFound($"User with ID {userId} not found");
            }

            // Get commits using email-based filtering
            var commitsByEmail = await dbContext.RawCommits
                .Where(c => c.AuthorEmail == user.Email && 
                           c.CommittedAt >= fromDateParsed && 
                           c.CommittedAt < toDateParsed)
                .OrderByDescending(c => c.CommittedAt)
                .Take(50)
                .ToListAsync(cancellationToken);

            // Get commits using user ID filtering (old approach)
            var commitsByUserId = await dbContext.RawCommits
                .Where(c => c.AuthorUserId == userId && 
                           c.CommittedAt >= fromDateParsed && 
                           c.CommittedAt < toDateParsed)
                .OrderByDescending(c => c.CommittedAt)
                .Take(50)
                .ToListAsync(cancellationToken);

            var response = new UserCommitsDebugResponse
            {
                UserId = userId,
                UserEmail = user.Email,
                UserName = user.Name,
                FromDate = fromDateParsed,
                ToDate = toDateParsed,
                CommitsByEmail = commitsByEmail.Count,
                CommitsByUserId = commitsByUserId.Count,
                EmailBasedCommits = commitsByEmail.Select(c => new CommitInfo
                {
                    CommitId = c.CommitId,
                    AuthorEmail = c.AuthorEmail,
                    AuthorName = c.AuthorName,
                    CommittedAt = c.CommittedAt,
                    Message = c.Message.Length > 100 ? c.Message[..100] + "..." : c.Message
                }).ToList(),
                UserIdBasedCommits = commitsByUserId.Select(c => new CommitInfo
                {
                    CommitId = c.CommitId,
                    AuthorEmail = c.AuthorEmail,
                    AuthorName = c.AuthorName,
                    CommittedAt = c.CommittedAt,
                    Message = c.Message.Length > 100 ? c.Message[..100] + "..." : c.Message
                }).ToList()
            };

            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Debug Endpoint Failed",
                detail: ex.Message,
                statusCode: 500);
        }
    }

    // Response models for debug endpoint
    public sealed record UserCommitsDebugResponse
    {
        public required long UserId { get; init; }
        public required string UserEmail { get; init; }
        public required string UserName { get; init; }
        public required DateTimeOffset FromDate { get; init; }
        public required DateTimeOffset ToDate { get; init; }
        public required int CommitsByEmail { get; init; }
        public required int CommitsByUserId { get; init; }
        public required List<CommitInfo> EmailBasedCommits { get; init; }
        public required List<CommitInfo> UserIdBasedCommits { get; init; }
    }

    public sealed record CommitInfo
    {
        public required string CommitId { get; init; }
        public required string AuthorEmail { get; init; }
        public required string AuthorName { get; init; }
        public required DateTimeOffset CommittedAt { get; init; }
        public required string Message { get; init; }
    }

    private static async Task<IResult> SyncUsers(
        [FromServices] IUserSyncService userSyncService,
        ILogger<IUserMetricsService> logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Manual user synchronization requested");
            
            var syncedUsers = await userSyncService.SyncMissingUsersFromRawDataAsync(cancellationToken);
            
            var response = new SyncUsersResponse
            {
                SyncedUsers = syncedUsers,
                Message = $"Successfully synchronized {syncedUsers} users from GitLab",
                SyncedAt = DateTimeOffset.UtcNow
            };

            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to synchronize users");
            return Results.Problem(
                title: "User Synchronization Failed",
                detail: ex.Message,
                statusCode: 500);
        }
    }

    // Response model for user synchronization
    public sealed record SyncUsersResponse
    {
        public required int SyncedUsers { get; init; }
        public required string Message { get; init; }
        public required DateTimeOffset SyncedAt { get; init; }
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
