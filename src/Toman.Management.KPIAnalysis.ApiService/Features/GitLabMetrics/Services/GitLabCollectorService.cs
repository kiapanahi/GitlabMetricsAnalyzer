using Microsoft.EntityFrameworkCore;

using Toman.Management.KPIAnalysis.ApiService.Data.Extensions;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Data;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Operational;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

public sealed class GitLabCollectorService : IGitLabCollectorService
{
    private readonly IGitLabService _gitLabService;
    private readonly GitLabMetricsDbContext _dbContext;
    private readonly IUserSyncService _userSyncService;
    private readonly ILogger<GitLabCollectorService> _logger;

    public GitLabCollectorService(
        IGitLabService gitLabService,
        GitLabMetricsDbContext dbContext,
        IUserSyncService userSyncService,
        ILogger<GitLabCollectorService> logger)
    {
        _gitLabService = gitLabService;
        _dbContext = dbContext;
        _userSyncService = userSyncService;
        _logger = logger;
    }

    public async Task RunIncrementalCollectionAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting incremental GitLab collection");

        // Test connection first
        if (!await _gitLabService.TestConnectionAsync(cancellationToken))
        {
            _logger.LogError("GitLab connection test failed. Aborting incremental collection.");
            return;
        }

        // Get last run timestamp
        var lastRun = await _dbContext.IngestionStates
            .Where(s => s.Entity == "incremental")
            .FirstOrDefaultAsync(cancellationToken);

        var updatedAfter = lastRun?.LastSeenUpdatedAt ?? DateTimeOffset.UtcNow.AddHours(-1);

        await CollectDataAsync(updatedAfter, cancellationToken);

        // Update ingestion state
        var state = new IngestionState
        {
            Entity = "incremental",
            LastSeenUpdatedAt = DateTimeOffset.UtcNow,
            LastRunAt = DateTimeOffset.UtcNow
        };

        await _dbContext.UpsertAsync(state, cancellationToken);

        _logger.LogInformation("Completed incremental GitLab collection");
    }

    public async Task RunBackfillCollectionAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting backfill GitLab collection");

        await CollectDataAsync(null, cancellationToken);

        // Update ingestion state
        var state = new IngestionState
        {
            Entity = "backfill",
            LastSeenUpdatedAt = DateTimeOffset.UtcNow,
            LastRunAt = DateTimeOffset.UtcNow
        };

        await _dbContext.UpsertAsync(state, cancellationToken);

        _logger.LogInformation("Completed backfill GitLab collection");
    }

    private async Task CollectDataAsync(DateTimeOffset? updatedAfter, CancellationToken cancellationToken)
    {
        // Discover and collect projects
        var projects = await _gitLabService.GetProjectsAsync(cancellationToken);
        _logger.LogInformation("Discovered {ProjectCount} projects", projects.Count);

        foreach (var project in projects)
        {
            _logger.LogDebug("Processing project: {ProjectPath} (ID: {ProjectId})", project.PathWithNamespace, project.Id);
            await ProcessProjectAsync((int)project.Id, updatedAfter, cancellationToken);
        }

        // Synchronize users after collecting data
        _logger.LogInformation("Starting user synchronization after data collection");
        try
        {
            var syncedUsers = await _userSyncService.SyncMissingUsersFromRawDataAsync(cancellationToken);
            _logger.LogInformation("Synchronized {SyncedUsers} users from raw data", syncedUsers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to synchronize users from raw data");
        }
    }

    private async Task ProcessProjectAsync(long projectId, DateTimeOffset? updatedAfter, CancellationToken cancellationToken)
    {
        using var activity = Diagnostics.ActivitySource.StartActivity("ProcessProject");
        activity?.SetTag("projectId", projectId);
        try
        {
            _logger.LogDebug("Processing project {ProjectId}", projectId);

            // Collect commits
            await CollectCommitsAsync(projectId, updatedAfter, cancellationToken);

            // Collect merge requests
            await CollectMergeRequestsAsync(projectId, updatedAfter, cancellationToken);

            // Collect pipelines
            await CollectPipelinesAsync(projectId, updatedAfter, cancellationToken);

            _logger.LogDebug("Completed processing project {ProjectId}", projectId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process project {ProjectId}", projectId);
        }
    }

    private async Task CollectCommitsAsync(long projectId, DateTimeOffset? since, CancellationToken cancellationToken)
    {
        using var activity = Diagnostics.ActivitySource.StartActivity("CollectCommits");
        activity?.SetTag("projectId", projectId);
        try
        {
            var commits = await _gitLabService.GetCommitsAsync(projectId, since, cancellationToken);

            if (commits.Count > 0)
            {
                await _dbContext.UpsertRangeAsync(commits, cancellationToken);
                _logger.LogDebug("Collected {CommitCount} commits for project {ProjectId}", commits.Count, projectId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to collect commits for project {ProjectId}", projectId);
        }
    }

    private async Task CollectMergeRequestsAsync(long projectId, DateTimeOffset? updatedAfter, CancellationToken cancellationToken)
    {
        using var activity = Diagnostics.ActivitySource.StartActivity("CollectMergeRequests");
        activity?.SetTag("projectId", projectId);
        try
        {
            var mergeRequests = await _gitLabService.GetMergeRequestsAsync(projectId, updatedAfter, cancellationToken);

            if (mergeRequests.Count > 0)
            {
                await _dbContext.UpsertRangeAsync(mergeRequests, cancellationToken);
                _logger.LogDebug("Collected {MrCount} merge requests for project {ProjectId}", mergeRequests.Count, projectId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to collect merge requests for project {ProjectId}", projectId);
        }
    }

    private async Task CollectPipelinesAsync(long projectId, DateTimeOffset? updatedAfter, CancellationToken cancellationToken)
    {
        using var activity = Diagnostics.ActivitySource.StartActivity("CollectPipelines");
        activity?.SetTag("projectId", projectId);
        try
        {
            var pipelines = await _gitLabService.GetPipelinesAsync(projectId, updatedAfter, cancellationToken);

            if (pipelines.Count > 0)
            {
                await _dbContext.UpsertRangeAsync(pipelines, cancellationToken);
                _logger.LogDebug("Collected {PipelineCount} pipelines for project {ProjectId}", pipelines.Count, projectId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to collect pipelines for project {ProjectId}", projectId);
        }
    }
}
