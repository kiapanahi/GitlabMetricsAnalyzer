using System.Net.Http.Headers;
using System.Text.Json;

using Microsoft.Extensions.Options;

using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Configuration;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure.DTOs;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure;

public sealed class GitLabApiService(HttpClient httpClient) : IGitLabApiService
{
    private readonly HttpClient _httpClient = httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IReadOnlyList<GitLabProject>> GetProjectsAsync(string groupPath, CancellationToken cancellationToken = default)
    {
        var projects = new List<GitLabProject>();
        var page = 1;
        const int perPage = 100;

        while (true)
        {
            var response = await _httpClient.GetAsync(
                $"/api/v4/groups/{Uri.EscapeDataString(groupPath)}/projects?page={page}&per_page={perPage}&include_subgroups=true&archived=false&simple=false",
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var pageProjects = await ReadResponseContent<GitLabProject[]>(response, cancellationToken).ConfigureAwait(false);

            if (pageProjects is null || pageProjects.Length == 0)
                break;

            projects.AddRange(pageProjects);

            if (pageProjects.Length < perPage)
                break;

            page++;
        }

        return projects;
    }

    public async Task<IReadOnlyList<GitLabMergeRequest>> GetMergeRequestsAsync(int projectId, DateTimeOffset? updatedAfter = null, CancellationToken cancellationToken = default)
    {
        var mergeRequests = new List<GitLabMergeRequest>();
        var page = 1;
        const int perPage = 100;

        var updatedAfterParam = updatedAfter?.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

        while (true)
        {
            var url = $"/api/v4/projects/{projectId}/merge_requests?page={page}&per_page={perPage}&state=all&order_by=updated_at&sort=asc";
            if (!string.IsNullOrEmpty(updatedAfterParam))
                url += $"&updated_after={Uri.EscapeDataString(updatedAfterParam)}";

            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var pageMrs = await ReadResponseContent<GitLabMergeRequest[]>(response, cancellationToken).ConfigureAwait(false);

            if (pageMrs is null || pageMrs.Length == 0)
                break;

            mergeRequests.AddRange(pageMrs);

            if (pageMrs.Length < perPage)
                break;

            page++;
        }

        return mergeRequests;
    }

    public async Task<IReadOnlyList<GitLabCommit>> GetCommitsAsync(int projectId, string? since = null, CancellationToken cancellationToken = default)
    {
        var commits = new List<GitLabCommit>();
        var page = 1;
        const int perPage = 100;

        while (true)
        {
            var url = $"/api/v4/projects/{projectId}/repository/commits?page={page}&per_page={perPage}&with_stats=true";
            if (!string.IsNullOrEmpty(since))
                url += $"&since={Uri.EscapeDataString(since)}";

            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var pageCommits = await ReadResponseContent<GitLabCommit[]>(response, cancellationToken).ConfigureAwait(false);

            if (pageCommits is null || pageCommits.Length == 0)
                break;

            commits.AddRange(pageCommits);

            if (pageCommits.Length < perPage)
                break;

            page++;
        }

        return commits;
    }

    public async Task<IReadOnlyList<GitLabPipeline>> GetPipelinesAsync(int projectId, DateTimeOffset? updatedAfter = null, CancellationToken cancellationToken = default)
    {
        var pipelines = new List<GitLabPipeline>();
        var page = 1;
        const int perPage = 100;

        var updatedAfterParam = updatedAfter?.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

        while (true)
        {
            var url = $"/api/v4/projects/{projectId}/pipelines?page={page}&per_page={perPage}&order_by=updated_at&sort=asc";
            if (!string.IsNullOrEmpty(updatedAfterParam))
                url += $"&updated_after={Uri.EscapeDataString(updatedAfterParam)}";

            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var pagePipelines = await ReadResponseContent<GitLabPipeline[]>(response, cancellationToken).ConfigureAwait(false);

            if (pagePipelines is null || pagePipelines.Length == 0)
                break;

            pipelines.AddRange(pagePipelines);

            if (pagePipelines.Length < perPage)
                break;

            page++;
        }

        return pipelines;
    }

    private static async ValueTask<T?> ReadResponseContent<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken).ConfigureAwait(false);
        return result;
    }
}
