using KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure;
using KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;

namespace KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

/// <summary>
/// Service implementation for calculating CI/CD pipeline metrics from live GitLab data
/// </summary>
public sealed class PipelineMetricsService : IPipelineMetricsService
{
    private readonly IGitLabHttpClient _gitLabHttpClient;
    private readonly ILogger<PipelineMetricsService> _logger;

    public PipelineMetricsService(
        IGitLabHttpClient gitLabHttpClient,
        ILogger<PipelineMetricsService> logger)
    {
        _gitLabHttpClient = gitLabHttpClient;
        _logger = logger;
    }

    public async Task<PipelineMetricsResult> CalculatePipelineMetricsAsync(
        long projectId,
        int windowDays = 30,
        CancellationToken cancellationToken = default)
    {
        if (windowDays <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(windowDays), windowDays, "Window days must be greater than 0");
        }

        _logger.LogInformation("Calculating pipeline metrics for project {ProjectId} over {WindowDays} days", projectId, windowDays);

        // Get project details
        var project = await _gitLabHttpClient.GetProjectByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            throw new InvalidOperationException($"Project with ID {projectId} not found");
        }

        var windowEnd = DateTime.UtcNow;
        var windowStart = windowEnd.AddDays(-windowDays);

        _logger.LogDebug("Fetching pipelines for project {ProjectId} from {WindowStart} to {WindowEnd}",
            projectId, windowStart, windowEnd);

        // Get pipelines for the project within the time window
        var pipelines = await _gitLabHttpClient.GetPipelinesAsync(projectId, new DateTimeOffset(windowStart), cancellationToken);

        // Filter pipelines within time window
        var pipelinesInWindow = pipelines
            .Where(p => p.CreatedAt.HasValue && p.CreatedAt.Value >= windowStart && p.CreatedAt.Value <= windowEnd)
            .ToList();

        if (!pipelinesInWindow.Any())
        {
            _logger.LogWarning("No pipelines found for project {ProjectId} in the specified window", projectId);
            return CreateEmptyResult(project, windowDays, windowStart, windowEnd);
        }

        _logger.LogInformation("Processing {PipelineCount} pipelines for project {ProjectId}", pipelinesInWindow.Count, projectId);

        // Fetch jobs for all pipelines (with parallel execution for performance)
        var jobsFetchTasks = pipelinesInWindow.Select(async pipeline =>
        {
            try
            {
                var jobs = await _gitLabHttpClient.GetPipelineJobsAsync(projectId, pipeline.Id, cancellationToken);
                return (Pipeline: pipeline, Jobs: jobs);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch jobs for pipeline {PipelineId} in project {ProjectId}", pipeline.Id, projectId);
                return (Pipeline: pipeline, Jobs: (IReadOnlyList<GitLabPipelineJob>)Array.Empty<GitLabPipelineJob>());
            }
        });

        var pipelineJobsData = await Task.WhenAll(jobsFetchTasks);
        var allJobs = pipelineJobsData.SelectMany(pd => pd.Jobs).ToList();

        _logger.LogInformation("Processing {JobCount} jobs across {PipelineCount} pipelines", allJobs.Count, pipelinesInWindow.Count);

        // Calculate all metrics
        var failedJobs = CalculateFailedJobRate(allJobs);
        var retryMetrics = CalculatePipelineRetryRate(pipelinesInWindow);
        var waitTimeMetrics = CalculatePipelineWaitTime(pipelinesInWindow);
        var deploymentFrequency = CalculateDeploymentFrequency(pipelinesInWindow, project.DefaultBranch);
        var jobDurationTrends = CalculateJobDurationTrends(allJobs);
        var branchTypeMetrics = CalculateBranchTypeMetrics(pipelinesInWindow, project.DefaultBranch);
        var coverageMetrics = CalculateCoverageTrend(pipelinesInWindow);

        return new PipelineMetricsResult
        {
            ProjectId = projectId,
            ProjectName = project.Name ?? "Unknown",
            WindowDays = windowDays,
            WindowStart = windowStart,
            WindowEnd = windowEnd,
            FailedJobs = failedJobs,
            PipelineRetryRate = retryMetrics.RetryRate,
            RetriedPipelineCount = retryMetrics.RetriedCount,
            TotalPipelineCount = pipelinesInWindow.Count,
            PipelineWaitTimeP50Min = waitTimeMetrics.P50,
            PipelineWaitTimeP95Min = waitTimeMetrics.P95,
            PipelinesWithWaitTimeCount = waitTimeMetrics.Count,
            DeploymentFrequency = deploymentFrequency,
            JobDurationTrends = jobDurationTrends,
            BranchTypeMetrics = branchTypeMetrics,
            AverageCoveragePercent = coverageMetrics.AverageCoverage,
            CoverageTrend = coverageMetrics.Trend,
            PipelinesWithCoverageCount = coverageMetrics.Count
        };
    }

    private List<FailedJobSummary> CalculateFailedJobRate(List<GitLabPipelineJob> jobs)
    {
        if (!jobs.Any())
        {
            return new List<FailedJobSummary>();
        }

        // Group jobs by name and calculate failure rate
        var jobsByName = jobs
            .GroupBy(j => j.Name)
            .Select(g => new
            {
                JobName = g.Key,
                TotalRuns = g.Count(),
                FailureCount = g.Count(j => j.Status.Equals("failed", StringComparison.OrdinalIgnoreCase) && !j.AllowFailure)
            })
            .Where(j => j.FailureCount > 0) // Only include jobs that have failed
            .OrderByDescending(j => j.FailureCount)
            .Take(10) // Top 10 most failing jobs
            .Select(j => new FailedJobSummary
            {
                JobName = j.JobName,
                FailureCount = j.FailureCount,
                TotalRuns = j.TotalRuns,
                FailureRate = j.TotalRuns > 0 ? (decimal)j.FailureCount / j.TotalRuns : 0
            })
            .ToList();

        return jobsByName;
    }

    private (decimal? RetryRate, int RetriedCount) CalculatePipelineRetryRate(List<GitLabPipeline> pipelines)
    {
        if (!pipelines.Any())
        {
            return (null, 0);
        }

        // Group pipelines by SHA to identify retries
        var pipelinesBySha = pipelines
            .GroupBy(p => p.Sha)
            .Select(g => new
            {
                Sha = g.Key,
                PipelineCount = g.Count(),
                IsRetried = g.Count() > 1
            })
            .ToList();

        var retriedCount = pipelinesBySha.Count(p => p.IsRetried);
        var retryRate = (decimal)retriedCount / pipelinesBySha.Count;

        return (retryRate, retriedCount);
    }

    private (decimal? P50, decimal? P95, int Count) CalculatePipelineWaitTime(List<GitLabPipeline> pipelines)
    {
        var waitTimesInSeconds = pipelines
            .Where(p => p.CreatedAt.HasValue && p.StartedAt.HasValue)
            .Select(p => (p.StartedAt!.Value - p.CreatedAt!.Value).TotalSeconds)
            .Where(d => d > 0)
            .OrderBy(d => d)
            .ToList();

        if (!waitTimesInSeconds.Any())
        {
            return (null, null, 0);
        }

        var p50Index = (int)Math.Ceiling(waitTimesInSeconds.Count * 0.5) - 1;
        var p95Index = (int)Math.Ceiling(waitTimesInSeconds.Count * 0.95) - 1;

        var p50Minutes = (decimal)(waitTimesInSeconds[p50Index] / 60.0);
        var p95Minutes = (decimal)(waitTimesInSeconds[p95Index] / 60.0);

        return (p50Minutes, p95Minutes, waitTimesInSeconds.Count);
    }

    private int CalculateDeploymentFrequency(List<GitLabPipeline> pipelines, string? defaultBranch)
    {
        if (string.IsNullOrEmpty(defaultBranch))
        {
            defaultBranch = "main";
        }

        // Count pipelines on main/master/production branches
        var deploymentPipelines = pipelines.Count(p =>
            p.Ref?.Equals(defaultBranch, StringComparison.OrdinalIgnoreCase) == true ||
            p.Ref?.Equals("main", StringComparison.OrdinalIgnoreCase) == true ||
            p.Ref?.Equals("master", StringComparison.OrdinalIgnoreCase) == true ||
            p.Ref?.Equals("production", StringComparison.OrdinalIgnoreCase) == true);

        return deploymentPipelines;
    }

    private List<JobDurationTrend> CalculateJobDurationTrends(List<GitLabPipelineJob> jobs)
    {
        if (!jobs.Any())
        {
            return new List<JobDurationTrend>();
        }

        // Group jobs by name and calculate duration statistics
        var jobTrends = jobs
            .Where(j => j.Duration.HasValue && j.Duration.Value > 0)
            .GroupBy(j => j.Name)
            .Where(g => g.Count() >= 3) // Need at least 3 runs to determine trend
            .Select(g =>
            {
                var orderedJobs = g.OrderBy(j => j.CreatedAt).ToList();
                var durations = orderedJobs.Select(j => j.Duration!.Value).ToList();
                var durationsInMinutes = durations.Select(d => (decimal)(d / 60.0)).OrderBy(d => d).ToList();

                var p50Index = (int)Math.Ceiling(durationsInMinutes.Count * 0.5) - 1;
                var p95Index = (int)Math.Ceiling(durationsInMinutes.Count * 0.95) - 1;

                // Calculate trend: compare first half vs second half
                var midpoint = orderedJobs.Count / 2;
                var firstHalfAvg = orderedJobs.Take(midpoint).Average(j => j.Duration!.Value);
                var secondHalfAvg = orderedJobs.Skip(midpoint).Average(j => j.Duration!.Value);
                
                var trend = "stable";
                var changePercent = Math.Abs((secondHalfAvg - firstHalfAvg) / firstHalfAvg);
                
                if (changePercent > 0.1) // More than 10% change
                {
                    trend = secondHalfAvg < firstHalfAvg ? "improving" : "degrading";
                }

                return new JobDurationTrend
                {
                    JobName = g.Key,
                    AverageDurationMin = (decimal)(durations.Average() / 60.0),
                    DurationP50Min = durationsInMinutes[p50Index],
                    DurationP95Min = durationsInMinutes[p95Index],
                    Trend = trend,
                    RunCount = g.Count()
                };
            })
            .OrderByDescending(t => t.AverageDurationMin)
            .Take(10) // Top 10 longest running jobs
            .ToList();

        return jobTrends;
    }

    private BranchTypeMetrics CalculateBranchTypeMetrics(List<GitLabPipeline> pipelines, string? defaultBranch)
    {
        if (string.IsNullOrEmpty(defaultBranch))
        {
            defaultBranch = "main";
        }

        // Separate pipelines by branch type
        var mainBranchPipelines = pipelines
            .Where(p =>
                p.Ref?.Equals(defaultBranch, StringComparison.OrdinalIgnoreCase) == true ||
                p.Ref?.Equals("main", StringComparison.OrdinalIgnoreCase) == true ||
                p.Ref?.Equals("master", StringComparison.OrdinalIgnoreCase) == true ||
                p.Ref?.Equals("production", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        var featureBranchPipelines = pipelines.Except(mainBranchPipelines).ToList();

        var mainBranchSuccessCount = mainBranchPipelines.Count(p => p.Status?.Equals("success", StringComparison.OrdinalIgnoreCase) == true);
        var featureBranchSuccessCount = featureBranchPipelines.Count(p => p.Status?.Equals("success", StringComparison.OrdinalIgnoreCase) == true);

        return new BranchTypeMetrics
        {
            MainBranchSuccessRate = mainBranchPipelines.Any() ? (decimal)mainBranchSuccessCount / mainBranchPipelines.Count : null,
            MainBranchSuccessCount = mainBranchSuccessCount,
            MainBranchTotalCount = mainBranchPipelines.Count,
            FeatureBranchSuccessRate = featureBranchPipelines.Any() ? (decimal)featureBranchSuccessCount / featureBranchPipelines.Count : null,
            FeatureBranchSuccessCount = featureBranchSuccessCount,
            FeatureBranchTotalCount = featureBranchPipelines.Count
        };
    }

    private (decimal? AverageCoverage, string? Trend, int Count) CalculateCoverageTrend(List<GitLabPipeline> pipelines)
    {
        // Filter pipelines with coverage data
        var pipelinesWithCoverage = pipelines
            .Where(p => !string.IsNullOrEmpty(p.Coverage))
            .Select(p => new
            {
                Pipeline = p,
                CoverageValue = TryParseCoverage(p.Coverage)
            })
            .Where(p => p.CoverageValue.HasValue)
            .OrderBy(p => p.Pipeline.CreatedAt)
            .ToList();

        if (!pipelinesWithCoverage.Any())
        {
            return (null, null, 0);
        }

        var averageCoverage = (decimal)pipelinesWithCoverage.Average(p => p.CoverageValue!.Value);

        // Calculate trend: compare first half vs second half
        string? trend = null;
        if (pipelinesWithCoverage.Count >= 4)
        {
            var midpoint = pipelinesWithCoverage.Count / 2;
            var firstHalfAvg = pipelinesWithCoverage.Take(midpoint).Average(p => p.CoverageValue!.Value);
            var secondHalfAvg = pipelinesWithCoverage.Skip(midpoint).Average(p => p.CoverageValue!.Value);

            var changePercent = Math.Abs(secondHalfAvg - firstHalfAvg);
            
            if (changePercent > 1.0) // More than 1% change
            {
                trend = secondHalfAvg > firstHalfAvg ? "improving" : "degrading";
            }
            else
            {
                trend = "stable";
            }
        }

        return (averageCoverage, trend, pipelinesWithCoverage.Count);
    }

    private double? TryParseCoverage(string? coverage)
    {
        if (string.IsNullOrEmpty(coverage))
        {
            return null;
        }

        if (double.TryParse(coverage, out var value))
        {
            return value;
        }

        return null;
    }

    private PipelineMetricsResult CreateEmptyResult(
        GitLabProject project,
        int windowDays,
        DateTime windowStart,
        DateTime windowEnd)
    {
        return new PipelineMetricsResult
        {
            ProjectId = project.Id,
            ProjectName = project.Name ?? "Unknown",
            WindowDays = windowDays,
            WindowStart = windowStart,
            WindowEnd = windowEnd,
            FailedJobs = new List<FailedJobSummary>(),
            PipelineRetryRate = null,
            RetriedPipelineCount = 0,
            TotalPipelineCount = 0,
            PipelineWaitTimeP50Min = null,
            PipelineWaitTimeP95Min = null,
            PipelinesWithWaitTimeCount = 0,
            DeploymentFrequency = 0,
            JobDurationTrends = new List<JobDurationTrend>(),
            BranchTypeMetrics = new BranchTypeMetrics
            {
                MainBranchSuccessRate = null,
                MainBranchSuccessCount = 0,
                MainBranchTotalCount = 0,
                FeatureBranchSuccessRate = null,
                FeatureBranchSuccessCount = 0,
                FeatureBranchTotalCount = 0
            },
            AverageCoveragePercent = null,
            CoverageTrend = null,
            PipelinesWithCoverageCount = 0
        };
    }
}
