namespace Toman.Management.KPIAnalysis;

public static class Constants
{
    public const string ServiceName = "Toman.Management.KPIAnalysis";
    public const string ServiceDisplayName = "Toman Management KPI Analysis Service";
    public const string ServiceDescription = "Provides KPI analysis for Toman Management.";
    public static readonly string ServiceVersion = typeof(Constants).Assembly.GetName().Version!.ToString(3);

    public static class Keys
    {
        public const string PostgresService = "postgres";
        public const string PostgresDatabase = "metricsdb";
        public const string GitlabMetricsCollector = "gitlabmetricscollector";
    }
}
