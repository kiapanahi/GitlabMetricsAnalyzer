using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using NGitLab;

using Toman.Management.KPIAnalysis.ApiService.Configuration;
using Toman.Management.KPIAnalysis.ApiService.Data.Extensions;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Configuration;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Data;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Dimensions;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Operational;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

public interface IGitLabCollectorService
{
    Task RunIncrementalCollectionAsync(CancellationToken cancellationToken = default);
    Task RunBackfillCollectionAsync(int days = 180, CancellationToken cancellationToken = default);
}

public sealed class GitLabCollectorService : IGitLabCollectorService
{
    private readonly IGitLabClient _gitLabClient;

    private readonly GitLabMetricsDbContext _dbContext;
    private readonly GitLabConfiguration _gitLabConfig;
    private readonly ProcessingConfiguration _processingConfig;
    private readonly ILogger<GitLabCollectorService> _logger;
    private readonly SemaphoreSlim _semaphore;

    public GitLabCollectorService(
        IGitLabClient gitLabClient,
        GitLabMetricsDbContext dbContext,
        IOptions<GitLabConfiguration> gitLabConfig,
        IOptions<ProcessingConfiguration> processingConfig,
        ILogger<GitLabCollectorService> logger)
    {
        _gitLabClient = gitLabClient;
        _dbContext = dbContext;
        _gitLabConfig = gitLabConfig.Value;
        _processingConfig = processingConfig.Value;
        _logger = logger;
        _semaphore = new SemaphoreSlim(_processingConfig.MaxDegreeOfParallelism);
    }

    public async Task RunIncrementalCollectionAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting incremental GitLab collection");

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

