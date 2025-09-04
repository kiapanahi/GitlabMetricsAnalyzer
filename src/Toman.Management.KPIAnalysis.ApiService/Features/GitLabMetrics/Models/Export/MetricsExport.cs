namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Export;

public sealed class MetricsExport
{
    public MetricsExport(string date, string org, string team, string repo, MetricsData metrics)
    {
        Date = date;
        Org = org;
        Team = team;
        Repo = repo;
        Metrics = metrics;
    }

    public string Date { get; }
    public string Org { get; }
    public string Team { get; }
    public string Repo { get; }
    public MetricsData Metrics { get; }
}

public sealed class MetricsData
{
    public required decimal MrCycleTimeP50H { get; init; }
    public required decimal PipelineSuccessRate { get; init; }
    public required int DeploymentFrequencyWk { get; init; }
    public required decimal ApprovalBypassRatio { get; init; }
    public required decimal TimeToFirstReviewP50H { get; init; }
    public required decimal TimeInReviewP50H { get; init; }
    public required decimal ReworkRate { get; init; }
    public required int MrThroughputWk { get; init; }
    public required int WipMrCount { get; init; }
    public required decimal WipAgeP50H { get; init; }
    public required decimal WipAgeP90H { get; init; }
    public required int ReleasesCadenceWk { get; init; }
    public required decimal MeanTimeToGreenSec { get; init; }
    public required decimal AvgPipelineDurationSec { get; init; }
    public required decimal FlakyJobRate { get; init; }
    public required int RollbackIncidence { get; init; }
    public required int DirectPushesDefault { get; init; }
    public required int ForcePushesProtected { get; init; }
    public required decimal SignedCommitRatio { get; init; }
    public required decimal BranchTtlP50H { get; init; }
    public required decimal BranchTtlP90H { get; init; }
    public required decimal IssueSlaBreachRate { get; init; }
    public required decimal ReopenedIssueRate { get; init; }
    public required decimal DefectEscapeRate { get; init; }
}
