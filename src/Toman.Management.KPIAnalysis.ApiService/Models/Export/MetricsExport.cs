namespace Toman.Management.KPIAnalysis.ApiService.Models.Export;

public sealed record MetricsExport(
    string Date,
    string Org,
    string Team,
    string Repo,
    MetricsData Metrics
);

public sealed record MetricsData(
    decimal MrCycleTimeP50H,
    decimal PipelineSuccessRate,
    int DeploymentFrequencyWk,
    decimal ApprovalBypassRatio,
    decimal TimeToFirstReviewP50H,
    decimal TimeInReviewP50H,
    decimal ReworkRate,
    int MrThroughputWk,
    int WipMrCount,
    decimal WipAgeP50H,
    decimal WipAgeP90H,
    int ReleasesCadenceWk,
    decimal MeanTimeToGreenSec,
    decimal AvgPipelineDurationSec,
    decimal FlakyJobRate,
    int RollbackIncidence,
    int DirectPushesDefault,
    int ForcePushesProtected,
    decimal SignedCommitRatio,
    decimal BranchTtlP50H,
    decimal BranchTtlP90H,
    decimal IssueSlaBreachRate,
    decimal ReopenedIssueRate,
    decimal DefectEscapeRate
);