    public async Task RunBackfillCollectionAsync(int days = 180, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting backfill GitLab collection for {Days} days", days);

        var updatedAfter = DateTimeOffset.UtcNow.AddDays(-days);

        await CollectDataAsync(updatedAfter, cancellationToken);

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

    private async Task CollectDataAsync(DateTimeOffset updatedAfter, CancellationToken cancellationToken)
    {
        // Discover and collect projects
        var projects = await DiscoverProjectsAsync(cancellationToken);
        _logger.LogInformation("Discovered {ProjectCount} projects", projects.Count);

        // Create channel for project processing
        var channel = Channel.CreateUnbounded<DimProject>();
        var writer = channel.Writer;
        var reader = channel.Reader;

        // Start processing projects concurrently
        var processingTask = Task.Run(async () =>
        {
            await foreach (var project in reader.ReadAllAsync(cancellationToken))
            {
                await _semaphore.WaitAsync(cancellationToken);
                try
                {
                    await ProcessProjectAsync(project, updatedAfter, cancellationToken);
                }
                finally
                {
                    _semaphore.Release();
                }
            }
        }, cancellationToken);

        // Queue projects for processing
        foreach (var project in projects)
        {
            await writer.WriteAsync(project, cancellationToken);
        }
        writer.Complete();

        await processingTask;
    }

    private async Task<IReadOnlyList<DimProject>> DiscoverProjectsAsync(CancellationToken cancellationToken)
    {
        const int weeklyRefreshDays = 7;
        var refreshThreshold = DateTimeOffset.UtcNow.AddDays(-weeklyRefreshDays);

        // Check if we need to refresh projects from GitLab API
        var lastProjectsDiscovery = await _dbContext.IngestionStates
            .Where(s => s.Entity == "projects_discovery")
            .FirstOrDefaultAsync(cancellationToken);

        var shouldRefreshFromApi = lastProjectsDiscovery is null ||
                                   lastProjectsDiscovery.LastRunAt < refreshThreshold;

        if (shouldRefreshFromApi)
        {
            _logger.LogInformation("Refreshing projects from GitLab API (last refresh: {LastRefresh})",
                lastProjectsDiscovery?.LastRunAt.ToString("yyyy-MM-dd HH:mm:ss") ?? "never");

            await RefreshProjectsFromApiAsync(cancellationToken);

            // Update ingestion state
            var state = new IngestionState
            {
                Entity = "projects_discovery",
                LastSeenUpdatedAt = DateTimeOffset.UtcNow,
                LastRunAt = DateTimeOffset.UtcNow
            };
            await _dbContext.UpsertAsync(state, cancellationToken);
        }
        else
        {
            _logger.LogInformation("Using cached projects from database (last refresh: {LastRefresh})",
                lastProjectsDiscovery!.LastRunAt.ToString("yyyy-MM-dd HH:mm:ss"));
        }

        // Return projects from database (either just refreshed or from cache)
        var allProjects = await _dbContext.DimProjects.ToListAsync(cancellationToken);

        _logger.LogInformation("Retrieved {TotalProjectCount} projects from database", allProjects.Count);

        return allProjects;
    }

    private async Task RefreshProjectsFromApiAsync(CancellationToken cancellationToken)
    {
        var allProjects = new List<DimProject>();

        try
        {
            // Get all projects that the token has access to
            _logger.LogInformation("Discovering all accessible GitLab projects from API...");
            var gitLabProjects = _gitLabClient.Projects.Get(new NGitLab.Models.ProjectQuery { }).ToList();


            _logger.LogInformation("Found {ProjectCount} accessible projects", gitLabProjects.Count);

            // Convert GitLab projects to DimProject entities
            foreach (var project in gitLabProjects)
            {
                var dimProject = new DimProject
                {
                    id = (int)project.Id,
                    name = project.PathWithNamespace.Split('/').Last(),
                    name_with_namespace = project.PathWithNamespace.Replace('/', ' '),
                    path = project.PathWithNamespace.Split('/').Last(),
                    path_with_namespace = project.PathWithNamespace,
                    default_branch = project.DefaultBranch ?? "main",
                    ssh_url_to_repo = project.SshUrl,
                    http_url_to_repo = project.HttpUrl,
                    web_url = project.WebUrl,
                    visibility = project.VisibilityLevel.ToString(),
                    created_at = DateTime.UtcNow,
                    last_activity_at = project.LastActivityAt,
                    archived = project.LastActivityAt < DateTimeOffset.UtcNow.AddDays(-90)
                };

                allProjects.Add(dimProject);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to discover projects from GitLab API");
            throw;
        }

        _logger.LogInformation("Discovered {ProjectCount} projects from GitLab API", allProjects.Count);

        // Upsert projects to database
        await _dbContext.UpsertRangeAsync(allProjects, cancellationToken);
    }

    private async Task ProcessProjectAsync(DimProject project, DateTimeOffset updatedAfter, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Processing project {ProjectPath}", project.path_with_namespace);

            // Collect merge requests
            await CollectMergeRequestsAsync(project.id, updatedAfter, cancellationToken);

            // Collect commits
            await CollectCommitsAsync(project.id, updatedAfter, cancellationToken);

            // Collect pipelines
            await CollectPipelinesAsync(project.id, updatedAfter, cancellationToken);

            _logger.LogDebug("Completed processing project {ProjectPath}", project.path_with_namespace);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process project {ProjectPath}", project.path_with_namespace);
        }
    }

    private async Task CollectMergeRequestsAsync(long projectId, DateTimeOffset updatedAfter, CancellationToken cancellationToken)
    {
        var mergeRequests = _gitLabClient.GetMergeRequest(projectId).All.ToList();

        var rawMrs = new List<RawMergeRequest>();
        var users = new HashSet<DimUser>();

        foreach (var mr in mergeRequests)
        {
            // Collect user data
            var user = new DimUser
            {
                UserId = mr.Author.Id,
                Username = mr.Author.Username,
                Name = mr.Author.Name,
                State = mr.Author.State,
                IsBot = mr.Author.Bot,
                EmailHash = ComputeEmailHash(mr.Author.Email ?? "")
            };
            users.Add(user);

            // Parse changes count
            var changesCount = 0;
            if (int.TryParse(mr.ChangesCount, out var changes))
                changesCount = changes;

            var rawMr = new RawMergeRequest
            {
                ProjectId = projectId,
                MrId = mr.Iid,
                AuthorUserId = mr.Author.Id,
                CreatedAt = mr.CreatedAt,
                MergedAt = mr.MergedAt,
                ClosedAt = mr.ClosedAt,
                State = mr.State,
                ChangesCount = changesCount,
                SourceBranch = mr.SourceBranch,
                TargetBranch = mr.TargetBranch,
                ApprovalsRequired = 0, // Would need additional API call
                ApprovalsGiven = 0,    // Would need additional API call
                FirstReviewAt = null   // Would need additional API call
            };

            rawMrs.Add(rawMr);
        }

        // Upsert to database
        await _dbContext.UpsertRangeAsync(users, cancellationToken);
        await _dbContext.UpsertRangeAsync(rawMrs, cancellationToken);
    }

    private async Task CollectCommitsAsync(long projectId, DateTimeOffset updatedAfter, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("Commit collection is not implemented yet.");
        //var commits = await _gitLabApi.GetCommitsAsync(projectId, updatedAfter.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"), cancellationToken);

        //var rawCommits = new List<RawCommit>();

        //foreach (var commit in commits)
        //{
        //    // Create a dummy user for the commit author (we'll link by email hash later)
        //    var emailHash = ComputeEmailHash(commit.AuthorEmail);

        //    var rawCommit = new RawCommit
        //    {
        //        ProjectId = projectId,
        //        CommitId = commit.Id,
        //        AuthorUserId = 0, // Will be linked later by email hash
        //        CommittedAt = commit.CommittedDate,
        //        Additions = commit.Stats?.Additions ?? 0,
        //        Deletions = commit.Stats?.Deletions ?? 0,
        //        IsSigned = false // Would need additional logic to detect
        //    };

        //    rawCommits.Add(rawCommit);
        //}

        //await _dbContext.UpsertRangeAsync(rawCommits, cancellationToken);
    }

    private async Task CollectPipelinesAsync(int projectId, DateTimeOffset updatedAfter, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("Pipeline collection is not implemented yet.");
        //var pipelines = await _gitLabApi.GetPipelinesAsync(projectId, updatedAfter, cancellationToken);

        //var rawPipelines = new List<RawPipeline>();

        //foreach (var pipeline in pipelines)
        //{
        //    var rawPipeline = new RawPipeline
        //    {
        //        ProjectId = projectId,
        //        PipelineId = pipeline.Id,
        //        Sha = pipeline.Sha,
        //        Ref = pipeline.Ref,
        //        Status = pipeline.Status,
        //        CreatedAt = pipeline.CreatedAt,
        //        UpdatedAt = pipeline.UpdatedAt,
        //        DurationSec = pipeline.Duration ?? 0,
        //        Environment = null // Would need additional API call
        //    };

        //    rawPipelines.Add(rawPipeline);
        //}

        //await _dbContext.UpsertRangeAsync(rawPipelines, cancellationToken);
    }

    private static string ComputeEmailHash(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return string.Empty;

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(email.ToLowerInvariant()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
