using Toman.Management.KPIAnalysis.ApiService.GitLab.DTOs;

namespace Toman.Management.KPIAnalysis.ApiService.GitLab;

public interface IGitLabApiService
{
    Task<IReadOnlyList<GitLabProject>> GetProjectsAsync(string groupPath, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GitLabMergeRequest>> GetMergeRequestsAsync(int projectId, DateTimeOffset? updatedAfter = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GitLabCommit>> GetCommitsAsync(int projectId, string? since = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GitLabPipeline>> GetPipelinesAsync(int projectId, DateTimeOffset? updatedAfter = null, CancellationToken cancellationToken = default);
}
