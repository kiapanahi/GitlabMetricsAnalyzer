using System.Text.Json;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NGitLab;
using NGitLab.Models;

using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Configuration;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure;

public sealed class GitLabService : IGitLabService
{
    private readonly IGitLabClient _gitLabClient;
    private readonly ILogger<GitLabService> _logger;

    public GitLabService(
        IGitLabClient gitLabClient,
        IOptions<GitLabConfiguration> configuration,
        ILogger<GitLabService> logger)
    {
        _gitLabClient = gitLabClient;
        _logger = logger;
    }

    public Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var user = _gitLabClient.Users.Current;
            _logger.LogInformation("Successfully connected to GitLab as user: {Username} ({Email})",
                user.Username, user.Email);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to GitLab API");
            return Task.FromResult(false);
        }
    }

    public async Task<IReadOnlyList<Project>> GetProjectsAsync(CancellationToken cancellationToken = default)
    {
        var projects = new List<Project>();

        try
        {
            // Get all accessible projects with basic query
            var allProjects = _gitLabClient.Projects.Get(new ProjectQuery()).Take(20).ToList(); // Limit to first 50 for testing
            projects.AddRange(allProjects);

            _logger.LogInformation("Retrieved {ProjectCount} projects", projects.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get projects");
        }

        return projects.AsReadOnly();
    }

    public async Task<IReadOnlyList<Project>> GetGroupProjectsAsync(long groupId, CancellationToken cancellationToken = default)
    {
        // Simplified - just return empty list for now
        _logger.LogDebug("Group projects retrieval not implemented yet for group {GroupId}", groupId);
        return new List<Project>().AsReadOnly();
    }

    public async Task<IReadOnlyList<RawCommit>> GetCommitsAsync(long projectId, DateTimeOffset? since = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var project = await _gitLabClient.Projects.GetByIdAsync(projectId, new SingleProjectQuery(), cancellationToken);
            var commits = _gitLabClient.GetRepository(projectId).Commits.ToList(); // Get commits for the project

            var rawCommits = new List<RawCommit>();

            foreach (var commit in commits)
            {
                try
                {
                    var rawCommit = new RawCommit
                    {
                        ProjectId = projectId,
                        ProjectName = project.Name,
                        CommitId = commit.Id.ToString(),
                        AuthorUserId = commit.AuthorName?.GetHashCode() ?? 0, // Fallback for missing user ID
                        AuthorName = commit.AuthorName ?? "Unknown",
                        AuthorEmail = commit.AuthorEmail ?? "unknown@example.com",
                        CommittedAt = new DateTimeOffset(commit.CommittedDate),
                        Message = commit.Message ?? "",
                        Additions = commit.Stats?.Additions ?? 0,
                        Deletions = commit.Stats?.Deletions ?? 0,
                        IsSigned = false, // NGitLab doesn't expose this easily
                        IngestedAt = DateTimeOffset.UtcNow
                    };

                    // Filter by date if specified
                    if (since.HasValue && rawCommit.CommittedAt < since.Value)
                        continue;

                    rawCommits.Add(rawCommit);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to process commit {CommitId} for project {ProjectId}", commit.Id, projectId);
                }
            }

            _logger.LogDebug("Retrieved {CommitCount} commits for project {ProjectId}", rawCommits.Count, projectId);
            return rawCommits.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get commits for project {ProjectId}", projectId);
            return new List<RawCommit>().AsReadOnly();
        }
    }

    public async Task<IReadOnlyList<RawMergeRequest>> GetMergeRequestsAsync(long projectId, DateTimeOffset? updatedAfter = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var project = _gitLabClient.Projects.GetByIdAsync(projectId, new SingleProjectQuery(), cancellationToken).Result;
            var mergeRequests = _gitLabClient.GetMergeRequest(projectId).All.Take(100).ToList(); // Limit for testing

            var rawMergeRequests = new List<RawMergeRequest>();

            foreach (var mr in mergeRequests)
            {
                try
                {
                    var rawMr = new RawMergeRequest
                    {
                        ProjectId = projectId,
                        ProjectName = project.Name,
                        MrId = mr.Iid,
                        AuthorUserId = mr.Author?.Id ?? 0,
                        AuthorName = mr.Author?.Name ?? "Unknown",
                        Title = mr.Title ?? "",
                        CreatedAt = mr.CreatedAt.ToUniversalTime(),
                        MergedAt = mr.MergedAt?.ToUniversalTime(),
                        ClosedAt = mr.ClosedAt?.ToUniversalTime(),
                        State = mr.State.ToString(),
                        ChangesCount = int.TryParse(mr.ChangesCount, out var changes) ? changes : 0,
                        SourceBranch = mr.SourceBranch ?? "",
                        TargetBranch = mr.TargetBranch ?? "",
                        ApprovalsRequired = 0, // Would need approvals API
                        ApprovalsGiven = 0, // Would need approvals API
                        FirstReviewAt = null, // Would need to check notes/discussions
                        ReviewerIds = null, // Would need to get reviewers
                        IngestedAt = DateTimeOffset.UtcNow
                    };

                    // Filter by date if specified
                    if (updatedAfter.HasValue && rawMr.CreatedAt < updatedAfter.Value)
                        continue;

                    rawMergeRequests.Add(rawMr);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to process merge request {MrId} for project {ProjectId}", mr.Iid, projectId);
                }
            }

            _logger.LogDebug("Retrieved {MrCount} merge requests for project {ProjectId}", rawMergeRequests.Count, projectId);
            return rawMergeRequests.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get merge requests for project {ProjectId}", projectId);
            return new List<RawMergeRequest>().AsReadOnly();
        }
    }

    public async Task<IReadOnlyList<RawPipeline>> GetPipelinesAsync(long projectId, DateTimeOffset? updatedAfter = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var project = _gitLabClient.Projects.GetByIdAsync(projectId, new SingleProjectQuery(), cancellationToken).Result;
            var pipelines = _gitLabClient.GetPipelines(projectId).All.Take(100).ToList(); // Limit for testing

            var rawPipelines = new List<RawPipeline>();

            foreach (var pipeline in pipelines)
            {
                try
                {
                    var rawPipeline = new RawPipeline
                    {
                        ProjectId = projectId,
                        ProjectName = project.Name,
                        PipelineId = (int)pipeline.Id,
                        Sha = pipeline.Sha.ToString(),
                        Ref = pipeline.Ref ?? "",
                        Status = pipeline.Status.ToString(),
                        AuthorUserId = 0, // PipelineBasic doesn't have User info
                        AuthorName = "Unknown",
                        TriggerSource = pipeline.Source?.ToString() ?? "unknown",
                        CreatedAt = pipeline.CreatedAt.ToUniversalTime(),
                        UpdatedAt = pipeline.UpdatedAt.ToUniversalTime(),
                        StartedAt = null, // Not available in PipelineBasic
                        FinishedAt = null, // Not available in PipelineBasic
                        DurationSec = 0, // Not available in PipelineBasic
                        Environment = null, // Would need to check pipeline jobs for environment
                        IngestedAt = DateTimeOffset.UtcNow
                    };

                    // Filter by date if specified
                    if (updatedAfter.HasValue && rawPipeline.CreatedAt < updatedAfter.Value)
                        continue;

                    rawPipelines.Add(rawPipeline);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to process pipeline {PipelineId} for project {ProjectId}", pipeline.Id, projectId);
                }
            }

            _logger.LogDebug("Retrieved {PipelineCount} pipelines for project {ProjectId}", rawPipelines.Count, projectId);
            return rawPipelines.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get pipelines for project {ProjectId}", projectId);
            return new List<RawPipeline>().AsReadOnly();
        }
    }
}
