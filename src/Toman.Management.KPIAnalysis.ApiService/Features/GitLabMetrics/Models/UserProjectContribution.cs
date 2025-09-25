using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models;

/// <summary>
/// Represents a project and the user's contribution level to it
/// </summary>
public sealed class UserProjectContribution
{
    public required GitLabProject Project { get; init; }
    
    /// <summary>
    /// Number of commits by the user in this project
    /// </summary>
    public int CommitsCount { get; init; }
    
    /// <summary>
    /// Number of merge requests created by the user in this project
    /// </summary>
    public int MergeRequestsCount { get; init; }
    
    /// <summary>
    /// Number of issues created by the user in this project
    /// </summary>
    public int IssuesCount { get; init; }
    
    /// <summary>
    /// Total contribution weight (calculated based on commits, MRs, and issues)
    /// Higher weight indicates more significant contribution to the project
    /// </summary>
    public double Weight { get; init; }
    
    /// <summary>
    /// Last activity date by the user in this project
    /// </summary>
    public DateTimeOffset? LastActivityAt { get; init; }
    
    /// <summary>
    /// Indicates if the user has any meaningful contribution to this project
    /// </summary>
    public bool HasContribution => CommitsCount > 0 || MergeRequestsCount > 0 || IssuesCount > 0;
}
