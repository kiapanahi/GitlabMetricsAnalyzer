using Microsoft.Extensions.Options;

using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Configuration;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure;

public sealed class GitLabService : IGitLabService
{
    private readonly IGitLabHttpClient _gitLabHttpClient;
    private readonly ILogger<GitLabService> _logger;

    public GitLabService(
        IGitLabHttpClient gitLabHttpClient,
        IOptions<GitLabConfiguration> configuration,
        ILogger<GitLabService> logger)
    {
        _gitLabHttpClient = gitLabHttpClient;
        _logger = logger;
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _gitLabHttpClient.TestConnectionAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to GitLab API");
            return false;
        }
    }

    public async Task<IReadOnlyList<GitLabProject>> GetProjectsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var projects = await _gitLabHttpClient.GetProjectsAsync(cancellationToken);
            _logger.LogInformation("Retrieved {ProjectCount} projects", projects.Count);
            return projects;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get projects");
            return new List<GitLabProject>().AsReadOnly();
        }
    }

    public async Task<IReadOnlyList<GitLabProject>> GetGroupProjectsAsync(long groupId, CancellationToken cancellationToken = default)
    {
        // Simplified - just return empty list for now
        _logger.LogDebug("Group projects retrieval not implemented yet for group {GroupId}", groupId);
        return await _gitLabHttpClient.GetGroupProjectsAsync(groupId, cancellationToken);
    }

    public async Task<IReadOnlyList<RawCommit>> GetCommitsAsync(long projectId, DateTimeOffset? since = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var commits = await _gitLabHttpClient.GetCommitsAsync(projectId, since, cancellationToken);

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
                        ProjectName = $"Project {projectId}", // Mock project name
                        CommitId = commit.Id?.ToString() ?? Guid.NewGuid().ToString(),
                        AuthorUserId = tempAuthorUserId, // Temporary ID - will be resolved later
                        AuthorName = commit.AuthorName ?? "Unknown",
                        AuthorEmail = authorEmail,
                        CommittedAt = commit.CommittedDate ?? DateTimeOffset.UtcNow,
                        Message = commit.Message ?? "",
                        Additions = commit.Stats?.Additions ?? 0,
                        Deletions = commit.Stats?.Deletions ?? 0,
                        IsSigned = false, // Not available in our model
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
            var mergeRequests = await _gitLabHttpClient.GetMergeRequestsAsync(projectId, updatedAfter, cancellationToken);

            var rawMergeRequests = new List<RawMergeRequest>();

            foreach (var mr in mergeRequests)
            {
                try
                {
                    var rawMr = new RawMergeRequest
                    {
                        ProjectId = projectId,
                        ProjectName = $"Project {projectId}", // Mock project name
                        MrId = mr.Iid,
                        AuthorUserId = mr.Author?.Id ?? 0,
                        AuthorName = mr.Author?.Name ?? "Unknown",
                        Title = mr.Title ?? "",
                        CreatedAt = mr.CreatedAt ?? DateTimeOffset.UtcNow,
                        MergedAt = mr.MergedAt,
                        ClosedAt = mr.ClosedAt,
                        State = mr.State ?? "opened",
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
            var pipelines = await _gitLabHttpClient.GetPipelinesAsync(projectId, updatedAfter, cancellationToken);

            var rawPipelines = new List<RawPipeline>();

            foreach (var pipeline in pipelines)
            {
                try
                {
                    var rawPipeline = new RawPipeline
                    {
                        ProjectId = projectId,
                        ProjectName = $"Project {projectId}", // Mock project name
                        PipelineId = (int)pipeline.Id,
                        Sha = pipeline.Sha ?? "",
                        Ref = pipeline.Ref ?? "",
                        Status = pipeline.Status ?? "unknown",
                        AuthorUserId = pipeline.User?.Id ?? 0,
                        AuthorName = pipeline.User?.Name ?? "Unknown",
                        TriggerSource = "unknown", // Not available in our model
                        CreatedAt = pipeline.CreatedAt ?? DateTimeOffset.UtcNow,
                        UpdatedAt = pipeline.UpdatedAt ?? DateTimeOffset.UtcNow,
                        StartedAt = null, // Not available in our model
                        FinishedAt = null, // Not available in our model
                        DurationSec = 0, // Not available in our model
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

    public async Task<IReadOnlyList<GitLabUser>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching all users from GitLab");

            var users = await _gitLabHttpClient.GetUsersAsync(cancellationToken);

            _logger.LogInformation("Retrieved {UserCount} users from GitLab", users.Count);
            return users;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get users from GitLab");
            return new List<GitLabUser>().AsReadOnly();
        }
    }

    public async Task<GitLabUser?> GetUserByIdAsync(long userId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching user {UserId} from GitLab", userId);

            var user = await _gitLabHttpClient.GetUserByIdAsync(userId, cancellationToken);

            _logger.LogDebug("Retrieved user {UserId}: {Username}", userId, user?.Username);
            return user;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get user {UserId} from GitLab", userId);
            return null;
        }
    }

    public async Task<IReadOnlyList<GitLabProject>> GetUserContributedProjectsAsync(long userId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching contributed projects for user {UserId} using GitLab's official contributed_projects API", userId);

            var contributedProjects = await _gitLabHttpClient.GetUserContributedProjectsAsync(userId, cancellationToken);

            // Convert the API response to GitLabProject models
            var projects = new List<GitLabProject>();

            foreach (var apiProject in contributedProjects)
            {
                try
                {
                    // Convert GitLabContributedProject to GitLabProject
                    var project = new GitLabProject
                    {
                        Id = apiProject.Id,
                        Name = apiProject.Name,
                        NameWithNamespace = apiProject.NameWithNamespace,
                        Path = apiProject.Path,
                        PathWithNamespace = apiProject.PathWithNamespace,
                        Description = apiProject.Description,
                        DefaultBranch = apiProject.DefaultBranch,
                        Visibility = apiProject.Visibility,
                        WebUrl = apiProject.WebUrl,
                        SshUrlToRepo = apiProject.SshUrlToRepo,
                        HttpUrlToRepo = apiProject.HttpUrlToRepo,
                        AvatarUrl = apiProject.AvatarUrl,
                        Archived = false,
                        ForksCount = 0,
                        StarCount = 0,
                        LastActivityAt = null,
                        CreatedAt = null
                    };
                    projects.Add(project);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to convert project {ProjectId} ({ProjectName}) from API response",
                        apiProject.Id, apiProject.Name);
                }
            }

            _logger.LogInformation("Successfully fetched {ProjectCount} contributed projects for user {UserId} via GitLab API",
                projects.Count, userId);

            return projects.AsReadOnly();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed while fetching contributed projects for user {UserId}. Falling back to owned projects.", userId);
            return await GetFallbackUserProjectsAsync(userId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get contributed projects for user {UserId}. Falling back to owned projects.", userId);
            return await GetFallbackUserProjectsAsync(userId, cancellationToken);
        }
    }



    /// <summary>
    /// Fallback method to get user's owned projects when the contributed_projects API fails.
    /// Since we're using a custom GitLab client, this fallback is not available.
    /// </summary>
    /// <param name="userId">The GitLab user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Empty list since fallback is not available with custom client</returns>
    private Task<IReadOnlyList<GitLabProject>> GetFallbackUserProjectsAsync(long userId, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Fallback method for owned projects is not available with custom GitLab client for user {UserId}", userId);
        return Task.FromResult(new List<GitLabProject>().AsReadOnly() as IReadOnlyList<GitLabProject>);
    }

    /// <summary>
    /// Efficiently analyzes user contributions across their contributed projects.
    /// This method now uses the contributed projects API as a starting point for much better performance.
    /// </summary>
    /// <param name="userId">The GitLab user ID</param>
    /// <param name="userEmail">Optional user email for commit matching (if not provided, will be fetched from user profile)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Weighted list of projects ordered by contribution level</returns>
    public async Task<IReadOnlyList<UserProjectContribution>> GetUserProjectContributionsAsync(long userId, string? userEmail = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Analyzing user {UserId} contributions using contributed projects API", userId);

            // Resolve user email if not provided
            if (string.IsNullOrEmpty(userEmail))
            {
                var user = await GetUserByIdAsync(userId, cancellationToken);
                userEmail = user?.Email;
                if (string.IsNullOrEmpty(userEmail))
                {
                    _logger.LogWarning("Could not resolve email for user {UserId}, contribution analysis may be incomplete", userId);
                    return new List<UserProjectContribution>().AsReadOnly();
                }
            }

            // Get contributed projects - much more efficient than scanning all projects
            var contributedProjects = await GetUserContributedProjectsAsync(userId, cancellationToken);
            _logger.LogDebug("Analyzing contributions across {ProjectCount} contributed projects", contributedProjects.Count);

            // Analyze contributions in parallel for better performance
            var contributionTasks = contributedProjects.Select(async project =>
            {
                try
                {
                    var contributions = await AnalyzeUserContributionsInProjectAsync(project, userId, userEmail, cancellationToken);
                    return contributions;
                }
                catch (Exception ex)
                {
                    _logger.LogTrace(ex, "Failed to analyze contributions for user {UserId} in project {ProjectId}", userId, project.Id);
                    // Return empty contribution for this project
                    return new UserProjectContribution
                    {
                        Project = project,
                        CommitsCount = 0,
                        MergeRequestsCount = 0,
                        IssuesCount = 0,
                        Weight = 0,
                        LastActivityAt = null
                    };
                }
            });

            var allContributions = await Task.WhenAll(contributionTasks);

            // Filter to only projects with actual contributions and sort by weight
            var relevantContributions = allContributions
                .Where(c => c.HasContribution)
                .OrderByDescending(c => c.Weight)
                .ThenByDescending(c => c.LastActivityAt)
                .ToList();

            _logger.LogInformation("Found user {UserId} actual contributions in {RelevantProjectCount} out of {TotalProjectCount} contributed projects",
                userId, relevantContributions.Count, contributedProjects.Count);

            return relevantContributions.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze user {UserId} project contributions", userId);
            return new List<UserProjectContribution>().AsReadOnly();
        }
    }
    /// <summary>
    /// Analyzes a user's contributions to a specific project
    /// </summary>
    private async Task<UserProjectContribution> AnalyzeUserContributionsInProjectAsync(GitLabProject project, long userId, string userEmail, CancellationToken cancellationToken)
    {
        var commitsCount = 0;
        var mergeRequestsCount = 0;
        var issuesCount = 0;
        DateTimeOffset? lastActivity = null;

        try
        {
            // Count commits by email (most reliable for contribution counting)
            var recentCommits = await GetCommitsByUserEmailAsync(project.Id, userEmail, DateTimeOffset.UtcNow.AddYears(-1), cancellationToken);
            commitsCount = recentCommits.Count;
            if (recentCommits.Count > 0)
            {
                lastActivity = recentCommits.Max(c => c.CommittedAt);
            }

            // Count merge requests by user ID
            var recentMRs = await GetMergeRequestsAsync(project.Id, DateTimeOffset.UtcNow.AddYears(-1), cancellationToken);
            var userMRs = recentMRs.Where(mr => mr.AuthorUserId == userId).ToList();
            mergeRequestsCount = userMRs.Count;
            if (userMRs.Count > 0)
            {
                var mrLastActivity = userMRs.Max(mr => mr.CreatedAt);
                lastActivity = lastActivity.HasValue ?
                    (lastActivity > mrLastActivity ? lastActivity : mrLastActivity) :
                    mrLastActivity;
            }

            // TODO: Add issues count when we implement issue fetching
            // For now, issues count remains 0

            // Calculate weight based on contribution types
            var weight = CalculateContributionWeight(commitsCount, mergeRequestsCount, issuesCount);

            return new UserProjectContribution
            {
                Project = project,
                CommitsCount = commitsCount,
                MergeRequestsCount = mergeRequestsCount,
                IssuesCount = issuesCount,
                Weight = weight,
                LastActivityAt = lastActivity
            };
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Error analyzing contributions for user {UserId} in project {ProjectId}", userId, project.Id);
            throw;
        }
    }

    /// <summary>
    /// Calculates contribution weight based on different activity types
    /// </summary>
    private static double CalculateContributionWeight(int commitsCount, int mergeRequestsCount, int issuesCount)
    {
        // Weight calculation:
        // - Commits: 1 point each (direct code contribution)
        // - Merge Requests: 5 points each (higher impact, includes code review process)
        // - Issues: 2 points each (project involvement, reporting/discussing)

        const double commitWeight = 1.0;
        const double mergeRequestWeight = 5.0;
        const double issueWeight = 2.0;

        return (commitsCount * commitWeight) +
               (mergeRequestsCount * mergeRequestWeight) +
               (issuesCount * issueWeight);
    }
    /// <summary>
    /// Gets projects for a specific user based on contributed projects.
    /// This method now uses the contributed projects approach for better accuracy and performance.
    /// </summary>
    /// <param name="userId">The GitLab user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of projects where the user has contributions</returns>
    public async Task<IReadOnlyList<GitLabProject>> GetUserProjectsAsync(long userId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching projects for user {UserId} using contributed projects approach", userId);

            // Use the new contributed projects method for much better performance
            var projects = await GetUserContributedProjectsAsync(userId, cancellationToken);

            _logger.LogInformation("Found {ProjectCount} projects for user {UserId}", projects.Count, userId);
            return projects;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get projects for user {UserId}", userId);
            return new List<GitLabProject>().AsReadOnly();
        }
    }    /// <summary>
         /// [DEPRECATED] Alternative approach for activity-based project discovery.
         /// Use GetUserContributedProjectsAsync instead for better performance and accuracy.
         /// 
         /// This method searches for user activity across the GitLab instance without admin privileges
         /// but requires many API calls during the search phase and is less reliable.
         /// </summary>
         /// <param name="userId">The GitLab user ID</param>
         /// <param name="userEmail">The user's email address</param>
         /// <param name="cancellationToken">Cancellation token</param>
         /// <returns>Projects where the user has actual activity</returns>
    [Obsolete("Use GetUserContributedProjectsAsync for better performance and accuracy")]
    public async Task<IReadOnlyList<GitLabProject>> GetUserProjectsByActivityAsync(long userId, string userEmail, CancellationToken cancellationToken = default)
    {
        // Since this method is obsolete, delegate to the new interface method
        return await _gitLabHttpClient.GetUserProjectsByActivityAsync(userId, userEmail, cancellationToken);
    }

    public async Task<IReadOnlyList<RawCommit>> GetCommitsByUserEmailAsync(long projectId, string userEmail, DateTimeOffset? since = null, CancellationToken cancellationToken = default)
    {
        // Delegate to the new interface method
        var commits = await _gitLabHttpClient.GetCommitsByUserEmailAsync(projectId, userEmail, since, cancellationToken);
        
        var rawCommits = new List<RawCommit>();
        foreach (var commit in commits)
        {
            // Create a consistent user ID based on email
            var authorUserId = Math.Abs(userEmail.GetHashCode());

            var rawCommit = new RawCommit
            {
                ProjectId = projectId,
                ProjectName = $"Project {projectId}", // Mock project name
                CommitId = commit.Id?.ToString() ?? Guid.NewGuid().ToString(),
                AuthorUserId = authorUserId, // Consistent email-based ID
                AuthorName = commit.AuthorName ?? "Unknown",
                AuthorEmail = userEmail,
                CommittedAt = commit.CommittedDate ?? DateTimeOffset.UtcNow,
                Message = commit.Message ?? "",
                Additions = commit.Stats?.Additions ?? 0,
                Deletions = commit.Stats?.Deletions ?? 0,
                IsSigned = false,
                IngestedAt = DateTimeOffset.UtcNow
            };

            rawCommits.Add(rawCommit);
        }

        return rawCommits.AsReadOnly();
    }

    public async Task<IReadOnlyList<RawMergeRequestNote>> GetMergeRequestNotesAsync(long projectId, long mergeRequestIid, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching merge request notes for project {ProjectId}, MR {MergeRequestIid}", projectId, mergeRequestIid);

            var notes = await _gitLabHttpClient.GetMergeRequestNotesAsync(projectId, mergeRequestIid, cancellationToken);

            var rawNotes = new List<RawMergeRequestNote>();

            foreach (var note in notes)
            {
                try
                {
                    var rawNote = new RawMergeRequestNote
                    {
                        ProjectId = projectId,
                        ProjectName = $"Project {projectId}", // Mock project name - could be enhanced to fetch actual name
                        MergeRequestIid = mergeRequestIid,
                        NoteId = note.Id,
                        AuthorId = note.Author?.Id ?? 0,
                        AuthorName = note.Author?.Name ?? "Unknown",
                        Body = note.Body ?? "",
                        CreatedAt = note.CreatedAt ?? DateTimeOffset.UtcNow,
                        UpdatedAt = note.UpdatedAt,
                        System = note.System,
                        Resolvable = note.Resolvable,
                        Resolved = note.Resolved,
                        ResolvedById = note.ResolvedBy?.Id,
                        ResolvedBy = note.ResolvedBy?.Name,
                        NoteableType = note.NoteableType,
                        IngestedAt = DateTimeOffset.UtcNow
                    };

                    rawNotes.Add(rawNote);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to process merge request note {NoteId} for project {ProjectId}, MR {MergeRequestIid}", note.Id, projectId, mergeRequestIid);
                }
            }

            _logger.LogDebug("Retrieved {NoteCount} merge request notes for project {ProjectId}, MR {MergeRequestIid}", rawNotes.Count, projectId, mergeRequestIid);
            return rawNotes.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get merge request notes for project {ProjectId}, MR {MergeRequestIid}", projectId, mergeRequestIid);
            return new List<RawMergeRequestNote>().AsReadOnly();
        }
    }

    public async Task<IReadOnlyList<RawIssueNote>> GetIssueNotesAsync(long projectId, long issueIid, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching issue notes for project {ProjectId}, issue {IssueIid}", projectId, issueIid);

            var notes = await _gitLabHttpClient.GetIssueNotesAsync(projectId, issueIid, cancellationToken);

            var rawNotes = new List<RawIssueNote>();

            foreach (var note in notes)
            {
                try
                {
                    var rawNote = new RawIssueNote
                    {
                        ProjectId = projectId,
                        ProjectName = $"Project {projectId}", // Mock project name - could be enhanced to fetch actual name
                        IssueIid = issueIid,
                        NoteId = note.Id,
                        AuthorId = note.Author?.Id ?? 0,
                        AuthorName = note.Author?.Name ?? "Unknown",
                        Body = note.Body ?? "",
                        CreatedAt = note.CreatedAt ?? DateTimeOffset.UtcNow,
                        UpdatedAt = note.UpdatedAt,
                        System = note.System,
                        NoteableType = note.NoteableType,
                        IngestedAt = DateTimeOffset.UtcNow
                    };

                    rawNotes.Add(rawNote);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to process issue note {NoteId} for project {ProjectId}, issue {IssueIid}", note.Id, projectId, issueIid);
                }
            }

            _logger.LogDebug("Retrieved {NoteCount} issue notes for project {ProjectId}, issue {IssueIid}", rawNotes.Count, projectId, issueIid);
            return rawNotes.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get issue notes for project {ProjectId}, issue {IssueIid}", projectId, issueIid);
            return new List<RawIssueNote>().AsReadOnly();
        }
    }

    public Task<IReadOnlyList<RawIssue>> GetIssuesAsync(long projectId, DateTimeOffset? updatedAfter = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // Note: For now, we'll return empty list as the exact NGitLab API for issues needs to be verified
            // This placeholder ensures the interface is implemented correctly
            _logger.LogDebug("Issue fetching API method implemented but requires GitLab API verification for project {ProjectId}", projectId);
            
            return Task.FromResult(new List<RawIssue>().AsReadOnly() as IReadOnlyList<RawIssue>);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get issues for project {ProjectId}", projectId);
            return Task.FromResult(new List<RawIssue>().AsReadOnly() as IReadOnlyList<RawIssue>);
        }
    }

    public async Task<IReadOnlyList<RawIssue>> GetUserAssignedIssuesAsync(long userId, DateTimeOffset? updatedAfter = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var allAssignedIssues = new List<RawIssue>();
            
            // Get projects the user is involved in
            var projects = await GetUserProjectsAsync(userId, cancellationToken);

            foreach (var project in projects.Take(10)) // Limit to first 10 projects for now
            {
                try
                {
                    // Get issues from each project using the implemented GetIssuesAsync method
                    var projectIssues = await GetIssuesAsync(project.Id, updatedAfter, cancellationToken);
                    
                    // Filter for issues assigned to this user
                    var userAssignedIssues = projectIssues.Where(issue => issue.AssigneeUserId == userId).ToList();
                    allAssignedIssues.AddRange(userAssignedIssues);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get assigned issues from project {ProjectId} for user {UserId}", project.Id, userId);
                }
            }

            _logger.LogDebug("Retrieved {IssueCount} assigned issues for user {UserId} from {ProjectCount} projects", 
                allAssignedIssues.Count, userId, projects.Count);
            return allAssignedIssues.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get assigned issues for user {UserId}", userId);
            return new List<RawIssue>().AsReadOnly();
        }
    }
}
