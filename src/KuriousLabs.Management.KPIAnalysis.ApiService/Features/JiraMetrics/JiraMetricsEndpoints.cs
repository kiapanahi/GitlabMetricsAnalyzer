using KuriousLabs.Management.KPIAnalysis.ApiService.Features.JiraMetrics.Services;

namespace KuriousLabs.Management.KPIAnalysis.ApiService.Features.JiraMetrics;

internal static class JiraMetricsEndpoints
{
    internal static IEndpointRouteBuilder MapJiraMetricsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/jira")
            .WithTags("Jira Metrics")
            .WithOpenApi();

        // Project-level metrics
        group.MapGet("/projects/{projectKey}/metrics", async (
            string projectKey,
            int windowDays,
            IIssueTrackingMetricsService issueTrackingService,
            CancellationToken cancellationToken) =>
        {
            if (windowDays < 1 || windowDays > 365)
            {
                return Results.BadRequest("windowDays must be between 1 and 365");
            }

            var metrics = await issueTrackingService.CalculateIssueTrackingMetricsAsync(
                projectKey, windowDays, cancellationToken);
            
            return Results.Ok(metrics);
        })
        .WithName("GetProjectIssueMetrics")
        .WithSummary("Get issue tracking metrics for a Jira project")
        .Produces(200)
        .Produces(400);

        group.MapGet("/projects/{projectKey}/project-metrics", async (
            string projectKey,
            int windowDays,
            IProjectJiraMetricsService projectMetricsService,
            CancellationToken cancellationToken) =>
        {
            if (windowDays < 1 || windowDays > 365)
            {
                return Results.BadRequest("windowDays must be between 1 and 365");
            }

            var metrics = await projectMetricsService.CalculateProjectMetricsAsync(
                projectKey, windowDays, cancellationToken);
            
            return Results.Ok(metrics);
        })
        .WithName("GetProjectJiraMetrics")
        .WithSummary("Get project-level Jira metrics (backlog, velocity, cycle time)")
        .Produces(200)
        .Produces(400);

        // User-level metrics
        group.MapGet("/users/{accountId}/metrics", async (
            string accountId,
            int windowDays,
            IUserJiraMetricsService userMetricsService,
            CancellationToken cancellationToken) =>
        {
            if (windowDays < 1 || windowDays > 365)
            {
                return Results.BadRequest("windowDays must be between 1 and 365");
            }

            var metrics = await userMetricsService.CalculateUserMetricsAsync(
                accountId, windowDays, cancellationToken);
            
            return Results.Ok(metrics);
        })
        .WithName("GetUserJiraMetrics")
        .WithSummary("Get Jira metrics for a specific user")
        .Produces(200)
        .Produces(400);

        return app;
    }
}
