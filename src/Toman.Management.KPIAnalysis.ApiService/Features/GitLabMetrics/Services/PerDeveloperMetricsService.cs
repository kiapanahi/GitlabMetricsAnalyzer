using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

/// <summary>
/// Service implementation for calculating per-developer metrics from live GitLab data
/// </summary>
public sealed class PerDeveloperMetricsService : IPerDeveloperMetricsService
{
    private readonly IGitLabHttpClient _gitLabHttpClient;
    private readonly ILogger<PerDeveloperMetricsService> _logger;

    public PerDeveloperMetricsService(
        IGitLabHttpClient gitLabHttpClient,
        ILogger<PerDeveloperMetricsService> logger)
    {
        _gitLabHttpClient = gitLabHttpClient;
        _logger = logger;
    }

    public async Task<DeploymentFrequencyAnalysis> CalculateDeploymentFrequencyAsync(
        long userId,
        int windowDays = 30,
        CancellationToken cancellationToken = default)
    {
        if (windowDays <= 0)
        {
            throw new ArgumentException("Window days must be greater than 0", nameof(windowDays));
        }

        _logger.LogInformation("Starting deployment frequency calculation for user {UserId} over {WindowDays} days", userId, windowDays);

        // Get user details
        var user = await _gitLabHttpClient.GetUserByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            throw new InvalidOperationException($"User with ID {userId} not found");
        }

        var endDate = DateTime.UtcNow;
        var startDate = endDate.AddDays(-windowDays);

        _logger.LogDebug("Fetching contributed projects for user {UserId}", userId);

        // Get projects the user has contributed to
        var contributedProjects = await _gitLabHttpClient.GetUserContributedProjectsAsync(userId, cancellationToken);

        if (!contributedProjects.Any())
        {
            _logger.LogWarning("No contributed projects found for user {UserId}", userId);
            return CreateEmptyAnalysis(user, windowDays, startDate, endDate);
        }

        _logger.LogInformation("Found {ProjectCount} contributed projects for user {UserId}", contributedProjects.Count, userId);

        var projectDeployments = new List<ProjectDeploymentSummary>();
        var totalDeployments = 0;

        // Fetch pipelines for each project and identify deployments
        // Since GitLab API doesn't provide user info on pipeline list, we need to get commits for each project
        // and match pipeline refs with user's commits
        foreach (var project in contributedProjects)
        {
            try
            {
                _logger.LogDebug("Fetching pipelines and commits for project {ProjectId} ({ProjectName})", project.Id, project.Name);

                // Get user's commits in this project
                var commits = await _gitLabHttpClient.GetCommitsAsync(
                    project.Id,
                    new DateTimeOffset(startDate),
                    cancellationToken);

                var userCommitShas = commits
                    .Where(c => c.AuthorEmail == user.Email || c.CommitterEmail == user.Email)
                    .Select(c => c.Id)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                if (!userCommitShas.Any())
                {
                    _logger.LogDebug("No commits found for user {UserId} in project {ProjectName}", userId, project.Name);
                    continue;
                }

                // Get pipelines for this project
                var pipelines = await _gitLabHttpClient.GetPipelinesAsync(
                    project.Id,
                    new DateTimeOffset(startDate),
                    cancellationToken);

                // Identify successful production deployments that are associated with user's commits
                var userDeployments = pipelines
                    .Where(p => userCommitShas.Contains(p.Sha ?? string.Empty))
                    .Where(p => IsProductionDeployment(p))
                    .Where(p => p.UpdatedAt >= startDate && p.UpdatedAt <= endDate)
                    .ToList();

                if (userDeployments.Any())
                {
                    var deploymentCount = userDeployments.Count;
                    totalDeployments += deploymentCount;

                    projectDeployments.Add(new ProjectDeploymentSummary
                    {
                        ProjectId = project.Id,
                        ProjectName = project.Name ?? $"Project {project.Id}",
                        DeploymentCount = deploymentCount
                    });

                    _logger.LogDebug("Found {DeploymentCount} deployments for user {UserId} in project {ProjectName}",
                        deploymentCount, userId, project.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch data for project {ProjectId} ({ProjectName})",
                    project.Id, project.Name);
                // Continue with other projects
            }
        }

        // Calculate deployment frequency per week
        var deploymentFrequencyWk = (int)(totalDeployments * 7.0 / windowDays);

        _logger.LogInformation("Completed deployment frequency calculation for user {UserId}: {TotalDeployments} deployments, {DeploymentFrequencyWk} per week",
            userId, totalDeployments, deploymentFrequencyWk);

        return new DeploymentFrequencyAnalysis
        {
            UserId = userId,
            Username = user.Username ?? string.Empty,
            WindowDays = windowDays,
            AnalysisStartDate = startDate,
            AnalysisEndDate = endDate,
            TotalDeployments = totalDeployments,
            DeploymentFrequencyWk = deploymentFrequencyWk,
            Projects = projectDeployments.OrderByDescending(p => p.DeploymentCount).ToList()
        };
    }

    /// <summary>
    /// Identifies if a pipeline represents a production deployment
    /// A pipeline is considered a production deployment if:
    /// 1. Status is "success"
    /// 2. AND runs on main/master branch (production branches)
    /// </summary>
    private static bool IsProductionDeployment(Models.Raw.GitLabPipeline pipeline)
    {
        // Must be successful
        if (!string.Equals(pipeline.Status, "success", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Must run on production branches (main/master)
        var isProductionBranch = string.Equals(pipeline.Ref, "main", StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(pipeline.Ref, "master", StringComparison.OrdinalIgnoreCase);

        return isProductionBranch;
    }

    private static DeploymentFrequencyAnalysis CreateEmptyAnalysis(
        Models.Raw.GitLabUser user,
        int windowDays,
        DateTime startDate,
        DateTime endDate)
    {
        return new DeploymentFrequencyAnalysis
        {
            UserId = user.Id,
            Username = user.Username ?? string.Empty,
            WindowDays = windowDays,
            AnalysisStartDate = startDate,
            AnalysisEndDate = endDate,
            TotalDeployments = 0,
            DeploymentFrequencyWk = 0,
            Projects = new List<ProjectDeploymentSummary>()
        };
    }
}
