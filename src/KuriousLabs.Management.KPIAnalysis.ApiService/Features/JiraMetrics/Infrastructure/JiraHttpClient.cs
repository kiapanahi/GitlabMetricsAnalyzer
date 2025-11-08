using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using KuriousLabs.Management.KPIAnalysis.ApiService.Features.JiraMetrics.Configuration;
using KuriousLabs.Management.KPIAnalysis.ApiService.Features.JiraMetrics.Infrastructure.DTOs;

using Microsoft.Extensions.Options;

namespace KuriousLabs.Management.KPIAnalysis.ApiService.Features.JiraMetrics.Infrastructure;

/// <summary>
/// HTTP client service for making Jira API calls.
/// </summary>
public interface IJiraHttpClient
{
    /// <summary>
    /// Tests the connection to Jira API by fetching server info.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Server info if successful, null otherwise</returns>
    Task<JiraServerInfo?> GetServerInfoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific issue by key.
    /// </summary>
    /// <param name="issueKey">The issue key (e.g., PROJECT-123)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The issue or null if not found</returns>
    Task<JiraIssue?> GetIssueAsync(string issueKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for issues using JQL.
    /// </summary>
    /// <param name="jql">JQL query string</param>
    /// <param name="startAt">Starting index for pagination</param>
    /// <param name="maxResults">Maximum results per page</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Search result with issues</returns>
    Task<JiraSearchResult<JiraIssue>> SearchIssuesAsync(string jql, int startAt = 0, int maxResults = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a project by key.
    /// </summary>
    /// <param name="projectKey">The project key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The project or null if not found</returns>
    Task<JiraProject?> GetProjectAsync(string projectKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user by account ID.
    /// </summary>
    /// <param name="accountId">The account ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The user or null if not found</returns>
    Task<JiraUser?> GetUserAsync(string accountId, CancellationToken cancellationToken = default);
}

public sealed class JiraHttpClient : IJiraHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly JiraConfiguration _configuration;
    private readonly ILogger<JiraHttpClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public JiraHttpClient(
        HttpClient httpClient,
        IOptions<JiraConfiguration> configuration,
        ILogger<JiraHttpClient> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration.Value;
        _logger = logger;
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        // Configure base address and authentication
        _httpClient.BaseAddress = new Uri(_configuration.BaseUrl.TrimEnd('/') + "/");
        
        // Use Bearer token authentication (Personal Access Token for Data Center)
        _httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", _configuration.Token);
        
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<JiraServerInfo?> GetServerInfoAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var activity = Diagnostics.ActivitySource.StartActivity("GetServerInfo");
            
            var response = await _httpClient.GetAsync("rest/api/3/serverInfo", cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get Jira server info: {StatusCode}", response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<JiraServerInfo>(content, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Jira server info");
            return null;
        }
    }

    public async Task<JiraIssue?> GetIssueAsync(string issueKey, CancellationToken cancellationToken = default)
    {
        try
        {
            using var activity = Diagnostics.ActivitySource.StartActivity("GetIssue");
            activity?.SetTag("issue.key", issueKey);
            
            var response = await _httpClient.GetAsync($"rest/api/3/issue/{issueKey}", cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get issue {IssueKey}: {StatusCode}", issueKey, response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<JiraIssue>(content, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting issue {IssueKey}", issueKey);
            return null;
        }
    }

    public async Task<JiraSearchResult<JiraIssue>> SearchIssuesAsync(
        string jql, 
        int startAt = 0, 
        int maxResults = 50, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var activity = Diagnostics.ActivitySource.StartActivity("SearchIssues");
            activity?.SetTag("jql", jql);
            activity?.SetTag("startAt", startAt);
            activity?.SetTag("maxResults", maxResults);
            
            var url = $"rest/api/3/search?jql={Uri.EscapeDataString(jql)}&startAt={startAt}&maxResults={maxResults}";
            var response = await _httpClient.GetAsync(url, cancellationToken);
            
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<JiraSearchResult<JiraIssue>>(content, _jsonOptions) 
                ?? new JiraSearchResult<JiraIssue>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching issues with JQL: {Jql}", jql);
            return new JiraSearchResult<JiraIssue>();
        }
    }

    public async Task<JiraProject?> GetProjectAsync(string projectKey, CancellationToken cancellationToken = default)
    {
        try
        {
            using var activity = Diagnostics.ActivitySource.StartActivity("GetProject");
            activity?.SetTag("project.key", projectKey);
            
            var response = await _httpClient.GetAsync($"rest/api/3/project/{projectKey}", cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get project {ProjectKey}: {StatusCode}", projectKey, response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<JiraProject>(content, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting project {ProjectKey}", projectKey);
            return null;
        }
    }

    public async Task<JiraUser?> GetUserAsync(string accountId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var activity = Diagnostics.ActivitySource.StartActivity("GetUser");
            activity?.SetTag("user.accountId", accountId);
            
            var response = await _httpClient.GetAsync($"rest/api/3/user?accountId={Uri.EscapeDataString(accountId)}", cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get user {AccountId}: {StatusCode}", accountId, response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<JiraUser>(content, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user {AccountId}", accountId);
            return null;
        }
    }
}
