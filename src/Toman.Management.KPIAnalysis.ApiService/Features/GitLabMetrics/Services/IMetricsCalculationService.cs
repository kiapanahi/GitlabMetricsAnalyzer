using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Facts;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

public interface IMetricsCalculationService
{
    Task<DeveloperMetrics> CalculateDeveloperMetricsAsync(long userId, DateTimeOffset fromDate, DateTimeOffset toDate, CancellationToken cancellationToken = default);
    Task<ProjectMetrics> CalculateProjectMetricsAsync(long projectId, DateTimeOffset fromDate, DateTimeOffset toDate, CancellationToken cancellationToken = default);
    Task<TeamMetrics> CalculateTeamMetricsAsync(long[] projectIds, DateTimeOffset fromDate, DateTimeOffset toDate, CancellationToken cancellationToken = default);
    Task ProcessDailyMetricsAsync(DateTimeOffset targetDate, CancellationToken cancellationToken = default);
}

public record DeveloperMetrics(
    long UserId,
    string Username,
    DateTimeOffset FromDate,
    DateTimeOffset ToDate,
    int CommitCount,
    double CommitFrequencyPerDay,
    int MergeRequestCount,
    TimeSpan? AverageMRCycleTime,
    int PipelineCount,
    double PipelineSuccessRate,
    int TotalLinesAdded,
    int TotalLinesDeleted,
    double AverageMRSize
);

public record ProjectMetrics(
    long ProjectId,
    string ProjectName,
    DateTimeOffset FromDate,
    DateTimeOffset ToDate,
    int TotalCommits,
    int TotalMergeRequests,
    int TotalPipelines,
    double PipelineSuccessRate,
    TimeSpan? AverageMRCycleTime,
    int ActiveDevelopers,
    double DeploymentFrequency
);

public record TeamMetrics(
    long[] ProjectIds,
    DateTimeOffset FromDate,
    DateTimeOffset ToDate,
    int TotalCommits,
    int TotalMergeRequests,
    double PipelineSuccessRate,
    TimeSpan? AverageMRCycleTime,
    int ActiveDevelopers,
    Dictionary<long, DeveloperMetrics> DeveloperBreakdown
);
