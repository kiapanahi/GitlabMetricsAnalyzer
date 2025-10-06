using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

/// <summary>
/// Service implementation for calculating per-developer metrics from live GitLab data
/// </summary>
public sealed class PerDeveloperMetricsService : IPerDeveloperMetricsService
{
    private readonly IGitLabService _gitLabService;
    private readonly ILogger<PerDeveloperMetricsService> _logger;

    public PerDeveloperMetricsService(
        IGitLabService gitLabService,
        ILogger<PerDeveloperMetricsService> logger)
    {
        _gitLabService = gitLabService;
        _logger = logger;
    }

    public async Task<PipelineSuccessRateResult> GetPipelineSuccessRateAsync(
        long userId,
        int lookbackDays = 30,
        CancellationToken cancellationToken = default)
    {
        if (lookbackDays <= 0)
        {
            throw new ArgumentException("Lookback days must be greater than 0", nameof(lookbackDays));
        }

        _logger.LogInformation("Calculating pipeline success rate for user {UserId} over {LookbackDays} days", userId, lookbackDays);

        // Get user details
        var user = await _gitLabService.GetUserByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            throw new InvalidOperationException($"User with ID {userId} not found");
        }

        var endDate = DateTime.UtcNow;
        var startDate = endDate.AddDays(-lookbackDays);

        _logger.LogDebug("Fetching pipeline data for user {UserId} from {StartDate} to {EndDate}", userId, startDate, endDate);

        // Get projects where user has contributed
        var projects = await _gitLabService.GetUserContributedProjectsAsync(userId, cancellationToken);

        if (!projects.Any())
        {
            _logger.LogWarning("No projects found for user {UserId}", userId);
            return CreateEmptyResult(user, lookbackDays, startDate, endDate);
        }

        _logger.LogInformation("Found {ProjectCount} projects for user {UserId}", projects.Count, userId);

        // Fetch pipelines from all projects
        var allPipelines = new List<Models.Raw.RawPipeline>();
        var projectSummaries = new List<ProjectPipelineSummary>();

        foreach (var project in projects)
        {
            try
            {
                _logger.LogDebug("Fetching pipelines for project {ProjectId} ({ProjectName})", project.Id, project.Name);

                var pipelines = await _gitLabService.GetPipelinesAsync(project.Id, startDate, cancellationToken);

                // Filter pipelines by author (user who triggered the pipeline)
                var userPipelines = pipelines
                    .Where(p => p.AuthorUserId == userId && p.CreatedAt >= startDate && p.CreatedAt < endDate)
                    .ToList();

                if (userPipelines.Any())
                {
                    allPipelines.AddRange(userPipelines);

                    var successful = userPipelines.Count(p => p.Status.Equals("success", StringComparison.OrdinalIgnoreCase));
                    var failed = userPipelines.Count(p => p.Status.Equals("failed", StringComparison.OrdinalIgnoreCase));

                    projectSummaries.Add(new ProjectPipelineSummary
                    {
                        ProjectId = project.Id,
                        ProjectName = project.Name ?? $"Project {project.Id}",
                        TotalPipelines = userPipelines.Count,
                        SuccessfulPipelines = successful,
                        FailedPipelines = failed
                    });

                    _logger.LogDebug("Found {PipelineCount} pipelines for user {UserId} in project {ProjectId}",
                        userPipelines.Count, userId, project.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch pipelines for project {ProjectId}", project.Id);
                // Continue with other projects
            }
        }

        if (!allPipelines.Any())
        {
            _logger.LogInformation("No pipelines found for user {UserId} in the specified time period", userId);
            return CreateEmptyResult(user, lookbackDays, startDate, endDate);
        }

        // Calculate metrics
        var totalPipelines = allPipelines.Count;
        var successfulPipelines = allPipelines.Count(p => p.Status.Equals("success", StringComparison.OrdinalIgnoreCase));
        var failedPipelines = allPipelines.Count(p => p.Status.Equals("failed", StringComparison.OrdinalIgnoreCase));
        var otherStatusPipelines = totalPipelines - successfulPipelines - failedPipelines;

        var successRate = totalPipelines > 0
            ? (decimal)successfulPipelines / totalPipelines
            : (decimal?)null;

        _logger.LogInformation(
            "Pipeline success rate for user {UserId}: {SuccessRate:P2} ({Successful}/{Total} pipelines)",
            userId, successRate, successfulPipelines, totalPipelines);

        return new PipelineSuccessRateResult
        {
            UserId = userId,
            Username = user.Username ?? string.Empty,
            Email = user.Email ?? string.Empty,
            LookbackDays = lookbackDays,
            AnalysisStartDate = startDate,
            AnalysisEndDate = endDate,
            TotalPipelines = totalPipelines,
            SuccessfulPipelines = successfulPipelines,
            FailedPipelines = failedPipelines,
            OtherStatusPipelines = otherStatusPipelines,
            PipelineSuccessRate = successRate,
            Projects = projectSummaries
        };
    }

    private static PipelineSuccessRateResult CreateEmptyResult(
        Models.Raw.GitLabUser user,
        int lookbackDays,
        DateTime startDate,
        DateTime endDate)
    {
        return new PipelineSuccessRateResult
        {
            UserId = user.Id,
            Username = user.Username ?? string.Empty,
            Email = user.Email ?? string.Empty,
            LookbackDays = lookbackDays,
            AnalysisStartDate = startDate,
            AnalysisEndDate = endDate,
            TotalPipelines = 0,
            SuccessfulPipelines = 0,
            FailedPipelines = 0,
            OtherStatusPipelines = 0,
            PipelineSuccessRate = null,
            Projects = new List<ProjectPipelineSummary>()
        };
    }
}
