using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Toman.Management.KPIAnalysis.ApiService.Data.Extensions;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Configuration;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Data;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Operational;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

public sealed class GitLabCollectorService : IGitLabCollectorService
{
    private readonly IGitLabService _gitLabService;
    private readonly GitLabMetricsDbContext _dbContext;
    private readonly IUserSyncService _userSyncService;
    private readonly IDataEnrichmentService _dataEnrichmentService;
    private readonly CollectionConfiguration _collectionConfig;
    private readonly IObservabilityMetricsService _metricsService;
    private readonly IDataQualityService _dataQualityService;
    private readonly ILogger<GitLabCollectorService> _logger;

    public GitLabCollectorService(
        IGitLabService gitLabService,
        GitLabMetricsDbContext dbContext,
        IUserSyncService userSyncService,
        IDataEnrichmentService dataEnrichmentService,
        IOptions<CollectionConfiguration> collectionConfig,
        IObservabilityMetricsService metricsService,
        IDataQualityService dataQualityService,
        ILogger<GitLabCollectorService> logger)
    {
        _gitLabService = gitLabService;
        _dbContext = dbContext;
        _userSyncService = userSyncService;
        _dataEnrichmentService = dataEnrichmentService;
        _collectionConfig = collectionConfig.Value;
        _metricsService = metricsService;
        _dataQualityService = dataQualityService;
        _logger = logger;
    }

    public async Task RunBackfillCollectionAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting backfill GitLab collection");

        await CollectDataAsync(null, cancellationToken);

        // Update ingestion state
        var state = new IngestionState
        {
            Entity = "backfill",
            LastSeenUpdatedAt = DateTime.UtcNow,
            LastRunAt = DateTime.UtcNow
        };

        await _dbContext.UpsertAsync(state, cancellationToken);

