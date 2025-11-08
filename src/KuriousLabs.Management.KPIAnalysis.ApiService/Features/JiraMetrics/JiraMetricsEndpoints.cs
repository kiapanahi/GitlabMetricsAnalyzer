namespace KuriousLabs.Management.KPIAnalysis.ApiService.Features.JiraMetrics;

internal static class JiraMetricsEndpoints
{
    internal static IEndpointRouteBuilder MapJiraMetricsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/jira")
            .WithTags("Jira Metrics")
            .WithOpenApi();

        // Endpoints will be added as services are implemented

        return app;
    }
}
