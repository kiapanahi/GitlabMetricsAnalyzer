using System.Net;
using System.Text.Json;

using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;

using GitLabCommit = Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw.GitLabCommit;
using GitLabCommitStats = Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw.GitLabCommitStats;
using GitLabMergeRequest = Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw.GitLabMergeRequest;
using GitLabPipeline = Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw.GitLabPipeline;
using GitLabProject = Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw.GitLabProject;
using GitLabUser = Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw.GitLabUser;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure;

/// <summary>
/// HTTP client service for making direct GitLab API calls that replace NGitLab library functionality.
/// </summary>
public interface IGitLabHttpClient
{
    /// <summary>
    /// Tests the connection to GitLab API.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if connection is successful</returns>
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all accessible projects.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of projects</returns>
    Task<IReadOnlyList<GitLabProject>> GetProjectsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets projects for a specific group.
    /// </summary>
    /// <param name="groupId">The group ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of projects in the group</returns>
    Task<IReadOnlyList<GitLabProject>> GetGroupProjectsAsync(long groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific project by ID.
    /// </summary>
    /// <param name="projectId">The project ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The project or null if not found</returns>
    Task<GitLabProject?> GetProjectByIdAsync(long projectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets commits for a specific project.
    /// </summary>
    /// <param name="projectId">The project ID</param>
    /// <param name="since">Optional date filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of commits</returns>
    Task<IReadOnlyList<GitLabCommit>> GetCommitsAsync(long projectId, DateTimeOffset? since = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets merge requests for a specific project.
    /// </summary>
    /// <param name="projectId">The project ID</param>
    /// <param name="updatedAfter">Optional date filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of merge requests</returns>
    Task<IReadOnlyList<GitLabMergeRequest>> GetMergeRequestsAsync(long projectId, DateTimeOffset? updatedAfter = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets commits for a specific merge request.
    /// </summary>
    /// <param name="projectId">The project ID</param>
    /// <param name="mergeRequestIid">The merge request IID (internal ID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of commits in the merge request</returns>
    Task<IReadOnlyList<GitLabCommit>> GetMergeRequestCommitsAsync(long projectId, long mergeRequestIid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets pipelines for a specific project.
    /// </summary>
    /// <param name="projectId">The project ID</param>
    /// <param name="updatedAfter">Optional date filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of pipelines</returns>
    Task<IReadOnlyList<GitLabPipeline>> GetPipelinesAsync(long projectId, DateTimeOffset? updatedAfter = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all users.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of users</returns>
    Task<IReadOnlyList<GitLabUser>> GetUsersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user by ID.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The user or null if not found</returns>
    Task<GitLabUser?> GetUserByIdAsync(long userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets projects that a user has contributed to using GitLab's /users/:user_id/contributed_projects API.
    /// </summary>
    /// <param name="userId">The GitLab user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of projects the user has contributed to</returns>
    Task<IReadOnlyList<GitLabContributedProject>> GetUserContributedProjectsAsync(long userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets user project contributions.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="userEmail">Optional user email</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of user project contributions</returns>
    Task<IReadOnlyList<GitLabUserProjectContribution>> GetUserProjectContributionsAsync(long userId, string? userEmail = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets projects owned by a user.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of user projects</returns>
    Task<IReadOnlyList<GitLabProject>> GetUserProjectsAsync(long userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets projects a user has contributed to based on activity.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="userEmail">The user email</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of projects by activity</returns>
    Task<IReadOnlyList<GitLabProject>> GetUserProjectsByActivityAsync(long userId, string userEmail, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets commits by user email for a specific project.
    /// </summary>
    /// <param name="projectId">The project ID</param>
    /// <param name="userEmail">The user email</param>
    /// <param name="since">Optional date filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of commits by the user</returns>
    Task<IReadOnlyList<GitLabCommit>> GetCommitsByUserEmailAsync(long projectId, string userEmail, DateTimeOffset? since = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets merge request notes/discussions for a specific project and merge request.
    /// </summary>
    /// <param name="projectId">The project ID</param>
    /// <param name="mergeRequestIid">The merge request IID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of merge request notes</returns>
    Task<IReadOnlyList<GitLabMergeRequestNote>> GetMergeRequestNotesAsync(long projectId, long mergeRequestIid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets contribution events for a user.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="after">Optional date to filter events after this date</param>
    /// <param name="before">Optional date to filter events before this date</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of user events</returns>
    Task<IReadOnlyList<GitLabEvent>> GetUserEventsAsync(long userId, DateTimeOffset? after = null, DateTimeOffset? before = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets changes (diff stats) for a specific merge request.
    /// </summary>
    /// <param name="projectId">The project ID</param>
    /// <param name="mergeRequestIid">The merge request IID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Merge request with changes including additions and deletions</returns>
    Task<GitLabMergeRequestChanges?> GetMergeRequestChangesAsync(long projectId, long mergeRequestIid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets discussions (threaded comments) for a specific merge request.
    /// </summary>
    /// <param name="projectId">The project ID</param>
    /// <param name="mergeRequestIid">The merge request IID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of discussions</returns>
    Task<IReadOnlyList<GitLabDiscussion>> GetMergeRequestDiscussionsAsync(long projectId, long mergeRequestIid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets approval state for a specific merge request.
    /// </summary>
    /// <param name="projectId">The project ID</param>
    /// <param name="mergeRequestIid">The merge request IID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Approval state information</returns>
    Task<GitLabMergeRequestApprovals?> GetMergeRequestApprovalsAsync(long projectId, long mergeRequestIid, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of GitLab HTTP client for direct API calls.
/// </summary>
public sealed class GitLabHttpClient(HttpClient httpClient, ILogger<GitLabHttpClient> logger) : IGitLabHttpClient
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly ILogger<GitLabHttpClient> _logger = logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    /// <summary>
    /// Makes a paginated API request to GitLab
    /// </summary>
    private async Task<List<T>> GetPaginatedAsync<T>(string endpoint, CancellationToken cancellationToken = default, Dictionary<string, string>? queryParams = null)
    {
        var allItems = new List<T>();
        var page = 1;
        const int perPage = 100; // GitLab's maximum per page

        while (true)
        {
            var url = $"{endpoint}?page={page}&per_page={perPage}";

            if (queryParams is not null)
            {
                foreach (var kvp in queryParams)
                {
                    url += $"&{kvp.Key}={Uri.EscapeDataString(kvp.Value)}";
                }
            }

            _logger.LogDebug("Making paginated request to: {Url}", url);

            var response = await _httpClient.GetAsync(url, cancellationToken);

            // Check for rate limiting
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var retryAfter = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(60);
                _logger.LogWarning("GitLab API rate limit hit. Waiting {RetryAfter} seconds", retryAfter.TotalSeconds);
                await Task.Delay(retryAfter, cancellationToken);
                continue; // Retry the same page
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("GitLab API request failed with status {StatusCode}: {ErrorContent}", response.StatusCode, errorContent);
                throw new HttpRequestException($"GitLab API request failed with status {response.StatusCode}: {errorContent}");
            }

            // Log rate limit headers for monitoring
            LogRateLimitHeaders(response);

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var items = JsonSerializer.Deserialize<List<T>>(content, JsonOptions) ?? new List<T>();

            _logger.LogTrace("Retrieved {ItemCount} items from page {Page}", items.Count, page);

            allItems.AddRange(items);

            // Check if we've reached the last page
            if (items.Count < perPage)
            {
                break;
            }

            page++;

            // Add a small delay between requests to be respectful of GitLab API
            await Task.Delay(50, cancellationToken);
        }

        _logger.LogDebug("Retrieved total of {TotalCount} items from {TotalPages} pages", allItems.Count, page);
        return allItems;
    }

    /// <summary>
    /// Logs GitLab API rate limit headers for monitoring
    /// </summary>
    private void LogRateLimitHeaders(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("RateLimit-Limit", out var limitValues) &&
            response.Headers.TryGetValues("RateLimit-Remaining", out var remainingValues) &&
            response.Headers.TryGetValues("RateLimit-Reset", out var resetValues))
        {
            var limit = limitValues.FirstOrDefault();
            var remaining = remainingValues.FirstOrDefault();
            var reset = resetValues.FirstOrDefault();

            _logger.LogDebug("GitLab API Rate Limit: {Remaining}/{Limit} requests remaining, resets at {Reset}",
                remaining, limit, reset);

            // Warn when getting close to rate limit
            if (int.TryParse(remaining, out var remainingInt) && remainingInt < 10)
            {
                _logger.LogWarning("GitLab API rate limit nearly exhausted: {Remaining} requests remaining", remaining);
            }
        }
    }

    /// <summary>
    /// Maps GitLab DTO models to domain models
    /// </summary>
    private static GitLabProject MapToProject(DTOs.GitLabProject dto)
    {
        return new GitLabProject
        {
            Id = dto.Id,
            Name = dto.PathWithNamespace?.Split('/').LastOrDefault() ?? "Unknown",
            NameWithNamespace = dto.PathWithNamespace,
            Path = dto.PathWithNamespace?.Split('/').LastOrDefault() ?? "unknown",
            PathWithNamespace = dto.PathWithNamespace,
            Description = string.Empty, // Not available in simple DTO
            DefaultBranch = dto.DefaultBranch,
            Visibility = dto.Visibility,
            Archived = false, // Need to be determined from full project data
            WebUrl = string.Empty, // Not available in simple DTO
            ForksCount = 0, // Not available in simple DTO
            StarCount = 0, // Not available in simple DTO
            CreatedAt = DateTime.MinValue, // Not available in simple DTO
            LastActivityAt = dto.LastActivityAt.DateTime,
            Owner = null // Would need separate call to get owner info
        };
    }

    private static GitLabUser MapToUser(DTOs.GitLabUser dto)
    {
        return new GitLabUser
        {
            Id = dto.Id,
            Username = dto.Username,
            Email = dto.Email,
            Name = dto.Name,
            State = dto.State,
            AvatarUrl = string.Empty, // Not available in simple DTO
            WebUrl = string.Empty, // Not available in simple DTO
            CreatedAt = null, // Not available in simple DTO
            IsAdmin = false, // Not available in simple DTO
            CanCreateGroup = false, // Not available in simple DTO
            CanCreateProject = false, // Not available in simple DTO
            TwoFactorEnabled = false, // Not available in simple DTO
            External = false, // Not available in simple DTO
            PrivateProfile = false // Not available in simple DTO
        };
    }

    private static GitLabCommit MapToCommit(DTOs.GitLabCommit dto, long projectId)
    {
        return new GitLabCommit
        {
            Id = dto.Id,
            ShortId = dto.Id[..Math.Min(8, dto.Id.Length)], // Take first 8 characters as short ID
            Title = string.Empty, // Not available in stats DTO
            Message = string.Empty, // Not available in stats DTO
            AuthorName = dto.AuthorName,
            AuthorEmail = dto.AuthorEmail,
            CommitterName = dto.AuthorName, // Assuming author is committer
            CommitterEmail = dto.AuthorEmail,
            AuthoredDate = dto.CommittedDate.DateTime,
            CommittedDate = dto.CommittedDate.DateTime,
            Stats = dto.Stats is not null ? new GitLabCommitStats
            {
                Additions = dto.Stats.Additions,
                Deletions = dto.Stats.Deletions,
                Total = dto.Stats.Additions + dto.Stats.Deletions
            } : null,
            Status = "success", // Default status
            ProjectId = projectId
        };
    }

    private static GitLabMergeRequest MapToMergeRequest(DTOs.GitLabMergeRequest dto)
    {
        return new GitLabMergeRequest
        {
            Id = dto.Id,
            Iid = dto.Iid,
            ProjectId = dto.ProjectId,
            Title = dto.Title ?? string.Empty,
            Description = string.Empty, // Not available in simple DTO
            State = dto.State,
            CreatedAt = dto.CreatedAt.DateTime,
            UpdatedAt = dto.UpdatedAt.DateTime,
            MergedAt = dto.MergedAt?.DateTime,
            ClosedAt = dto.ClosedAt?.DateTime,
            TargetBranch = dto.TargetBranch,
            SourceBranch = dto.SourceBranch,
            Author = MapToUser(dto.Author),
            WorkInProgress = false, // Not available in simple DTO
            HasConflicts = dto.HasConflicts,
            ChangesCount = dto.ChangesCount ?? "0",
            MergeStatus = "can_be_merged", // Default status
            WebUrl = string.Empty, // Not available in simple DTO
            Labels = dto.Labels
        };
    }

    private static GitLabPipeline MapToPipeline(DTOs.GitLabPipeline dto, long projectId)
    {
        return new GitLabPipeline
        {
            Id = dto.Id,
            ProjectId = projectId,
            Sha = dto.Sha,
            Ref = dto.Ref,
            Status = dto.Status,
            Source = "push", // Default source, not available in simple DTO
            CreatedAt = dto.CreatedAt.DateTime,
            UpdatedAt = dto.UpdatedAt.DateTime,
            WebUrl = string.Empty // Not available in simple DTO
        };
    }

    /// <summary>
    /// Gets projects that a user has contributed to using GitLab's official API endpoint.
    /// </summary>
    /// <param name="userId">The GitLab user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of projects the user has contributed to</returns>
    public async Task<IReadOnlyList<GitLabContributedProject>> GetUserContributedProjectsAsync(long userId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching contributed projects for user {UserId} via direct GitLab API call", userId);

            // Construct the URL for the contributed projects endpoint
            var url = $"users/{userId}/contributed_projects?simple=true&per_page=100";

            _logger.LogDebug("Making HTTP GET request to: {Url}", url);

            // Make the HTTP request
            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("GitLab API request failed with status {StatusCode}: {ErrorContent}",
                    response.StatusCode, errorContent);

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("User {UserId} not found or has no contributed projects", userId);
                    return new List<GitLabContributedProject>().AsReadOnly();
                }

                throw new HttpRequestException($"GitLab API request failed with status {response.StatusCode}: {errorContent}");
            }

            // Read and deserialize the response
            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogTrace("GitLab API response: {JsonContent}", jsonContent);

            var contributedProjects = JsonSerializer.Deserialize<List<GitLabContributedProject>>(jsonContent, JsonOptions);

            if (contributedProjects is null)
            {
                _logger.LogWarning("Failed to deserialize GitLab API response for user {UserId}", userId);
                return new List<GitLabContributedProject>().AsReadOnly();
            }

            _logger.LogInformation("Successfully fetched {ProjectCount} contributed projects for user {UserId} via GitLab API",
                contributedProjects.Count, userId);

            return contributedProjects.AsReadOnly();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed while fetching contributed projects for user {UserId}", userId);
            throw;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize GitLab API response for user {UserId}", userId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while fetching contributed projects for user {UserId}", userId);
            throw;
        }
    }

    // GitLab API client implementations
    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Testing GitLab API connection");

            // Use GitLab API version endpoint to test connectivity
            var response = await _httpClient.GetAsync("version", cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogInformation("GitLab API connection test successful. Version info: {VersionInfo}", content);
                return true;
            }
            else
            {
                _logger.LogWarning("GitLab API connection test failed with status code: {StatusCode}", response.StatusCode);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GitLab API connection test failed with exception");
            return false;
        }
    }

    public async Task<IReadOnlyList<GitLabProject>> GetProjectsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching ALL projects (including archived) via GitLab API");

            var projectDtos = await GetPaginatedAsync<DTOs.GitLabProject>("projects", cancellationToken,
                new Dictionary<string, string>
                {
                    {"simple", "true"}
                });

            var projects = projectDtos.Select(MapToProject).ToList();

            _logger.LogInformation("Successfully fetched {ProjectCount} projects (including archived) via GitLab API", projects.Count);
            return projects.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch projects via GitLab API");
            throw;
        }
    }

    public async Task<IReadOnlyList<GitLabProject>> GetGroupProjectsAsync(long groupId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching projects for group {GroupId} via GitLab API", groupId);

            var projectDtos = await GetPaginatedAsync<DTOs.GitLabProject>($"groups/{groupId}/projects", cancellationToken,
                new Dictionary<string, string>
                {
                    {"simple", "true"},
                    {"archived", "false"}
                });

            var projects = projectDtos.Select(MapToProject).ToList();

            _logger.LogInformation("Successfully fetched {ProjectCount} projects for group {GroupId} via GitLab API", projects.Count, groupId);
            return projects.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch projects for group {GroupId} via GitLab API", groupId);

            // If group not found or no access, return empty list instead of throwing
            if (ex is HttpRequestException httpEx && httpEx.Message.Contains("404"))
            {
                _logger.LogWarning("Group {GroupId} not found or no access", groupId);
                return new List<GitLabProject>().AsReadOnly();
            }

            throw;
        }
    }

    public async Task<GitLabProject?> GetProjectByIdAsync(long projectId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching project {ProjectId} via GitLab API", projectId);

            var response = await _httpClient.GetAsync($"projects/{projectId}", cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Project {ProjectId} not found", projectId);
                return null;
            }

            response.EnsureSuccessStatusCode();

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var projectDto = JsonSerializer.Deserialize<DTOs.GitLabProject>(jsonContent, JsonOptions);

            if (projectDto is null)
            {
                _logger.LogWarning("Failed to deserialize project {ProjectId}", projectId);
                return null;
            }

            var project = MapToProject(projectDto);
            _logger.LogInformation("Successfully fetched project {ProjectId} ({ProjectName}) via GitLab API", projectId, project.Name);
            return project;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch project {ProjectId} via GitLab API", projectId);

            // If project not found or no access, return null instead of throwing
            if (ex is HttpRequestException httpEx && httpEx.Message.Contains("404"))
            {
                _logger.LogWarning("Project {ProjectId} not found or no access", projectId);
                return null;
            }

            throw;
        }
    }

    public async Task<IReadOnlyList<GitLabCommit>> GetCommitsAsync(long projectId, DateTimeOffset? since = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching commits for project {ProjectId} via GitLab API", projectId);

            var queryParams = new Dictionary<string, string>
            {
                {"with_stats", "true"} // Include commit statistics
            };

            if (since.HasValue)
            {
                queryParams.Add("since", since.Value.ToString("yyyy-MM-ddTHH:mm:ssZ"));
            }

            var commitDtos = await GetPaginatedAsync<DTOs.GitLabCommit>($"projects/{projectId}/repository/commits", cancellationToken, queryParams);

            var commits = commitDtos.Select(dto => MapToCommit(dto, projectId)).ToList();

            _logger.LogInformation("Successfully fetched {CommitCount} commits for project {ProjectId} via GitLab API", commits.Count, projectId);
            return commits.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch commits for project {ProjectId} via GitLab API", projectId);
            throw;
        }
    }

    public async Task<IReadOnlyList<GitLabMergeRequest>> GetMergeRequestsAsync(long projectId, DateTimeOffset? updatedAfter = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching merge requests for project {ProjectId} via GitLab API", projectId);

            var queryParams = new Dictionary<string, string>
            {
                {"state", "all"}, // Include all states (opened, closed, merged)
                {"order_by", "updated_at"},
                {"sort", "desc"}
            };

            if (updatedAfter.HasValue)
            {
                queryParams.Add("updated_after", updatedAfter.Value.ToString("yyyy-MM-ddTHH:mm:ssZ"));
            }

            var mrDtos = await GetPaginatedAsync<DTOs.GitLabMergeRequest>($"projects/{projectId}/merge_requests", cancellationToken, queryParams);

            var mergeRequests = mrDtos.Select(MapToMergeRequest).ToList();

            _logger.LogInformation("Successfully fetched {MergeRequestCount} merge requests for project {ProjectId} via GitLab API", mergeRequests.Count, projectId);
            return mergeRequests.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch merge requests for project {ProjectId} via GitLab API", projectId);
            throw;
        }
    }

    public async Task<IReadOnlyList<GitLabCommit>> GetMergeRequestCommitsAsync(long projectId, long mergeRequestIid, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching commits for merge request {MergeRequestIid} in project {ProjectId} via GitLab API", mergeRequestIid, projectId);

            // First, get the list of commits in the MR (this endpoint doesn't support with_stats)
            var commitDtos = await GetPaginatedAsync<DTOs.GitLabCommit>($"projects/{projectId}/merge_requests/{mergeRequestIid}/commits", cancellationToken);

            // Then fetch detailed stats for each commit using the repository commits endpoint
            var detailedCommitTasks = commitDtos.Select(async dto =>
            {
                try
                {
                    // Fetch individual commit details with stats
                    var response = await _httpClient.GetAsync($"projects/{projectId}/repository/commits/{Uri.EscapeDataString(dto.Id)}?stats=true", cancellationToken);
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("Failed to fetch stats for commit {CommitId} in project {ProjectId}: {StatusCode}", dto.Id, projectId, response.StatusCode);
                        return MapToCommit(dto, projectId); // Return without stats
                    }

                    var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    var detailedDto = JsonSerializer.Deserialize<DTOs.GitLabCommit>(jsonContent, JsonOptions);
                    
                    return detailedDto is not null ? MapToCommit(detailedDto, projectId) : MapToCommit(dto, projectId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch stats for commit {CommitId} in project {ProjectId}", dto.Id, projectId);
                    return MapToCommit(dto, projectId); // Return without stats on error
                }
            });

            var commits = await Task.WhenAll(detailedCommitTasks);

            _logger.LogDebug("Successfully fetched {CommitCount} commits for merge request {MergeRequestIid} in project {ProjectId}", commits.Length, mergeRequestIid, projectId);
            return commits.ToList().AsReadOnly();
        }
        catch (Exception ex)
        {
            // Try to log a more specific reason for the failure
            if (ex is HttpRequestException httpEx)
            {
                // If the inner exception is a WebException, we may be able to get the status code
                var statusCode = (httpEx.Data.Contains("StatusCode") ? httpEx.Data["StatusCode"] : null);
                if (statusCode != null)
                {
                    _logger.LogWarning(httpEx, "Failed to fetch commits for merge request {MergeRequestIid} in project {ProjectId} via GitLab API. HTTP status code: {StatusCode}", mergeRequestIid, projectId, statusCode);
                }
                else
                {
                    _logger.LogWarning(httpEx, "Failed to fetch commits for merge request {MergeRequestIid} in project {ProjectId} via GitLab API. Reason: {Message}", mergeRequestIid, projectId, httpEx.Message);
                }
            }
            else
            {
                _logger.LogWarning(ex, "Failed to fetch commits for merge request {MergeRequestIid} in project {ProjectId} via GitLab API. Reason: {Message}", mergeRequestIid, projectId, ex.Message);
            }
            // Return empty list instead of throwing to handle MRs without accessible commits gracefully
            return Array.Empty<GitLabCommit>();
        }
    }

    public async Task<IReadOnlyList<GitLabPipeline>> GetPipelinesAsync(long projectId, DateTimeOffset? updatedAfter = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching pipelines for project {ProjectId} via GitLab API", projectId);

            var queryParams = new Dictionary<string, string>
            {
                {"order_by", "updated_at"},
                {"sort", "desc"}
            };

            if (updatedAfter.HasValue)
            {
                queryParams.Add("updated_after", updatedAfter.Value.ToString("yyyy-MM-ddTHH:mm:ssZ"));
            }

            var pipelineDtos = await GetPaginatedAsync<DTOs.GitLabPipeline>($"projects/{projectId}/pipelines", cancellationToken, queryParams);

            var pipelines = pipelineDtos.Select(dto => MapToPipeline(dto, projectId)).ToList();

            _logger.LogInformation("Successfully fetched {PipelineCount} pipelines for project {ProjectId} via GitLab API", pipelines.Count, projectId);
            return pipelines.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch pipelines for project {ProjectId} via GitLab API", projectId);
            throw;
        }
    }

    public async Task<IReadOnlyList<GitLabUser>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching all users via GitLab API");

            var userDtos = await GetPaginatedAsync<DTOs.GitLabUser>("users", cancellationToken,
                new Dictionary<string, string>
                {
                    {"active", "true"}, // Only active users
                    {"blocked", "false"} // Exclude blocked users
                });

            var users = userDtos.Select(MapToUser).ToList();

            _logger.LogInformation("Successfully fetched {UserCount} users via GitLab API", users.Count);
            return users.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch users via GitLab API");
            throw;
        }
    }

