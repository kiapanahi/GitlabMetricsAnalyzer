using Microsoft.EntityFrameworkCore;

using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Data;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

public sealed class MetricsCalculationService : IMetricsCalculationService
{
    private readonly GitLabMetricsDbContext _dbContext;
    private readonly ILogger<MetricsCalculationService> _logger;

    public MetricsCalculationService(
        GitLabMetricsDbContext dbContext,
        ILogger<MetricsCalculationService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<DeveloperMetrics> CalculateDeveloperMetricsAsync(long userId, DateTimeOffset fromDate, DateTimeOffset toDate, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Calculating metrics for developer {UserId} from {FromDate} to {ToDate}", userId, fromDate, toDate);

        var daysDiff = (toDate - fromDate).TotalDays;
        if (daysDiff <= 0)
        {
            throw new ArgumentException("ToDate must be after FromDate");
        }

        // Get commits for the developer in the date range
        var commits = await _dbContext.RawCommits
            .Where(c => c.AuthorUserId == userId && c.CommittedAt >= fromDate && c.CommittedAt < toDate)
            .ToListAsync(cancellationToken);

        // Get merge requests for the developer in the date range
        var mergeRequests = await _dbContext.RawMergeRequests
            .Where(mr => mr.AuthorUserId == userId && mr.CreatedAt >= fromDate && mr.CreatedAt < toDate)
            .ToListAsync(cancellationToken);

        // Get pipelines for the developer in the date range
        var pipelines = await _dbContext.RawPipelines
            .Where(p => p.AuthorUserId == userId && p.CreatedAt >= fromDate && p.CreatedAt < toDate)
            .ToListAsync(cancellationToken);

        // Calculate metrics
        var commitCount = commits.Count;
        var commitFrequencyPerDay = commitCount / daysDiff;

        var mrCount = mergeRequests.Count;
        var mergedMRs = mergeRequests.Where(mr => mr.MergedAt.HasValue).ToList();
        var averageMRCycleTime = mergedMRs.Count > 0
            ? TimeSpan.FromTicks((long)mergedMRs.Average(mr => (mr.MergedAt!.Value - mr.CreatedAt).Ticks))
            : (TimeSpan?)null;

        var pipelineCount = pipelines.Count;
        var successfulPipelines = pipelines.Count(p => p.IsSuccessful);
        var pipelineSuccessRate = pipelineCount > 0 ? (double)successfulPipelines / pipelineCount : 0.0;

        var totalLinesAdded = commits.Sum(c => c.Additions);
        var totalLinesDeleted = commits.Sum(c => c.Deletions);
        var averageMRSize = mergeRequests.Count > 0 ? mergeRequests.Average(mr => mr.ChangesCount) : 0.0;

        // Get username from first commit or MR
        var username = commits.FirstOrDefault()?.AuthorName ?? mergeRequests.FirstOrDefault()?.AuthorName ?? "Unknown";

        return new DeveloperMetrics(
            userId,
            username,
            fromDate,
            toDate,
            commitCount,
            commitFrequencyPerDay,
            mrCount,
            averageMRCycleTime,
            pipelineCount,
            pipelineSuccessRate,
            totalLinesAdded,
            totalLinesDeleted,
            averageMRSize
        );
    }

    public async Task<ProjectMetrics> CalculateProjectMetricsAsync(long projectId, DateTimeOffset fromDate, DateTimeOffset toDate, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Calculating metrics for project {ProjectId} from {FromDate} to {ToDate}", projectId, fromDate, toDate);

        // Get all data for the project in the date range
        var commits = await _dbContext.RawCommits
            .Where(c => c.ProjectId == projectId && c.CommittedAt >= fromDate && c.CommittedAt < toDate)
            .ToListAsync(cancellationToken);

        var mergeRequests = await _dbContext.RawMergeRequests
            .Where(mr => mr.ProjectId == projectId && mr.CreatedAt >= fromDate && mr.CreatedAt < toDate)
            .ToListAsync(cancellationToken);

        var pipelines = await _dbContext.RawPipelines
            .Where(p => p.ProjectId == projectId && p.CreatedAt >= fromDate && p.CreatedAt < toDate)
            .ToListAsync(cancellationToken);

        // Calculate metrics
        var totalCommits = commits.Count;
        var totalMergeRequests = mergeRequests.Count;
        var totalPipelines = pipelines.Count;

        var successfulPipelines = pipelines.Count(p => p.IsSuccessful);
        var pipelineSuccessRate = totalPipelines > 0 ? (double)successfulPipelines / totalPipelines : 0.0;

        var mergedMRs = mergeRequests.Where(mr => mr.MergedAt.HasValue).ToList();
        var averageMRCycleTime = mergedMRs.Count > 0
            ? TimeSpan.FromTicks((long)mergedMRs.Average(mr => (mr.MergedAt!.Value - mr.CreatedAt).Ticks))
            : (TimeSpan?)null;

        var activeDevelopers = commits.Select(c => c.AuthorUserId).Distinct().Count();

        // Calculate deployment frequency (successful pipelines to main/master)
        var deploymentPipelines = pipelines.Where(p =>
            p.IsSuccessful &&
            (p.Ref.EndsWith("/main") || p.Ref.EndsWith("/master") || p.Ref == "main" || p.Ref == "master")).Count();
        var deploymentFrequency = deploymentPipelines / (toDate - fromDate).TotalDays;

        var projectName = commits.FirstOrDefault()?.ProjectName ??
                         mergeRequests.FirstOrDefault()?.ProjectName ??
                         pipelines.FirstOrDefault()?.ProjectName ??
                         "Unknown";

        return new ProjectMetrics(
            projectId,
            projectName,
            fromDate,
            toDate,
            totalCommits,
            totalMergeRequests,
            totalPipelines,
            pipelineSuccessRate,
            averageMRCycleTime,
            activeDevelopers,
            deploymentFrequency
        );
    }

    public async Task<TeamMetrics> CalculateTeamMetricsAsync(long[] projectIds, DateTimeOffset fromDate, DateTimeOffset toDate, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Calculating team metrics for projects [{ProjectIds}] from {FromDate} to {ToDate}",
            string.Join(", ", projectIds), fromDate, toDate);

        // Get all data across projects
        var commits = await _dbContext.RawCommits
            .Where(c => projectIds.Contains(c.ProjectId) && c.CommittedAt >= fromDate && c.CommittedAt < toDate)
            .ToListAsync(cancellationToken);

        var mergeRequests = await _dbContext.RawMergeRequests
            .Where(mr => projectIds.Contains((int)mr.ProjectId) && mr.CreatedAt >= fromDate && mr.CreatedAt < toDate)
            .ToListAsync(cancellationToken);

        var pipelines = await _dbContext.RawPipelines
            .Where(p => projectIds.Contains(p.ProjectId) && p.CreatedAt >= fromDate && p.CreatedAt < toDate)
            .ToListAsync(cancellationToken);

        // Calculate team-level metrics
        var totalCommits = commits.Count;
        var totalMergeRequests = mergeRequests.Count;

        var successfulPipelines = pipelines.Count(p => p.IsSuccessful);
        var pipelineSuccessRate = pipelines.Count > 0 ? (double)successfulPipelines / pipelines.Count : 0.0;

        var mergedMRs = mergeRequests.Where(mr => mr.MergedAt.HasValue).ToList();
        var averageMRCycleTime = mergedMRs.Count > 0
            ? TimeSpan.FromTicks((long)mergedMRs.Average(mr => (mr.MergedAt!.Value - mr.CreatedAt).Ticks))
            : (TimeSpan?)null;

        var activeDevelopers = commits.Select(c => c.AuthorUserId).Distinct().Count();

        // Calculate per-developer breakdown
        var developerBreakdown = new Dictionary<long, DeveloperMetrics>();
        var allDeveloperIds = commits.Select(c => c.AuthorUserId)
            .Concat(mergeRequests.Select(mr => mr.AuthorUserId))
            .Concat(pipelines.Select(p => p.AuthorUserId))
            .Distinct();

        foreach (var developerId in allDeveloperIds)
        {
            var developerMetrics = await CalculateDeveloperMetricsAsync(developerId, fromDate, toDate, cancellationToken);
            developerBreakdown[developerId] = developerMetrics;
        }

        return new TeamMetrics(
            projectIds,
            fromDate,
            toDate,
            totalCommits,
            totalMergeRequests,
            pipelineSuccessRate,
            averageMRCycleTime,
            activeDevelopers,
            developerBreakdown
        );
    }

