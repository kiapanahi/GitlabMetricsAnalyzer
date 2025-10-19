namespace KuriousLabs.Management.KPIAnalysis;

public static class Constants
{
    public const string ServiceName = "KuriousLabs.Management.KPIAnalysis";
    public const string ServiceDisplayName = "KuriousLabs Management KPI Analysis Service";
    public const string ServiceDescription = "Provides KPI analysis for KuriousLabs Management.";
    public static readonly string ServiceVersion = typeof(Constants).Assembly.GetName().Version!.ToString(3);

    public static class Keys
    {
        public const string GitlabMetricsCollector = "gitlabmetricscollector";
    }
}
