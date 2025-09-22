using System.Text.Json;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Configuration;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure;

/// <summary>
/// HTTP client service for making direct GitLab API calls that aren't supported by NGitLab library.
/// </summary>
public interface IGitLabHttpClient
{
    /// <summary>
    /// Gets projects that a user has contributed to using GitLab's /users/:user_id/contributed_projects API.
    /// </summary>
    /// <param name="userId">The GitLab user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of projects the user has contributed to</returns>
    Task<IReadOnlyList<GitLabContributedProject>> GetUserContributedProjectsAsync(long userId, CancellationToken cancellationToken = default);
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
}