        _logger.LogInformation("Completed backfill GitLab collection");
    }

    private async Task CollectDataAsync(DateTime? updatedAfter, CancellationToken cancellationToken)
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

    private async Task ProcessProjectAsync(long projectId, DateTime? updatedAfter, CancellationToken cancellationToken)
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

    private async Task CollectCommitsAsync(long projectId, DateTime? since, CancellationToken cancellationToken)
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

    private async Task CollectMergeRequestsAsync(long projectId, DateTime? updatedAfter, CancellationToken cancellationToken)
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

    private async Task CollectPipelinesAsync(long projectId, DateTime? updatedAfter, CancellationToken cancellationToken)
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

    #region Windowed Collection Methods

    public async Task<CollectionRunResponse> StartCollectionRunAsync(StartCollectionRunRequest request, CancellationToken cancellationToken = default)
    {
        var runId = Guid.NewGuid();
        var startTime = DateTime.UtcNow;

        using var activity = Diagnostics.ActivitySource.StartActivity("GitLabCollection.Run");
        activity?.SetTag("run_id", runId.ToString());
        activity?.SetTag("run_type", request.RunType);
        activity?.SetTag("trigger_source", request.TriggerSource);
        activity?.SetTag("window_size_hours", request.WindowSizeHours?.ToString());

        _logger.LogInformation("Starting collection run {RunId} of type {RunType}", runId, request.RunType);

        var collectionRun = new CollectionRun
        {
            Id = runId,
            RunType = request.RunType,
            Status = "running",
            StartedAt = startTime,
            TriggerSource = request.TriggerSource,
            WindowSizeHours = request.WindowSizeHours
        };

        try
        {
            // Save initial run record
            _dbContext.CollectionRuns.Add(collectionRun);
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Execute collection based on run type
            CollectionStats stats;
            if (request.RunType.Equals("backfill", StringComparison.OrdinalIgnoreCase))
            {
                stats = await ExecuteBackfillCollectionAsync(runId, request, cancellationToken);
            }
            else
            {
                throw new ArgumentException($"Unknown run type: {request.RunType}", nameof(request.RunType));
            }

            // Update run with completion
            await UpdateCollectionRunAsync(runId, "completed", stats, null, cancellationToken);

            // Record observability metrics
            var duration = DateTimeOffset.UtcNow - startTime;
            _metricsService.RecordRunDuration(request.RunType, "completed", duration, runId);
            _metricsService.RecordCollectionStats(
                stats.ProjectsProcessed,
                stats.CommitsCollected,
                stats.MergeRequestsCollected,
                stats.PipelinesCollected,
                stats.ReviewEventsCollected,
                runId);

            activity?.SetTag("status", "completed");
            activity?.SetTag("duration_seconds", duration.TotalSeconds.ToString("F2"));
            activity?.SetTag("projects_processed", stats.ProjectsProcessed.ToString());
            activity?.SetTag("commits_collected", stats.CommitsCollected.ToString());

            // Run data quality checks after successful collection
            try
            {
                var dataQualityReport = await _dataQualityService.PerformDataQualityChecksAsync(runId, cancellationToken);
                activity?.SetTag("data_quality_status", dataQualityReport.OverallStatus);
                activity?.SetTag("data_quality_score", dataQualityReport.OverallScore.ToString("F2"));

                _logger.LogInformation("Data quality checks completed for run {RunId}. Status: {Status}, Score: {Score:F2}",
                    runId, dataQualityReport.OverallStatus, dataQualityReport.OverallScore);
            }
            catch (Exception dqEx)
            {
                _logger.LogWarning(dqEx, "Data quality checks failed for run {RunId}", runId);
                activity?.SetTag("data_quality_error", dqEx.Message);
            }

            _logger.LogInformation("Completed collection run {RunId} successfully", runId);
            return await GetCollectionRunResponseAsync(runId, cancellationToken);
        }
        catch (Exception ex)
        {
            var duration = DateTimeOffset.UtcNow - startTime;
            _logger.LogError(ex, "Failed collection run {RunId}", runId);

            // Record failure metrics
            _metricsService.RecordRunDuration(request.RunType, "failed", duration, runId);
            _metricsService.RecordApiError("collection_run_failed", 500, runId);

            activity?.SetTag("status", "failed");
            activity?.SetTag("error_message", ex.Message);
            activity?.SetTag("duration_seconds", duration.TotalSeconds.ToString("F2"));

            await UpdateCollectionRunAsync(runId, "failed", null, ex.Message, cancellationToken);
            throw;
        }
    }

    public async Task<CollectionRunResponse?> GetCollectionRunStatusAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        return await GetCollectionRunResponseAsync(runId, cancellationToken);
    }

    public async Task<IReadOnlyList<CollectionRunResponse>> GetRecentCollectionRunsAsync(string? runType = null, int limit = 10, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.CollectionRuns.AsQueryable();

        if (!string.IsNullOrEmpty(runType))
        {
            query = query.Where(r => r.RunType == runType);
        }

        var runs = await query
            .OrderByDescending(r => r.StartedAt)
            .Take(Math.Min(limit, 50)) // Cap at 50
            .ToListAsync(cancellationToken);

        return runs.Select(MapToCollectionRunResponse).ToList().AsReadOnly();
    }

    private async Task<CollectionStats> ExecuteBackfillCollectionAsync(Guid runId, StartCollectionRunRequest request, CancellationToken cancellationToken)
    {
        var startDate = request.BackfillStartDate;
        var endDate = request.BackfillEndDate ?? DateTime.UtcNow;

        _logger.LogInformation("Collecting backfill data from {StartDate} to {EndDate} (Run {RunId})",
            startDate, endDate, runId);

        // Update run with window information
        await UpdateCollectionRunWindowAsync(runId, startDate?.DateTime, endDate.DateTime, null, cancellationToken);

        // Collect data for the backfill period
        var stats = await CollectDataForWindowAsync(startDate?.DateTime, endDate.DateTime, cancellationToken);

        // Update backfill state
        await UpsertIngestionStateAsync("backfill", endDate.DateTime, DateTime.UtcNow, null, cancellationToken);

        return stats;
    }

    private async Task<CollectionStats> CollectDataForWindowAsync(DateTime? startDate, DateTime? endDate, CancellationToken cancellationToken)
    {
        var stats = new CollectionStats();

        // Test connection first
        if (!await _gitLabService.TestConnectionAsync(cancellationToken))
        {
            throw new InvalidOperationException("GitLab connection test failed");
        }

        // Get projects
        var projects = await _gitLabService.GetProjectsAsync(cancellationToken);
        _logger.LogInformation("Processing {ProjectCount} projects for window collection", projects.Count);

        stats.ProjectsProcessed = projects.Count;

        // Process projects in parallel with throttling
        var semaphore = new SemaphoreSlim(_collectionConfig.MaxParallelProjects);
        var tasks = projects.Select(async project =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                await Task.Delay(_collectionConfig.ProjectProcessingDelayMs, cancellationToken);
                var projectStats = await ProcessProjectWithRetryAsync((int)project.Id, startDate, cancellationToken);
                lock (stats)
                {
                    stats.CommitsCollected += projectStats.CommitsCollected;
                    stats.MergeRequestsCollected += projectStats.MergeRequestsCollected;
                    stats.PipelinesCollected += projectStats.PipelinesCollected;
                    stats.ReviewEventsCollected += projectStats.ReviewEventsCollected;
                }
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        // Synchronize users after collecting data
        if (_collectionConfig.EnrichMergeRequestData)
        {
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

        return stats;
    }

    private async Task<CollectionStats> ProcessProjectWithRetryAsync(long projectId, DateTime? updatedAfter, CancellationToken cancellationToken)
    {
        var stats = new CollectionStats();
        var retryCount = 0;
        var delay = _collectionConfig.RetryDelayMs;

        while (retryCount <= _collectionConfig.MaxRetries)
        {
            try
            {
                // Collect commits
                var commits = await _gitLabService.GetCommitsAsync(projectId, updatedAfter, cancellationToken);
                if (commits.Count > 0)
                {
                    // Enrich commits with file exclusion analysis if enabled
                    if (_collectionConfig.CollectCommitStats)
                    {
                        var enrichedCommits = commits
                            .Where(c => !_dataEnrichmentService.ShouldExcludeCommit(c.Message))
                            .Select(c => _dataEnrichmentService.EnrichCommit(c))
                            .ToList();

                        if (enrichedCommits.Count > 0)
                        {
                            await _dbContext.UpsertRangeAsync(enrichedCommits, cancellationToken);
                            stats.CommitsCollected = enrichedCommits.Count;
                        }
                    }
                    else
                    {
                        await _dbContext.UpsertRangeAsync(commits, cancellationToken);
                        stats.CommitsCollected = commits.Count;
                    }
                }

                // Collect merge requests
                var mergeRequests = await _gitLabService.GetMergeRequestsAsync(projectId, updatedAfter, cancellationToken);
                if (mergeRequests.Count > 0)
                {
                    // Enrich merge requests with additional data if enabled
                    if (_collectionConfig.EnrichMergeRequestData)
                    {
                        var enrichedMergeRequests = new List<Models.Raw.RawMergeRequest>();

                        foreach (var mr in mergeRequests)
                        {
                            // Skip if branch should be excluded
                            if (_dataEnrichmentService.ShouldExcludeBranch(mr.SourceBranch) ||
                                _dataEnrichmentService.ShouldExcludeBranch(mr.TargetBranch))
                            {
                                continue;
                            }

                            // Get commits for this MR to enhance the data
                            var mrCommits = commits?.Where(c =>
                                c.CommittedAt >= mr.CreatedAt &&
                                c.CommittedAt <= (mr.MergedAt ?? DateTime.UtcNow))
                                .ToList();

                            var enrichedMr = _dataEnrichmentService.EnrichMergeRequest(mr, mrCommits);
                            enrichedMergeRequests.Add(enrichedMr);
                        }

                        if (enrichedMergeRequests.Count > 0)
                        {
                            await _dbContext.UpsertRangeAsync(enrichedMergeRequests, cancellationToken);
                            stats.MergeRequestsCollected = enrichedMergeRequests.Count;

                            // Collect review events if enabled
                            if (_collectionConfig.CollectReviewEvents)
                            {
                                var reviewEventsCount = await CollectReviewEventsForMergeRequestsAsync(projectId, enrichedMergeRequests, cancellationToken);
                                stats.ReviewEventsCollected = reviewEventsCount;
                            }
                        }
                    }
                    else
                    {
                        await _dbContext.UpsertRangeAsync(mergeRequests, cancellationToken);
                        stats.MergeRequestsCollected = mergeRequests.Count;

                        // Collect review events if enabled
                        if (_collectionConfig.CollectReviewEvents)
                        {
                            var reviewEventsCount = await CollectReviewEventsForMergeRequestsAsync(projectId, mergeRequests, cancellationToken);
                            stats.ReviewEventsCollected = reviewEventsCount;
                        }
                    }
                }

                // Collect pipelines
                var pipelines = await _gitLabService.GetPipelinesAsync(projectId, updatedAfter, cancellationToken);
                if (pipelines.Count > 0)
                {
                    await _dbContext.UpsertRangeAsync(pipelines, cancellationToken);
                    stats.PipelinesCollected = pipelines.Count;
                }

                break; // Success, exit retry loop
            }
            catch (Exception ex)
            {
                retryCount++;
                if (retryCount > _collectionConfig.MaxRetries)
                {
                    _logger.LogError(ex, "Failed to process project {ProjectId} after {Retries} retries", projectId, retryCount - 1);
                    throw;
                }

                _logger.LogWarning(ex, "Failed to process project {ProjectId}, retry {RetryCount}/{MaxRetries}",
                    projectId, retryCount, _collectionConfig.MaxRetries);
                await Task.Delay(delay, cancellationToken);
                delay *= 2; // Exponential backoff
            }
        }

        return stats;
    }

    private async Task<int> CollectReviewEventsForMergeRequestsAsync(long projectId, IReadOnlyList<Models.Raw.RawMergeRequest> mergeRequests, CancellationToken cancellationToken)
    {
        var totalReviewEvents = 0;

        foreach (var mr in mergeRequests)
        {
            try
            {
                var notes = await _gitLabService.GetMergeRequestNotesAsync(projectId, mr.MrId, cancellationToken);
                if (notes.Count > 0)
                {
                    await _dbContext.UpsertRangeAsync(notes, cancellationToken);
                    totalReviewEvents += notes.Count;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to collect review events for MR {MrId} in project {ProjectId}",
                    mr.MrId, projectId);
            }
        }

        return totalReviewEvents;
    }

    private async Task UpsertIngestionStateAsync(string entity, DateTime? lastSeenUpdatedAt, DateTime lastRunAt, int? windowSizeHours, CancellationToken cancellationToken)
    {
        var state = await _dbContext.IngestionStates
            .Where(s => s.Entity == entity)
            .FirstOrDefaultAsync(cancellationToken);

        if (state is not null)
        {
            // Update existing state
            var updatedState = new IngestionState
            {
                Id = state.Id,
                Entity = entity,
                LastSeenUpdatedAt = lastSeenUpdatedAt ?? state.LastSeenUpdatedAt,
                LastRunAt = lastRunAt,
                WindowSizeHours = windowSizeHours ?? state.WindowSizeHours,
                LastWindowEnd = lastSeenUpdatedAt ?? state.LastWindowEnd
            };
            await _dbContext.UpsertAsync(updatedState, cancellationToken);
        }
        else
        {
            // Create new state
            var newState = new IngestionState
            {
                Entity = entity,
                LastSeenUpdatedAt = lastSeenUpdatedAt ?? lastRunAt,
                LastRunAt = lastRunAt,
                WindowSizeHours = windowSizeHours,
                LastWindowEnd = lastSeenUpdatedAt
            };
            await _dbContext.UpsertAsync(newState, cancellationToken);
        }
    }

    private async Task UpdateCollectionRunAsync(Guid runId, string status, CollectionStats? stats, string? errorMessage, CancellationToken cancellationToken)
    {
        var run = await _dbContext.CollectionRuns
            .Where(r => r.Id == runId)
            .FirstOrDefaultAsync(cancellationToken);

        if (run is not null)
        {
            var updatedRun = new CollectionRun
            {
                Id = runId,
                RunType = run.RunType,
                Status = status,
                StartedAt = run.StartedAt,
                CompletedAt = status == "completed" || status == "failed" ? DateTime.UtcNow : run.CompletedAt,
                WindowStart = run.WindowStart,
                WindowEnd = run.WindowEnd,
                WindowSizeHours = run.WindowSizeHours,
                ProjectsProcessed = stats?.ProjectsProcessed ?? run.ProjectsProcessed,
                CommitsCollected = stats?.CommitsCollected ?? run.CommitsCollected,
                MergeRequestsCollected = stats?.MergeRequestsCollected ?? run.MergeRequestsCollected,
                PipelinesCollected = stats?.PipelinesCollected ?? run.PipelinesCollected,
                ReviewEventsCollected = stats?.ReviewEventsCollected ?? run.ReviewEventsCollected,
                ErrorMessage = errorMessage ?? run.ErrorMessage,
                ErrorDetails = errorMessage ?? run.ErrorDetails,
                TriggerSource = run.TriggerSource,
                CreatedAt = run.CreatedAt
            };
            await _dbContext.UpsertAsync(updatedRun, cancellationToken);
        }
    }

    private async Task UpdateCollectionRunWindowAsync(Guid runId, DateTime? windowStart, DateTime? windowEnd, int? windowSizeHours, CancellationToken cancellationToken)
    {
        var run = await _dbContext.CollectionRuns
            .Where(r => r.Id == runId)
            .FirstOrDefaultAsync(cancellationToken);

        if (run is not null)
        {
            run.WindowStart = windowStart;
            run.WindowEnd = windowEnd;
            run.WindowSizeHours = windowSizeHours;

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<CollectionRunResponse> GetCollectionRunResponseAsync(Guid runId, CancellationToken cancellationToken)
    {
        var run = await _dbContext.CollectionRuns
            .Where(r => r.Id == runId)
            .FirstOrDefaultAsync(cancellationToken);

        if (run is null)
        {
            throw new KeyNotFoundException($"Collection run {runId} not found");
        }

        return MapToCollectionRunResponse(run);
    }

    private static CollectionRunResponse MapToCollectionRunResponse(CollectionRun run)
    {
        return new CollectionRunResponse
        {
            RunId = run.Id,
            RunType = run.RunType,
            Status = run.Status,
            StartedAt = run.StartedAt,
            CompletedAt = run.CompletedAt,
            WindowStart = run.WindowStart,
            WindowEnd = run.WindowEnd,
            WindowSizeHours = run.WindowSizeHours,
            ProjectsProcessed = run.ProjectsProcessed,
            CommitsCollected = run.CommitsCollected,
            MergeRequestsCollected = run.MergeRequestsCollected,
            PipelinesCollected = run.PipelinesCollected,
            ReviewEventsCollected = run.ReviewEventsCollected,
            ErrorMessage = run.ErrorMessage
        };
    }

    #endregion

    private sealed class CollectionStats
    {
        public int ProjectsProcessed { get; set; }
        public int CommitsCollected { get; set; }
        public int MergeRequestsCollected { get; set; }
        public int PipelinesCollected { get; set; }
        public int ReviewEventsCollected { get; set; }
    }
}
