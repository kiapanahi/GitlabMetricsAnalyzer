namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics;

public static class GitLabMetricsEndpoints
{
    public static WebApplication MapGitlabMetricsEndpoints(this WebApplication app)
    {
        app.MapCommitTimeAnalysisEndpoints();
        app.MapMrThroughputEndpoints();

        return app;
    }
}
