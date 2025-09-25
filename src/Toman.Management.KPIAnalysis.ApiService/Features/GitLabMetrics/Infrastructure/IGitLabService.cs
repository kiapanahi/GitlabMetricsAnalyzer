using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure;

public interface IGitLabService
{
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GitLabProject>> GetProjectsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GitLabProject>> GetGroupProjectsAsync(long groupId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RawCommit>> GetCommitsAsync(long projectId, DateTimeOffset? since = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RawMergeRequest>> GetMergeRequestsAsync(long projectId, DateTimeOffset? updatedAfter = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RawPipeline>> GetPipelinesAsync(long projectId, DateTimeOffset? updatedAfter = null, CancellationToken cancellationToken = default);
    
    // Issue management methods
    Task<IReadOnlyList<RawIssue>> GetIssuesAsync(long projectId, DateTimeOffset? updatedAfter = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RawIssue>> GetUserAssignedIssuesAsync(long userId, DateTimeOffset? updatedAfter = null, CancellationToken cancellationToken = default);
    
    // User management methods
    Task<IReadOnlyList<GitLabUser>> GetUsersAsync(CancellationToken cancellationToken = default);
    Task<GitLabUser?> GetUserByIdAsync(long userId, CancellationToken cancellationToken = default);
    
    // User-project relationship methods
    Task<IReadOnlyList<GitLabProject>> GetUserContributedProjectsAsync(long userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserProjectContribution>> GetUserProjectContributionsAsync(long userId, string? userEmail = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GitLabProject>> GetUserProjectsAsync(long userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GitLabProject>> GetUserProjectsByActivityAsync(long userId, string userEmail, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RawCommit>> GetCommitsByUserEmailAsync(long projectId, string userEmail, DateTimeOffset? since = null, CancellationToken cancellationToken = default);
}