    public async Task ProcessDailyMetricsAsync(DateTimeOffset targetDate, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing daily metrics for {TargetDate}", targetDate.Date);

        // Get all projects that had activity on the target date
        var activeProjects = await _dbContext.RawCommits
            .Where(c => c.CommittedAt.Date == targetDate.Date)
            .Select(c => c.ProjectId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var activeProjectsFromMRs = await _dbContext.RawMergeRequests
            .Where(mr => mr.CreatedAt.Date == targetDate.Date || (mr.MergedAt.HasValue && mr.MergedAt.Value.Date == targetDate.Date))
            .Select(mr => mr.ProjectId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var activeProjectsFromPipelines = await _dbContext.RawPipelines
            .Where(p => p.CreatedAt.Date == targetDate.Date)
            .Select(p => p.ProjectId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var allActiveProjects = activeProjects
            .Concat(activeProjectsFromMRs)
            .Concat(activeProjectsFromPipelines)
            .Distinct()
            .ToArray();

        _logger.LogDebug("Found {ProjectCount} active projects for {TargetDate}", allActiveProjects.Length, targetDate.Date);

        // Calculate team metrics for the day (looking at last 30 days for context)
        var fromDate = targetDate.AddDays(-30);
        var toDate = targetDate.AddDays(1);

        if (allActiveProjects.Length > 0)
        {
            var teamMetrics = await CalculateTeamMetricsAsync(allActiveProjects, fromDate, toDate, cancellationToken);

            _logger.LogInformation("Daily metrics processed for {TargetDate}: {CommitCount} commits, {MRCount} MRs, {PipelineSuccessRate:P1} pipeline success rate",
                targetDate.Date, teamMetrics.TotalCommits, teamMetrics.TotalMergeRequests, teamMetrics.PipelineSuccessRate);

            // Here you could store the calculated metrics to a fact table for historical reporting
            // For now, we just log the results
        }
    }
}
