using System.Text.Json;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Configuration;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;

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
    /// Gets issue notes for a specific project and issue.
    /// </summary>
    /// <param name="projectId">The project ID</param>
    /// <param name="issueIid">The issue IID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of issue notes</returns>
    Task<IReadOnlyList<GitLabIssueNote>> GetIssueNotesAsync(long projectId, long issueIid, CancellationToken cancellationToken = default);
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

    // Stub implementations for interface compliance - to be implemented later
    public Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Real GitLab API client not yet implemented");

    public Task<IReadOnlyList<GitLabProject>> GetProjectsAsync(CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Real GitLab API client not yet implemented");

    public Task<IReadOnlyList<GitLabProject>> GetGroupProjectsAsync(long groupId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Real GitLab API client not yet implemented");

    public Task<IReadOnlyList<GitLabCommit>> GetCommitsAsync(long projectId, DateTimeOffset? since = null, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Real GitLab API client not yet implemented");

    public Task<IReadOnlyList<GitLabMergeRequest>> GetMergeRequestsAsync(long projectId, DateTimeOffset? updatedAfter = null, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Real GitLab API client not yet implemented");

    public Task<IReadOnlyList<GitLabPipeline>> GetPipelinesAsync(long projectId, DateTimeOffset? updatedAfter = null, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Real GitLab API client not yet implemented");

    public Task<IReadOnlyList<GitLabUser>> GetUsersAsync(CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Real GitLab API client not yet implemented");

    public Task<GitLabUser?> GetUserByIdAsync(long userId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Real GitLab API client not yet implemented");

    public Task<IReadOnlyList<GitLabUserProjectContribution>> GetUserProjectContributionsAsync(long userId, string? userEmail = null, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Real GitLab API client not yet implemented");

    public Task<IReadOnlyList<GitLabProject>> GetUserProjectsAsync(long userId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Real GitLab API client not yet implemented");

    public Task<IReadOnlyList<GitLabProject>> GetUserProjectsByActivityAsync(long userId, string userEmail, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Real GitLab API client not yet implemented");

    public Task<IReadOnlyList<GitLabCommit>> GetCommitsByUserEmailAsync(long projectId, string userEmail, DateTimeOffset? since = null, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Real GitLab API client not yet implemented");

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

    public async Task<IReadOnlyList<GitLabIssueNote>> GetIssueNotesAsync(long projectId, long issueIid, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching issue notes for project {ProjectId}, issue {IssueIid}", projectId, issueIid);

            var url = $"projects/{projectId}/issues/{issueIid}/notes";
            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var notes = JsonSerializer.Deserialize<List<GitLabIssueNote>>(content, JsonOptions) ?? new List<GitLabIssueNote>();

            _logger.LogDebug("Retrieved {NoteCount} issue notes for project {ProjectId}, issue {IssueIid}", notes.Count, projectId, issueIid);
            return notes.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get issue notes for project {ProjectId}, issue {IssueIid}", projectId, issueIid);
            return new List<GitLabIssueNote>().AsReadOnly();
        }
    }
}
