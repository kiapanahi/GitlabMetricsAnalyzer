using Microsoft.AspNetCore.Mvc;
using KuriousLabs.Management.KPIAnalysis.ApiService.Features.CrossSystemMetrics.Services;

namespace KuriousLabs.Management.KPIAnalysis.ApiService.Features.CrossSystemMetrics;

/// <summary>
/// API endpoints for cross-system metrics (Jira ↔ GitLab)
/// </summary>
public static class CrossSystemMetricsEndpoints
{
    /// <summary>
    /// Map cross-system metrics endpoints
    /// </summary>
    public static IEndpointRouteBuilder MapCrossSystemMetricsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/cross-system")
            .WithTags("Cross-System Metrics")
            .WithOpenApi();

        group.MapGet("/delivery-metrics",
                async (
                    [FromQuery] string projectKey,
                    [FromQuery] string gitLabProjectIds,
                    [FromQuery] int windowDays,
                    ICrossSystemMetricsService metricsService,
                    CancellationToken cancellationToken) =>
                {
                    if (string.IsNullOrWhiteSpace(projectKey))
                    {
                        return Results.BadRequest("Project key is required");
                    }

                    if (string.IsNullOrWhiteSpace(gitLabProjectIds))
                    {
                        return Results.BadRequest("GitLab project IDs are required (comma-separated list)");
                    }

                    if (windowDays < 1 || windowDays > 365)
                    {
                        return Results.BadRequest("Window days must be between 1 and 365");
                    }

                    // Parse GitLab project IDs
                    var projectIdList = gitLabProjectIds
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(id =>
                        {
                            if (!long.TryParse(id, out var projectId))
                            {
                                throw new ArgumentException($"Invalid GitLab project ID: {id}");
                            }
                            return projectId;
                        })
                        .ToList();

                    if (projectIdList.Count == 0)
                    {
                        return Results.BadRequest("At least one GitLab project ID is required");
                    }

                    var metrics = await metricsService.CalculateDeliveryMetricsAsync(
                        projectKey,
                        projectIdList,
                        windowDays,
                        cancellationToken);

                    return Results.Ok(metrics);
                })
            .WithName("GetDeliveryMetrics")
            .WithDescription("Calculate delivery lead time and cycle time metrics by correlating Jira issues with GitLab commits and merge requests")
            .WithSummary("Get delivery metrics for a project")
            .Produces<Models.DeliveryMetrics>()
            .Produces(400);

        group.MapGet("/work-correlation/{projectKey}",
                async (
                    string projectKey,
                    [FromQuery] string gitLabProjectIds,
                    [FromQuery] int windowDays,
                    ICrossSystemMetricsService metricsService,
                    CancellationToken cancellationToken) =>
                {
                    if (string.IsNullOrWhiteSpace(gitLabProjectIds))
                    {
                        return Results.BadRequest("GitLab project IDs are required (comma-separated list)");
                    }

                    if (windowDays < 1 || windowDays > 365)
                    {
                        return Results.BadRequest("Window days must be between 1 and 365");
                    }

                    // Parse GitLab project IDs
                    var projectIdList = gitLabProjectIds
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(id =>
                        {
                            if (!long.TryParse(id, out var projectId))
                            {
                                throw new ArgumentException($"Invalid GitLab project ID: {id}");
                            }
                            return projectId;
                        })
                        .ToList();

                    if (projectIdList.Count == 0)
                    {
                        return Results.BadRequest("At least one GitLab project ID is required");
                    }

                    var correlation = await metricsService.CalculateWorkCorrelationAsync(
                        projectKey,
                        projectIdList,
                        windowDays,
                        cancellationToken);

                    return Results.Ok(correlation);
                })
            .WithName("GetWorkCorrelation")
            .WithDescription("Analyze correlation between Jira issues and GitLab activity to identify orphaned work")
            .WithSummary("Get work correlation report for a project")
            .Produces<Models.WorkCorrelation>()
            .Produces(400);

        group.MapGet("/issue/{issueKey}/activity",
                async (
                    string issueKey,
                    [FromQuery] string gitLabProjectIds,
                    ICrossSystemMetricsService metricsService,
                    CancellationToken cancellationToken) =>
                {
                    if (string.IsNullOrWhiteSpace(gitLabProjectIds))
                    {
                        return Results.BadRequest("GitLab project IDs are required (comma-separated list)");
                    }

                    // Parse GitLab project IDs
                    var projectIdList = gitLabProjectIds
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(id =>
                        {
                            if (!long.TryParse(id, out var projectId))
                            {
                                throw new ArgumentException($"Invalid GitLab project ID: {id}");
                            }
                            return projectId;
                        })
                        .ToList();

                    if (projectIdList.Count == 0)
                    {
                        return Results.BadRequest("At least one GitLab project ID is required");
                    }

                    var activity = await metricsService.GetIssueActivityAsync(
                        issueKey,
                        projectIdList,
                        cancellationToken);

                    return Results.Ok(activity);
                })
            .WithName("GetIssueActivity")
            .WithDescription("Get GitLab activity (commits, MRs) for a specific Jira issue")
            .WithSummary("Get GitLab activity for a Jira issue")
            .Produces<Models.IssueActivity>()
            .Produces(400)
            .Produces(404);

        return endpoints;
    }
}
