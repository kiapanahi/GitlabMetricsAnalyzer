using System.Collections.ObjectModel;

using Microsoft.EntityFrameworkCore;

using NGitLab;
using NGitLab.Models;

using Toman.Management.KPIAnalysis.ApiService.Data.Extensions;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Data;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Dimensions;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Operational;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

public sealed class GitLabCollectorService : IGitLabCollectorService
{
    private readonly IGitLabClient _gitLabClient;
    private readonly GitLabMetricsDbContext _dbContext;
    private readonly ILogger<GitLabCollectorService> _logger;

    public GitLabCollectorService(
        IGitLabClient gitLabClient,
        GitLabMetricsDbContext dbContext,
        ILogger<GitLabCollectorService> logger)
    {
        _gitLabClient = gitLabClient;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task RunIncrementalCollectionAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting incremental GitLab collection");

        // Get last run timestamp
        var lastRun = await _dbContext.IngestionStates
            .Where(s => s.Entity == "incremental")
            .FirstOrDefaultAsync(cancellationToken);

        var updatedAfter = lastRun?.LastSeenUpdatedAt ?? DateTimeOffset.UtcNow.AddHours(-1);

        await CollectDataAsync(cancellationToken);

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

        await CollectDataAsync(cancellationToken);

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

    private async Task CollectDataAsync(CancellationToken cancellationToken)
    {
        // Discover and collect projects
        var projects = DiscoverProjects();
        _logger.LogInformation("Discovered {ProjectCount} projects", projects.Count);

        foreach (var project in projects)
        {
            _logger.LogDebug("Project: {ProjectPath} (ID: {ProjectId})", project.PathWithNamespace, project.Id);
            await ProcessProjectAsync(project, cancellationToken);
        }
    }

    private ReadOnlyCollection<Project> DiscoverProjects()
    {
        var allProjects = _gitLabClient.Projects.Get(new ProjectQuery { }).ToList().AsReadOnly();

        _logger.LogInformation("Retrieved {TotalProjectCount} projects from Gitlab", allProjects.Count);

        return allProjects;
    }

    private async Task ProcessProjectAsync(Project project, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Processing project {ProjectPath}", project.PathWithNamespace);

            // Collect merge requests
            await CollectMergeRequestsAsync(project.Id, cancellationToken);

            // Collect commits
            //await CollectCommitsAsync(project.Id, updatedAfter, cancellationToken);

            // Collect pipelines
            //await CollectPipelinesAsync(project.Id, updatedAfter, cancellationToken);

            _logger.LogDebug("Completed processing project {ProjectPath}", project.PathWithNamespace);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process project {ProjectPath}", project.PathWithNamespace);
        }
    }

    private async Task CollectMergeRequestsAsync(long projectId, CancellationToken cancellationToken)
    {
        var mergeRequests = _gitLabClient.GetMergeRequest(projectId).All.ToList();

        var rawMrs = new List<RawMergeRequest>();
        var users = new HashSet<DimUser>();

        foreach (var mr in mergeRequests)
        {
            // // Collect user data
            // var user = new DimUser
            // {
            //     UserId = mr.Author.Id,
            //     Username = mr.Author.Username,
            //     Name = mr.Author.Name,
            //     State = mr.Author.State,
            //     IsBot = mr.Author.Bot,
            //     Email = mr.Author.Email ?? ""
            // };
            // users.Add(user);

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
        // await _dbContext.UpsertRangeAsync(users, cancellationToken);
        await _dbContext.UpsertRangeAsync(rawMrs, cancellationToken);
    }

    private Task CollectCommitsAsync(long projectId, DateTimeOffset updatedAfter, CancellationToken cancellationToken)
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

    private Task CollectPipelinesAsync(int projectId, DateTimeOffset updatedAfter, CancellationToken cancellationToken)
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

}
