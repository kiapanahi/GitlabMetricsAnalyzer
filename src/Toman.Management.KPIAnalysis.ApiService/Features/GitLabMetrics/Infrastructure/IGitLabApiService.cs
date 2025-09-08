using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure.DTOs;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure;

[Obsolete("use the `IGitLabClient` from NGitLab package instead", true)]
public interface IGitLabApiService
{
    Task<IReadOnlyList<GitLabProject>> GetAllProjectsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GitLabMergeRequest>> GetMergeRequestsAsync(int projectId, DateTimeOffset? updatedAfter = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GitLabCommit>> GetCommitsAsync(int projectId, string? since = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GitLabPipeline>> GetPipelinesAsync(int projectId, DateTimeOffset? updatedAfter = null, CancellationToken cancellationToken = default);
}
