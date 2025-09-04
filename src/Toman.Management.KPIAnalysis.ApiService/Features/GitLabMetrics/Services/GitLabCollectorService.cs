using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Toman.Management.KPIAnalysis.ApiService.Configuration;
using Toman.Management.KPIAnalysis.ApiService.Data.Extensions;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Configuration;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Data;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure.DTOs;
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
    private readonly IGitLabApiService _gitLabApi;
    private readonly GitLabMetricsDbContext _dbContext;
    private readonly Features.GitLabMetrics.Configuration.GitLabConfiguration _gitLabConfig;
    private readonly ProcessingConfiguration _processingConfig;
    private readonly ILogger<GitLabCollectorService> _logger;
    private readonly SemaphoreSlim _semaphore;

    public GitLabCollectorService(
        IGitLabApiService gitLabApi,
        GitLabMetricsDbContext dbContext,
        IOptions<Features.GitLabMetrics.Configuration.GitLabConfiguration> gitLabConfig,
        IOptions<ProcessingConfiguration> processingConfig,
        ILogger<GitLabCollectorService> logger)
    {
        _gitLabApi = gitLabApi;
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
        var allProjects = new List<DimProject>();

        foreach (var groupPath in _gitLabConfig.RootGroups)
        {
            try
            {
                var gitLabProjects = await _gitLabApi.GetProjectsAsync(groupPath, cancellationToken);
                
                foreach (var project in gitLabProjects)
                {
                    var dimProject = new DimProject
                    {
                        ProjectId = project.Id,
                        PathWithNamespace = project.PathWithNamespace,
                        DefaultBranch = project.DefaultBranch,
                        Visibility = project.Visibility,
                        ActiveFlag = project.LastActivityAt > DateTimeOffset.UtcNow.AddDays(-90)
                    };

                    allProjects.Add(dimProject);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to discover projects for group {GroupPath}", groupPath);
            }
        }

        // Upsert projects to database
        await _dbContext.UpsertRangeAsync(allProjects, cancellationToken);

        return allProjects;
    }

    private async Task ProcessProjectAsync(DimProject project, DateTimeOffset updatedAfter, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Processing project {ProjectPath}", project.PathWithNamespace);

            // Collect merge requests
            await CollectMergeRequestsAsync(project.ProjectId, updatedAfter, cancellationToken);

            // Collect commits
            await CollectCommitsAsync(project.ProjectId, updatedAfter, cancellationToken);

            // Collect pipelines
            await CollectPipelinesAsync(project.ProjectId, updatedAfter, cancellationToken);

            _logger.LogDebug("Completed processing project {ProjectPath}", project.PathWithNamespace);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process project {ProjectPath}", project.PathWithNamespace);
        }
    }

    private async Task CollectMergeRequestsAsync(int projectId, DateTimeOffset updatedAfter, CancellationToken cancellationToken)
    {
        var mergeRequests = await _gitLabApi.GetMergeRequestsAsync(projectId, updatedAfter, cancellationToken);
        
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

    private async Task CollectCommitsAsync(int projectId, DateTimeOffset updatedAfter, CancellationToken cancellationToken)
    {
        var commits = await _gitLabApi.GetCommitsAsync(projectId, updatedAfter.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"), cancellationToken);
        
        var rawCommits = new List<RawCommit>();

        foreach (var commit in commits)
        {
            // Create a dummy user for the commit author (we'll link by email hash later)
            var emailHash = ComputeEmailHash(commit.AuthorEmail);
            
            var rawCommit = new RawCommit
            {
                ProjectId = projectId,
                CommitId = commit.Id,
                AuthorUserId = 0, // Will be linked later by email hash
                CommittedAt = commit.CommittedDate,
                Additions = commit.Stats?.Additions ?? 0,
                Deletions = commit.Stats?.Deletions ?? 0,
                IsSigned = false // Would need additional logic to detect
            };

            rawCommits.Add(rawCommit);
        }

        await _dbContext.UpsertRangeAsync(rawCommits, cancellationToken);
    }

    private async Task CollectPipelinesAsync(int projectId, DateTimeOffset updatedAfter, CancellationToken cancellationToken)
    {
        var pipelines = await _gitLabApi.GetPipelinesAsync(projectId, updatedAfter, cancellationToken);
        
        var rawPipelines = new List<RawPipeline>();

        foreach (var pipeline in pipelines)
        {
            var rawPipeline = new RawPipeline
            {
                ProjectId = projectId,
                PipelineId = pipeline.Id,
                Sha = pipeline.Sha,
                Ref = pipeline.Ref,
                Status = pipeline.Status,
                CreatedAt = pipeline.CreatedAt,
                UpdatedAt = pipeline.UpdatedAt,
                DurationSec = pipeline.Duration ?? 0,
                Environment = null // Would need additional API call
            };

            rawPipelines.Add(rawPipeline);
        }

        await _dbContext.UpsertRangeAsync(rawPipelines, cancellationToken);
    }

    private static string ComputeEmailHash(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return string.Empty;

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(email.ToLowerInvariant()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
