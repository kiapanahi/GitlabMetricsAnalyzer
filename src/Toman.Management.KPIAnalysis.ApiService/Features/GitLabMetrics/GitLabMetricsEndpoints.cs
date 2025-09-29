namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics;

public static class GitLabMetricsEndpoints
{
    public static WebApplication MapGitlabCollectorEndpoints(this WebApplication app)
    {
        app.MapGitLabCollectionEndpoints();
        
        app.MapPerDeveloperMetricsEndpoints();
           
        app.MapDataQualityEndpoints();
           
        app.MapMetricsExportEndpoints();

        return app;
    }
}