    public async Task<GitLabUser?> GetUserByIdAsync(long userId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching user {UserId} via GitLab API", userId);

            var response = await _httpClient.GetAsync($"users/{userId}", cancellationToken);

            // Handle rate limiting
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var retryAfter = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(60);
                _logger.LogWarning("GitLab API rate limit hit for user {UserId}. Waiting {RetryAfter} seconds", userId, retryAfter.TotalSeconds);
                await Task.Delay(retryAfter, cancellationToken);

                // Retry the request
                response = await _httpClient.GetAsync($"users/{userId}", cancellationToken);
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning("User {UserId} not found", userId);
                return null;
            }

            response.EnsureSuccessStatusCode();

            // Log rate limit headers
            LogRateLimitHeaders(response);

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var userDto = JsonSerializer.Deserialize<DTOs.GitLabUser>(content, JsonOptions);

            if (userDto is null)
            {
                _logger.LogWarning("Failed to deserialize user {UserId}", userId);
                return null;
            }

            var user = MapToUser(userDto);
            _logger.LogDebug("Successfully fetched user {UserId}: {Username}", userId, user.Username);
            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch user {UserId} via GitLab API", userId);
            throw;
        }
    }

    public async Task<IReadOnlyList<GitLabUserProjectContribution>> GetUserProjectContributionsAsync(long userId, string? userEmail = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching user project contributions for user {UserId} via GitLab API", userId);

            // Get projects user has contributed to
            var contributedProjects = await GetUserContributedProjectsAsync(userId, cancellationToken);
            var contributions = new List<GitLabUserProjectContribution>();

            foreach (var project in contributedProjects)
            {
                try
                {
                    // For each project, get commits by this user's email
                    var commits = userEmail is not null
                        ? await GetCommitsByUserEmailAsync(project.Id, userEmail, null, cancellationToken)
                        : new List<GitLabCommit>().AsReadOnly();

                    if (commits.Any())
                    {
                        contributions.Add(new GitLabUserProjectContribution
                        {
                            ProjectId = project.Id,
                            ProjectName = project.Name,
                            UserId = userId,
                            UserEmail = userEmail,
                            CommitsCount = commits.Count,
                            LastContribution = commits.Max(c => c.CommittedDate)
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get commits for user {UserId} in project {ProjectId}", userId, project.Id);
                    // Continue with other projects
                }
            }

            _logger.LogInformation("Successfully calculated {ContributionCount} project contributions for user {UserId}", contributions.Count, userId);
            return contributions.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch user project contributions for user {UserId} via GitLab API", userId);
            throw;
        }
    }

    public async Task<IReadOnlyList<GitLabProject>> GetUserProjectsAsync(long userId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching owned projects for user {UserId} via GitLab API", userId);

            var projectDtos = await GetPaginatedAsync<DTOs.GitLabProject>($"users/{userId}/projects", cancellationToken,
                new Dictionary<string, string>
                {
                    {"simple", "true"},
                    {"owned", "true"}, // Only owned projects
                    {"archived", "false"}
                });

            var projects = projectDtos.Select(MapToProject).ToList();

            _logger.LogInformation("Successfully fetched {ProjectCount} owned projects for user {UserId} via GitLab API", projects.Count, userId);
            return projects.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch owned projects for user {UserId} via GitLab API", userId);
            throw;
        }
    }

    public async Task<IReadOnlyList<GitLabProject>> GetUserProjectsByActivityAsync(long userId, string userEmail, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching projects by activity for user {UserId} ({UserEmail}) via GitLab API", userId, userEmail);

            // Get all accessible projects
            var allProjects = await GetProjectsAsync(cancellationToken);
            var activeProjects = new List<GitLabProject>();

            // For each project, check if user has commits
            foreach (var project in allProjects)
            {
                try
                {
                    var commits = await GetCommitsByUserEmailAsync(project.Id, userEmail, DateTimeOffset.Now.AddMonths(-6), cancellationToken);
                    if (commits.Any())
                    {
                        activeProjects.Add(project);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogTrace(ex, "Could not fetch commits for user {UserEmail} in project {ProjectId}", userEmail, project.Id);
                    // Continue with other projects
                }
            }

            _logger.LogInformation("Successfully found {ProjectCount} active projects for user {UserId} via GitLab API", activeProjects.Count, userId);
            return activeProjects.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch active projects for user {UserId} via GitLab API", userId);
            throw;
        }
    }

    public async Task<IReadOnlyList<GitLabCommit>> GetCommitsByUserEmailAsync(long projectId, string userEmail, DateTimeOffset? since = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching commits by user {UserEmail} for project {ProjectId} via GitLab API", userEmail, projectId);

            var queryParams = new Dictionary<string, string>
            {
                {"author", userEmail}, // Filter by author email
                {"with_stats", "true"}
            };

            if (since.HasValue)
            {
                queryParams.Add("since", since.Value.ToString("yyyy-MM-ddTHH:mm:ssZ"));
            }

            var commitDtos = await GetPaginatedAsync<DTOs.GitLabCommit>($"projects/{projectId}/repository/commits", cancellationToken, queryParams);

            var commits = commitDtos.Select(dto => MapToCommit(dto, projectId)).ToList();

            _logger.LogDebug("Successfully fetched {CommitCount} commits by user {UserEmail} for project {ProjectId} via GitLab API", commits.Count, userEmail, projectId);
            return commits.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch commits by user {UserEmail} for project {ProjectId} via GitLab API", userEmail, projectId);
            throw;
        }
    }

    public async Task<IReadOnlyList<GitLabMergeRequestNote>> GetMergeRequestNotesAsync(long projectId, long mergeRequestIid, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching merge request notes for project {ProjectId}, MR {MergeRequestIid}", projectId, mergeRequestIid);

            var url = $"projects/{projectId}/merge_requests/{mergeRequestIid}/notes";
            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var notes = JsonSerializer.Deserialize<List<GitLabMergeRequestNote>>(content, JsonOptions) ?? new List<GitLabMergeRequestNote>();

            _logger.LogDebug("Retrieved {NoteCount} merge request notes for project {ProjectId}, MR {MergeRequestIid}", notes.Count, projectId, mergeRequestIid);
            return notes.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get merge request notes for project {ProjectId}, MR {MergeRequestIid}", projectId, mergeRequestIid);
            return new List<GitLabMergeRequestNote>().AsReadOnly();
        }
    }

    public async Task<IReadOnlyList<GitLabEvent>> GetUserEventsAsync(long userId, DateTimeOffset? after = null, DateTimeOffset? before = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching events for user {UserId}", userId);

            var queryParams = new Dictionary<string, string>
            {
                { "action", "pushed" } // Filter to only push events
            };

            if (after.HasValue)
            {
                queryParams.Add("after", after.Value.ToString("yyyy-MM-dd"));
            }

            if (before.HasValue)
            {
                queryParams.Add("before", before.Value.ToString("yyyy-MM-dd"));
            }

            var eventDtos = await GetPaginatedAsync<DTOs.GitLabEvent>($"users/{userId}/events", cancellationToken, queryParams);

            // Filter to only push events and map to domain model
            var events = eventDtos.Select(MapToEvent).ToList();

            _logger.LogInformation("Successfully fetched {EventCount} push events for user {UserId}", events.Count, userId);
            return events.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch events for user {UserId} via GitLab API", userId);
            throw;
        }
    }

    private static GitLabEvent MapToEvent(DTOs.GitLabEvent dto)
    {
        return new GitLabEvent
        {
            Id = dto.Id,
            Project = dto.ProjectId.HasValue ? new GitLabEventProject
            {
                Id = dto.ProjectId.Value,
                Name = string.Empty, // Not available in events API
                Description = string.Empty,
                WebUrl = string.Empty,
                PathWithNamespace = string.Empty
            } : null,
            ActionName = dto.ActionName,
            TargetType = dto.TargetType,
            Author = dto.Author is not null ? new GitLabEventAuthor
            {
                Id = dto.Author.Id,
                Username = dto.Author.Username,
                Name = dto.Author.Name
            } : null,
            CreatedAt = dto.CreatedAt.DateTime,
            PushData = dto.PushData is not null ? new GitLabPushData
            {
                CommitCount = dto.PushData.CommitCount,
                Action = dto.PushData.Action,
                RefType = dto.PushData.RefType,
                CommitFrom = dto.PushData.CommitFrom,
                CommitTo = dto.PushData.CommitTo,
                Ref = dto.PushData.Ref,
                CommitTitle = dto.PushData.CommitTitle
            } : null
        };
    }

    public async Task<GitLabMergeRequestChanges?> GetMergeRequestChangesAsync(long projectId, long mergeRequestIid, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching changes for MR {MergeRequestIid} in project {ProjectId}", mergeRequestIid, projectId);

            var url = $"projects/{projectId}/merge_requests/{mergeRequestIid}/changes";
            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning("MR {MergeRequestIid} not found in project {ProjectId}", mergeRequestIid, projectId);
                return null;
            }

            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var changesDto = JsonSerializer.Deserialize<DTOs.GitLabMergeRequestChangesDto>(content, JsonOptions);

            if (changesDto?.Changes is null)
            {
                _logger.LogDebug("No changes found for MR {MergeRequestIid} in project {ProjectId}", mergeRequestIid, projectId);
                return new GitLabMergeRequestChanges
                {
                    Additions = 0,
                    Deletions = 0,
                    Total = 0,
                    Changes = new List<GitLabMergeRequestChange>()
                };
            }

            // Map changes and calculate stats
            var changes = changesDto.Changes.Select(c => new GitLabMergeRequestChange
            {
                OldPath = c.OldPath,
                NewPath = c.NewPath,
                NewFile = c.NewFile,
                RenamedFile = c.RenamedFile,
                DeletedFile = c.DeletedFile
            }).ToList();

            // Note: GitLab API doesn't always provide per-file additions/deletions in the changes endpoint
            // We'll return the changes list but stats will need to be calculated from commits
            var result = new GitLabMergeRequestChanges
            {
                Additions = 0,
                Deletions = 0,
                Total = 0,
                Changes = changes
            };

            _logger.LogDebug("Successfully fetched {ChangeCount} file changes for MR {MergeRequestIid} in project {ProjectId}", 
                changes.Count, mergeRequestIid, projectId);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch changes for MR {MergeRequestIid} in project {ProjectId}", mergeRequestIid, projectId);
            return null;
        }
    }

    public async Task<IReadOnlyList<GitLabDiscussion>> GetMergeRequestDiscussionsAsync(long projectId, long mergeRequestIid, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching discussions for MR {MergeRequestIid} in project {ProjectId}", mergeRequestIid, projectId);

            var discussions = await GetPaginatedAsync<GitLabDiscussion>($"projects/{projectId}/merge_requests/{mergeRequestIid}/discussions", cancellationToken);

            _logger.LogDebug("Successfully fetched {DiscussionCount} discussions for MR {MergeRequestIid} in project {ProjectId}", 
                discussions.Count, mergeRequestIid, projectId);
            return discussions.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch discussions for MR {MergeRequestIid} in project {ProjectId}", mergeRequestIid, projectId);
            return Array.Empty<GitLabDiscussion>();
        }
    }

    public async Task<GitLabMergeRequestApprovals?> GetMergeRequestApprovalsAsync(long projectId, long mergeRequestIid, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching approvals for MR {MergeRequestIid} in project {ProjectId}", mergeRequestIid, projectId);

            var url = $"projects/{projectId}/merge_requests/{mergeRequestIid}/approvals";
            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Approvals not found for MR {MergeRequestIid} in project {ProjectId} (may not be available in this GitLab edition)", mergeRequestIid, projectId);
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch approvals for MR {MergeRequestIid} in project {ProjectId}: {StatusCode}", 
                    mergeRequestIid, projectId, response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var approvals = JsonSerializer.Deserialize<GitLabMergeRequestApprovals>(content, JsonOptions);

            _logger.LogDebug("Successfully fetched approvals for MR {MergeRequestIid} in project {ProjectId}", mergeRequestIid, projectId);
            return approvals;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch approvals for MR {MergeRequestIid} in project {ProjectId} (feature may not be available)", mergeRequestIid, projectId);
            return null;
        }
    }
}
