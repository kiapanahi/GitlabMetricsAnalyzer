using Microsoft.Extensions.Options;

using NGitLab;
using NGitLab.Models;

using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Configuration;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure;

public sealed class GitLabService : IGitLabService
{
    private readonly IGitLabClient _gitLabClient;
    private readonly IGitLabHttpClient _gitLabHttpClient;
    private readonly ILogger<GitLabService> _logger;

    public GitLabService(
        IGitLabClient gitLabClient,
        IGitLabHttpClient gitLabHttpClient,
        IOptions<GitLabConfiguration> configuration,
        ILogger<GitLabService> logger)
    {
        _gitLabClient = gitLabClient;
        _gitLabHttpClient = gitLabHttpClient;
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

    public Task<IReadOnlyList<Project>> GetProjectsAsync(CancellationToken cancellationToken = default)
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

        return Task.FromResult(projects.AsReadOnly() as IReadOnlyList<Project>);
    }

    public Task<IReadOnlyList<Project>> GetGroupProjectsAsync(long groupId, CancellationToken cancellationToken = default)
    {
        // Simplified - just return empty list for now
        _logger.LogDebug("Group projects retrieval not implemented yet for group {GroupId}", groupId);
        return Task.FromResult(new List<Project>().AsReadOnly() as IReadOnlyList<Project>);
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

    public Task<IReadOnlyList<RawMergeRequest>> GetMergeRequestsAsync(long projectId, DateTimeOffset? updatedAfter = null, CancellationToken cancellationToken = default)
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
            return Task.FromResult(rawMergeRequests.AsReadOnly() as IReadOnlyList<RawMergeRequest>);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get merge requests for project {ProjectId}", projectId);
            return Task.FromResult(new List<RawMergeRequest>().AsReadOnly() as IReadOnlyList<RawMergeRequest>);
        }
    }

    public Task<IReadOnlyList<RawPipeline>> GetPipelinesAsync(long projectId, DateTimeOffset? updatedAfter = null, CancellationToken cancellationToken = default)
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
            return Task.FromResult(rawPipelines.AsReadOnly() as IReadOnlyList<RawPipeline>);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get pipelines for project {ProjectId}", projectId);
            return Task.FromResult(new List<RawPipeline>().AsReadOnly() as IReadOnlyList<RawPipeline>);
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

    /// <summary>
    /// Gets projects that a user has contributed to using GitLab's official contributed_projects API.
    /// This is the most efficient and accurate way to get user-project relationships.
    /// </summary>
    /// <param name="userId">The GitLab user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of projects the user has contributed to</returns>
    public async Task<IReadOnlyList<Project>> GetUserContributedProjectsAsync(long userId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching contributed projects for user {UserId} using GitLab's official contributed_projects API", userId);

            // Use the HTTP client to call the official GitLab API
            var contributedProjectsApi = await _gitLabHttpClient.GetUserContributedProjectsAsync(userId, cancellationToken);

            // Convert the API response to NGitLab Project models
            var projects = new List<Project>();

            foreach (var apiProject in contributedProjectsApi)
            {
                try
                {
                    // Convert GitLabContributedProject to NGitLab Project
                    var project = ConvertToNGitLabProject(apiProject);
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
    /// Converts a GitLabContributedProject from the API response to an NGitLab Project model.
    /// </summary>
    /// <param name="apiProject">The API project response</param>
    /// <returns>NGitLab Project model</returns>
    private static Project ConvertToNGitLabProject(GitLabContributedProject apiProject)
    {
        return new Project
        {
            Id = (int)apiProject.Id,
            Name = apiProject.Name,
            NameWithNamespace = apiProject.NameWithNamespace,
            Path = apiProject.Path,
            PathWithNamespace = apiProject.PathWithNamespace,
            Description = apiProject.Description,
            DefaultBranch = apiProject.DefaultBranch,
            WebUrl = apiProject.WebUrl,
            AvatarUrl = apiProject.AvatarUrl,
            ForksCount = apiProject.ForksCount,
            StarCount = apiProject.StarCount,
            LastActivityAt = apiProject.LastActivityAt,
            CreatedAt = apiProject.CreatedAt,
            Archived = apiProject.Archived,
            // Use the non-deprecated Topics property
            Topics = apiProject.Topics.ToArray(),
            // Basic namespace mapping
            Namespace = apiProject.Namespace is not null ? new Namespace
            {
                Id = (int)apiProject.Namespace.Id,
                Name = apiProject.Namespace.Name,
                Path = apiProject.Namespace.Path,
                Kind = apiProject.Namespace.Kind,
                FullPath = apiProject.Namespace.FullPath
            } : null
        };
    }

    /// <summary>
    /// Fallback method to get user's owned projects when the contributed_projects API fails.
    /// </summary>
    /// <param name="userId">The GitLab user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of user's owned projects</returns>
    private Task<IReadOnlyList<Project>> GetFallbackUserProjectsAsync(long userId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogWarning("Using fallback method to get owned projects for user {UserId}", userId);

            var ownedProjects = _gitLabClient.Projects.Get(new ProjectQuery
            {
                UserId = (int)userId,
                PerPage = 100
            }).ToList();

            _logger.LogInformation("Fallback: Found {ProjectCount} owned projects for user {UserId}",
                ownedProjects.Count, userId);

            return Task.FromResult(ownedProjects.AsReadOnly() as IReadOnlyList<Project>);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallback method also failed for user {UserId}", userId);
            return Task.FromResult(new List<Project>().AsReadOnly() as IReadOnlyList<Project>);
        }
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
    private async Task<UserProjectContribution> AnalyzeUserContributionsInProjectAsync(Project project, long userId, string userEmail, CancellationToken cancellationToken)
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
    public async Task<IReadOnlyList<Project>> GetUserProjectsAsync(long userId, CancellationToken cancellationToken = default)
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
            return new List<Project>().AsReadOnly();
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
    public async Task<IReadOnlyList<Project>> GetUserProjectsByActivityAsync(long userId, string userEmail, CancellationToken cancellationToken = default)
    {
        var projectsWithActivity = new HashSet<long>();
        var userProjects = new List<Project>();

        try
        {
            _logger.LogDebug("Searching for user {UserId} activity across accessible projects", userId);

            // Get accessible projects to search through
            var searchProjects = _gitLabClient.Projects.Get(new ProjectQuery { PerPage = 100 }).ToList();

            // Search for user activity in each project (parallel for better performance)
            var activityTasks = searchProjects.Select(async project =>
            {
                try
                {
                    var hasActivity = false;

                    // Check for commits by this user (quick check)
                    var recentCommits = await GetCommitsByUserEmailAsync(project.Id, userEmail, DateTimeOffset.UtcNow.AddDays(-90), cancellationToken);
                    if (recentCommits.Count > 0)
                    {
                        hasActivity = true;
                        _logger.LogDebug("Found {CommitCount} commits for user {UserId} in project {ProjectId}",
                            recentCommits.Count, userId, project.Id);
                    }

                    // Check for merge requests by this user
                    if (!hasActivity)
                    {
                        var recentMRs = await GetMergeRequestsAsync(project.Id, DateTimeOffset.UtcNow.AddDays(-90), cancellationToken);
                        if (recentMRs.Any(mr => mr.AuthorUserId == userId))
                        {
                            hasActivity = true;
                            var userMRCount = recentMRs.Count(mr => mr.AuthorUserId == userId);
                            _logger.LogDebug("Found {MRCount} merge requests for user {UserId} in project {ProjectId}",
                                userMRCount, userId, project.Id);
                        }
                    }

                    if (hasActivity)
                    {
                        lock (projectsWithActivity)
                        {
                            projectsWithActivity.Add(project.Id);
                            userProjects.Add(project);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogTrace(ex, "Could not check activity for user {UserId} in project {ProjectId}", userId, project.Id);
                }
            });

            // Wait for all activity checks to complete (with reasonable timeout)
            await Task.WhenAll(activityTasks);

            _logger.LogInformation("Found user {UserId} activity in {ProjectCount} projects out of {SearchedCount} searched",
                userId, userProjects.Count, searchProjects.Count);

            return userProjects.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find projects by activity for user {UserId}", userId);
            return new List<Project>().AsReadOnly();
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
