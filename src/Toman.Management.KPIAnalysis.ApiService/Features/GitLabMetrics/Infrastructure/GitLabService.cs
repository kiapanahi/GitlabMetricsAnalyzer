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
            var allProjects = _gitLabClient.Projects.Get(new ProjectQuery()).ToList();
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
                    // For commits, we often only have email and name
                    // We'll use a placeholder user ID and resolve it later during user sync
                    // Use a hash of the email to create a consistent temporary ID
                    var authorEmail = commit.AuthorEmail ?? "unknown@example.com";
                    var tempAuthorUserId = Math.Abs(authorEmail.GetHashCode()); // Temporary ID based on email
                    
                    var rawCommit = new RawCommit
                    {
                        ProjectId = projectId,
                        ProjectName = project.Name,
                        CommitId = commit.Id.ToString(),
                        AuthorUserId = tempAuthorUserId, // Temporary ID - will be resolved later
                        AuthorName = commit.AuthorName ?? "Unknown",
                        AuthorEmail = authorEmail,
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

    public Task<IReadOnlyList<User>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching all users from GitLab");
            
            // Get all users
            var users = _gitLabClient.Users.Get(new UserQuery 
            { 
                PerPage = 100 // Adjust based on your GitLab instance
            }).ToList();
            
            _logger.LogInformation("Retrieved {UserCount} users from GitLab", users.Count);
            return Task.FromResult(users.AsReadOnly() as IReadOnlyList<User>);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get users from GitLab");
            return Task.FromResult(new List<User>().AsReadOnly() as IReadOnlyList<User>);
        }
    }

    public Task<User?> GetUserByIdAsync(long userId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching user {UserId} from GitLab", userId);
            
            var user = _gitLabClient.Users[userId];
            
            _logger.LogDebug("Retrieved user {UserId}: {Username}", userId, user?.Username);
            return Task.FromResult(user);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get user {UserId} from GitLab", userId);
            return Task.FromResult<User?>(null);
        }
    }

    public Task<IReadOnlyList<Project>> GetUserProjectsAsync(long userId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching projects for user {UserId} - returning all accessible projects", userId);
            
            // For now, return all accessible projects
            // In a production environment, you would implement proper project membership checking
            // This could involve calling GitLab's project members API for each project
            var allProjects = _gitLabClient.Projects.Get(new ProjectQuery()).ToList();
            
            _logger.LogInformation("Retrieved {ProjectCount} accessible projects for user analysis", allProjects.Count);
            return Task.FromResult(allProjects.AsReadOnly() as IReadOnlyList<Project>);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get projects for user {UserId}", userId);
            return Task.FromResult(new List<Project>().AsReadOnly() as IReadOnlyList<Project>);
        }
    }

    public Task<IReadOnlyList<RawCommit>> GetCommitsByUserEmailAsync(long projectId, string userEmail, DateTimeOffset? since = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching commits for project {ProjectId} filtered by user email {UserEmail}", projectId, userEmail);
            
            var project = _gitLabClient.Projects.GetById(projectId, new SingleProjectQuery());
            var commits = _gitLabClient.GetRepository(projectId).Commits.ToList();

            var rawCommits = new List<RawCommit>();

            foreach (var commit in commits.Where(c => 
                c.AuthorEmail?.Equals(userEmail, StringComparison.OrdinalIgnoreCase) == true))
            {
                try
                {
                    // Create a consistent user ID based on email
                    var authorUserId = Math.Abs(userEmail.GetHashCode());
                    
                    var rawCommit = new RawCommit
                    {
                        ProjectId = projectId,
                        ProjectName = project.Name,
                        CommitId = commit.Id.ToString(),
                        AuthorUserId = authorUserId, // Consistent email-based ID
                        AuthorName = commit.AuthorName ?? "Unknown",
                        AuthorEmail = userEmail,
                        CommittedAt = new DateTimeOffset(commit.CommittedDate),
                        Message = commit.Message ?? "",
                        Additions = commit.Stats?.Additions ?? 0,
                        Deletions = commit.Stats?.Deletions ?? 0,
                        IsSigned = false,
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

            _logger.LogDebug("Retrieved {CommitCount} commits for user email {UserEmail} in project {ProjectId}", 
                rawCommits.Count, userEmail, projectId);
            return Task.FromResult(rawCommits.AsReadOnly() as IReadOnlyList<RawCommit>);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get commits by user email {UserEmail} for project {ProjectId}", userEmail, projectId);
            return Task.FromResult(new List<RawCommit>().AsReadOnly() as IReadOnlyList<RawCommit>);
        }
    }
}
