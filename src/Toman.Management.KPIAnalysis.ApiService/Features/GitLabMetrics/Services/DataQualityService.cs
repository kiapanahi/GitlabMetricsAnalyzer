using Microsoft.EntityFrameworkCore;

using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Data;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.DataQuality;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

/// <summary>
/// Service for performing data quality checks on ingested data
/// </summary>
public interface IDataQualityService
{
    /// <summary>
    /// Perform all data quality checks for a specific run
    /// </summary>
    Task<DataQualityReport> PerformDataQualityChecksAsync(Guid? runId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check referential integrity between related entities
    /// </summary>
    Task<DataQualityCheckResult> CheckReferentialIntegrityAsync(Guid? runId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check data completeness (missing required fields, recent data availability)
    /// </summary>
    Task<DataQualityCheckResult> CheckDataCompletenessAsync(Guid? runId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check data latency (how recent the data is)
    /// </summary>
    Task<DataQualityCheckResult> CheckDataLatencyAsync(Guid? runId = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of data quality service
/// </summary>
public sealed class DataQualityService : IDataQualityService
{
    private readonly GitLabMetricsDbContext _dbContext;
    private readonly IObservabilityMetricsService _metricsService;
    private readonly ILogger<DataQualityService> _logger;

    public DataQualityService(
        GitLabMetricsDbContext dbContext,
        IObservabilityMetricsService metricsService,
        ILogger<DataQualityService> logger)
    {
        _dbContext = dbContext;
        _metricsService = metricsService;
        _logger = logger;
    }

    public async Task<DataQualityReport> PerformDataQualityChecksAsync(Guid? runId = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting data quality checks for run {RunId}", runId);

        var checks = new List<DataQualityCheckResult>();

        // Perform all checks
        checks.Add(await CheckReferentialIntegrityAsync(runId, cancellationToken));
        checks.Add(await CheckDataCompletenessAsync(runId, cancellationToken));
        checks.Add(await CheckDataLatencyAsync(runId, cancellationToken));

        // Calculate overall status and score
        var scores = checks.Where(c => c.Score.HasValue).Select(c => c.Score!.Value).ToList();
        var overallScore = scores.Any() ? scores.Average() : 0.0;

        var failedChecks = checks.Count(c => c.Status == "failed");
        var warningChecks = checks.Count(c => c.Status == "warning");

        var overallStatus = failedChecks > 0 ? "critical" : (warningChecks > 0 ? "warning" : "healthy");

        var report = new DataQualityReport
        {
            Checks = checks,
            OverallStatus = overallStatus,
            OverallScore = overallScore,
            RunId = runId
        };

        _logger.LogInformation("Data quality checks completed. Status: {Status}, Score: {Score:F2}",
            overallStatus, overallScore);

        return report;
    }

    public async Task<DataQualityCheckResult> CheckReferentialIntegrityAsync(Guid? runId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var issues = new List<string>();

            // Check if all commits reference existing projects
            var commitsWithInvalidProjects = await _dbContext.RawCommits
                .Where(c => !_dbContext.Projects.Any(p => p.Id == c.ProjectId))
                .CountAsync(cancellationToken);

            if (commitsWithInvalidProjects > 0)
            {
                issues.Add($"{commitsWithInvalidProjects} commits reference non-existent projects");
            }

            // Check if all merge requests reference existing projects
            var mrsWithInvalidProjects = await _dbContext.RawMergeRequests
                .Where(mr => !_dbContext.Projects.Any(p => p.Id == mr.ProjectId))
                .CountAsync(cancellationToken);

            if (mrsWithInvalidProjects > 0)
            {
                issues.Add($"{mrsWithInvalidProjects} merge requests reference non-existent projects");
            }

            // Check if all pipelines reference existing projects
            var pipelinesWithInvalidProjects = await _dbContext.RawPipelines
                .Where(p => !_dbContext.Projects.Any(proj => proj.Id == p.ProjectId))
                .CountAsync(cancellationToken);

            if (pipelinesWithInvalidProjects > 0)
            {
                issues.Add($"{pipelinesWithInvalidProjects} pipelines reference non-existent projects");
            }

            var totalIssues = commitsWithInvalidProjects + mrsWithInvalidProjects + pipelinesWithInvalidProjects;
            var status = totalIssues == 0 ? "passed" : (totalIssues < 10 ? "warning" : "failed");
            var score = Math.Max(0.0, 1.0 - (totalIssues / 100.0)); // Score decreases with more issues

            var result = new DataQualityCheckResult
            {
                CheckType = "referential_integrity",
                Status = status,
                Score = score,
                Description = "Checks referential integrity between related entities",
                Details = issues.Any() ? string.Join("; ", issues) : "All referential integrity checks passed",
                RunId = runId
            };

            _metricsService.RecordDataQualityCheck("referential_integrity", status, score);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing referential integrity check");
            _metricsService.RecordDataQualityCheck("referential_integrity", "failed");

            return new DataQualityCheckResult
            {
                CheckType = "referential_integrity",
                Status = "failed",
                Description = "Error performing referential integrity check",
                Details = ex.Message,
                RunId = runId
            };
        }
    }

    public async Task<DataQualityCheckResult> CheckDataCompletenessAsync(Guid? runId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var issues = new List<string>();
            var totalEntities = 0;
            var completeEntities = 0;

            // Check completeness of commits (required: author info, message)
            var totalCommits = await _dbContext.RawCommits.CountAsync(cancellationToken);
            var completeCommits = await _dbContext.RawCommits
                .CountAsync(c => !string.IsNullOrEmpty(c.AuthorEmail) &&
                               !string.IsNullOrEmpty(c.AuthorName) &&
                               !string.IsNullOrEmpty(c.Message), cancellationToken);

            totalEntities += totalCommits;
            completeEntities += completeCommits;

            if (totalCommits > 0)
            {
                var completenessRatio = (double)completeCommits / totalCommits;
                if (completenessRatio < 0.95)
                {
                    issues.Add($"Only {completenessRatio:P} of commits have complete author/message info");
                }
            }

            // Check completeness of merge requests (required: title, author)
            var totalMRs = await _dbContext.RawMergeRequests.CountAsync(cancellationToken);
            var completeMRs = await _dbContext.RawMergeRequests
                .CountAsync(mr => !string.IsNullOrEmpty(mr.Title) && mr.AuthorUserId > 0, cancellationToken);

            totalEntities += totalMRs;
            completeEntities += completeMRs;

            if (totalMRs > 0)
            {
                var completenessRatio = (double)completeMRs / totalMRs;
                if (completenessRatio < 0.95)
                {
                    issues.Add($"Only {completenessRatio:P} of merge requests have complete title/author info");
                }
            }

            // Check if we have recent data (within last 24 hours)
            var recentDataCutoff = DateTimeOffset.UtcNow.AddHours(-24);
            var hasRecentCommits = await _dbContext.RawCommits
                .AnyAsync(c => c.CommittedAt > recentDataCutoff, cancellationToken);
            var hasRecentMRs = await _dbContext.RawMergeRequests
                .AnyAsync(mr => mr.CreatedAt > recentDataCutoff, cancellationToken);

            if (!hasRecentCommits && !hasRecentMRs)
            {
                issues.Add("No recent data found in the last 24 hours");
            }

            var overallCompleteness = totalEntities > 0 ? (double)completeEntities / totalEntities : 1.0;
            var status = issues.Any() ? (issues.Count > 2 ? "failed" : "warning") : "passed";

            var result = new DataQualityCheckResult
            {
                CheckType = "data_completeness",
                Status = status,
                Score = overallCompleteness,
                Description = "Checks data completeness and recency",
                Details = issues.Any() ? string.Join("; ", issues) : $"Data completeness: {overallCompleteness:P}",
                RunId = runId
            };

            _metricsService.RecordDataQualityCheck("data_completeness", status, overallCompleteness);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing data completeness check");
            _metricsService.RecordDataQualityCheck("data_completeness", "failed");

            return new DataQualityCheckResult
            {
                CheckType = "data_completeness",
                Status = "failed",
                Description = "Error performing data completeness check",
                Details = ex.Message,
                RunId = runId
            };
        }
    }

    public async Task<DataQualityCheckResult> CheckDataLatencyAsync(Guid? runId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // Get the last ingestion timestamp
            var lastIncrementalRun = await _dbContext.IngestionStates
                .Where(s => s.Entity == "incremental")
                .FirstOrDefaultAsync(cancellationToken);

            var issues = new List<string>();

            if (lastIncrementalRun is null)
            {
                issues.Add("No incremental runs found");
            }
            else
            {
                var lagMinutes = (DateTimeOffset.UtcNow - lastIncrementalRun.LastRunAt).TotalMinutes;

                if (lagMinutes > 120) // More than 2 hours lag
                {
                    issues.Add($"Data is stale: {lagMinutes:F0} minutes since last ingestion");
                }
                else if (lagMinutes > 60) // More than 1 hour lag
                {
                    issues.Add($"Data has some lag: {lagMinutes:F0} minutes since last ingestion");
                }
            }

            // Check if we have very old data that might indicate collection issues
            var veryOldDataCutoff = DateTimeOffset.UtcNow.AddDays(-30);
            var hasOnlyOldData = await _dbContext.RawCommits
                .AllAsync(c => c.CommittedAt < veryOldDataCutoff, cancellationToken);

            if (hasOnlyOldData && await _dbContext.RawCommits.AnyAsync(cancellationToken))
            {
                issues.Add("All data is older than 30 days");
            }

            var lagScore = lastIncrementalRun is not null ?
                Math.Max(0.0, 1.0 - ((DateTimeOffset.UtcNow - lastIncrementalRun.LastRunAt).TotalHours / 24.0)) : 0.0;

            var status = issues.Any() ? (issues.Count > 1 ? "failed" : "warning") : "passed";

            var result = new DataQualityCheckResult
            {
                CheckType = "data_latency",
                Status = status,
                Score = lagScore,
                Description = "Checks data freshness and latency",
                Details = issues.Any() ? string.Join("; ", issues) : "Data latency is within acceptable limits",
                RunId = runId
            };

            _metricsService.RecordDataQualityCheck("data_latency", status, lagScore);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing data latency check");
            _metricsService.RecordDataQualityCheck("data_latency", "failed");

            return new DataQualityCheckResult
            {
                CheckType = "data_latency",
                Status = "failed",
                Description = "Error performing data latency check",
                Details = ex.Message,
                RunId = runId
            };
        }
    }
}
