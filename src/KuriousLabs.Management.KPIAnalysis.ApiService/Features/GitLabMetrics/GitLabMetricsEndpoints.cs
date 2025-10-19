namespace KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics;

public static class GitLabMetricsEndpoints
{
    public static WebApplication MapGitlabMetricsEndpoints(this WebApplication app)
    {
        app.MapUserMetricsEndpoints();
        app.MapPipelineMetricsEndpoints();
        app.MapTeamMetricsEndpoints();
        app.MapProjectMetricsEndpoints();

        return app;
    }
}
