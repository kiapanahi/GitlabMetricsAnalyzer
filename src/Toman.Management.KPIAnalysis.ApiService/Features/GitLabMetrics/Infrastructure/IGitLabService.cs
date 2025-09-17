using NGitLab.Models;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure;

public interface IGitLabService
{
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Project>> GetProjectsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Project>> GetGroupProjectsAsync(long groupId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RawCommit>> GetCommitsAsync(long projectId, DateTimeOffset? since = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RawMergeRequest>> GetMergeRequestsAsync(long projectId, DateTimeOffset? updatedAfter = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RawPipeline>> GetPipelinesAsync(long projectId, DateTimeOffset? updatedAfter = null, CancellationToken cancellationToken = default);
    
    // User management methods
    Task<IReadOnlyList<User>> GetUsersAsync(CancellationToken cancellationToken = default);
    Task<User?> GetUserByIdAsync(long userId, CancellationToken cancellationToken = default);
    
    // User-project relationship methods
    Task<IReadOnlyList<Project>> GetUserProjectsAsync(long userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RawCommit>> GetCommitsByUserEmailAsync(long projectId, string userEmail, DateTimeOffset? since = null, CancellationToken cancellationToken = default);
}
