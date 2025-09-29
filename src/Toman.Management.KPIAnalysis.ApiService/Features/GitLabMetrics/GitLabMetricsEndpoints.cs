namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics;

public static class GitLabMetricsEndpoints
{
    public static WebApplication MapGitlabMetricsEndpoints(this WebApplication app)
    {
        app.MapApiV1Endpoints();

        app.MapGitLabCollectionEndpoints();

        app.MapPerDeveloperMetricsEndpoints();

        app.MapDataQualityEndpoints();

        app.MapMetricsExportEndpoints();

        return app;
    }
}
