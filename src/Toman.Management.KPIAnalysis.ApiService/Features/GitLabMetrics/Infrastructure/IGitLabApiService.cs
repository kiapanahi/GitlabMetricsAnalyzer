using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure.DTOs;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure;

public interface IGitLabApiService
{
    Task<IReadOnlyList<GitLabGroup>> GetAllGroupsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GitLabProject>> GetProjectsAsync(string groupPath, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GitLabMergeRequest>> GetMergeRequestsAsync(int projectId, DateTimeOffset? updatedAfter = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GitLabCommit>> GetCommitsAsync(int projectId, string? since = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GitLabPipeline>> GetPipelinesAsync(int projectId, DateTimeOffset? updatedAfter = null, CancellationToken cancellationToken = default);
}
