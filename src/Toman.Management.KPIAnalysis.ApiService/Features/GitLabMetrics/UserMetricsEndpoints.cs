using Microsoft.AspNetCore.Mvc;

using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;
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
            .WithSummary("Get comprehensive metrics for a specific GitLab user")
            .WithDescription("Fetches user data directly from GitLab API and returns detailed productivity, collaboration, and quality metrics for the specified GitLab user ID within the given date range.")
            .Produces<UserMetricsResponse>(200)
            .Produces(400)
            .Produces(404)
            .Produces(500);

        group.MapGet("/{userId}/metrics/summary", GetUserMetricsSummary)
            .WithName("GetUserMetricsSummary")
            .WithSummary("Get a summary of key metrics for a GitLab user")
            .WithDescription("Fetches data directly from GitLab API and returns a high-level overview of the most important productivity indicators for the specified GitLab user ID.")
            .Produces<UserMetricsSummaryResponse>(200)
            .Produces(400)
            .Produces(404)
            .Produces(500);

        group.MapGet("/{userId}/metrics/trends", GetUserMetricsTrends)
            .WithName("GetUserMetricsTrends")
            .WithSummary("Get user metrics trends over time")
            .WithDescription("Fetches trend data for user metrics over specified time period.")
            .Produces<UserMetricsTrendsResponse>(200)
            .Produces(400)
            .Produces(404)
            .Produces(500);

        group.MapGet("/{userId}/metrics/comparison", GetUserMetricsComparison)
            .WithName("GetUserMetricsComparison")
            .WithSummary("Get user metrics comparison with peers")
            .WithDescription("Compare user metrics with peer developers.")
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

        return app;
    }

    private static async Task<IResult> GetUserCommitsByEmail(
        [FromServices] IGitLabService gitLabService,
        [FromRoute] long userId,
        [FromQuery] string? fromDate = null,
        [FromQuery] string? toDate = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var fromDateParsed = ParseDateOrDefault(fromDate, DateTimeOffset.UtcNow.AddDays(-30));
            var toDateParsed = ParseDateOrDefault(toDate, DateTimeOffset.UtcNow);

            // Get user info directly from GitLab API
            var user = await gitLabService.GetUserByIdAsync(userId, cancellationToken);

            if (user is null)
            {
                return Results.NotFound($"User with ID {userId} not found in GitLab");
            }

            var allCommitsByEmail = new List<RawCommit>();
            var projectCount = 0;

            try
            {
                // Get projects the user is involved with
                var userProjects = await gitLabService.GetUserProjectsAsync(userId, cancellationToken);
                projectCount = userProjects.Count;

                // Get commits from all projects using email-based filtering
                var commitTasks = userProjects.Select(async project =>
                {
                    try
                    {
                        return await gitLabService.GetCommitsByUserEmailAsync(project.Id, user.Email!, fromDateParsed, cancellationToken);
                    }
                    catch
                    {
                        return new List<RawCommit>(); // Return empty list if project fails
                    }
                });

                var projectCommits = await Task.WhenAll(commitTasks);

                // Aggregate all commits from all projects
                foreach (var commits in projectCommits)
                {
                    allCommitsByEmail.AddRange(commits.Where(c =>
                        c.CommittedAt >= fromDateParsed &&
                        c.CommittedAt < toDateParsed));
                }
            }
            catch (Exception ex)
            {
                // If project fetching fails, just return user info with no commits
                Console.WriteLine($"Failed to fetch projects/commits: {ex.Message}");
            }

            var response = new UserCommitsDebugResponse
            {
                UserId = userId,
                UserEmail = user.Email ?? "Unknown",
                UserName = user.Name ?? user.Username ?? "Unknown",
                FromDate = fromDateParsed,
                ToDate = toDateParsed,
                CommitsByEmail = allCommitsByEmail.Count,
                CommitsByUserId = 0, // Not applicable in on-demand mode
                ProjectsChecked = projectCount,
                EmailBasedCommits = allCommitsByEmail
                    .OrderByDescending(c => c.CommittedAt)
                    .Take(50)
                    .Select(c => new CommitInfo
                    {
                        CommitId = c.CommitId,
                        AuthorEmail = c.AuthorEmail,
                        AuthorName = c.AuthorName,
                        CommittedAt = c.CommittedAt,
                        ProjectName = c.ProjectName,
                        Message = c.Message.Length > 100 ? c.Message[..100] + "..." : c.Message
                    }).ToList(),
                UserIdBasedCommits = new List<CommitInfo>() // Not applicable in on-demand mode
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
        public required int ProjectsChecked { get; init; }
        public required List<CommitInfo> EmailBasedCommits { get; init; }
        public required List<CommitInfo> UserIdBasedCommits { get; init; }
    }

    public sealed record CommitInfo
    {
        public required string CommitId { get; init; }
        public required string AuthorEmail { get; init; }
        public required string AuthorName { get; init; }
        public required DateTimeOffset CommittedAt { get; init; }
        public required string ProjectName { get; init; }
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
                from,
                to,
                comparisonUserIds?.ToList(),
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
