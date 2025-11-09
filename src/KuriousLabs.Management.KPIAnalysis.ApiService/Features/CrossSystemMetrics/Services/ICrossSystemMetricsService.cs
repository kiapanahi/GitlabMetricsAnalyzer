using KuriousLabs.Management.KPIAnalysis.ApiService.Features.CrossSystemMetrics.Models;

namespace KuriousLabs.Management.KPIAnalysis.ApiService.Features.CrossSystemMetrics.Services;

/// <summary>
/// Service for calculating cross-system metrics between Jira and GitLab
/// </summary>
public interface ICrossSystemMetricsService
{
    /// <summary>
    /// Calculate delivery metrics by correlating Jira issues with GitLab commits and merge requests
    /// </summary>
    /// <param name="projectKey">Jira project key</param>
    /// <param name="gitLabProjectIds">GitLab project IDs to include in correlation</param>
    /// <param name="windowDays">Number of days to look back</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<DeliveryMetrics> CalculateDeliveryMetricsAsync(
        string projectKey,
        IReadOnlyList<long> gitLabProjectIds,
        int windowDays,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculate work correlation to identify orphaned work
    /// </summary>
    /// <param name="projectKey">Jira project key</param>
    /// <param name="gitLabProjectIds">GitLab project IDs to include in correlation</param>
    /// <param name="windowDays">Number of days to look back</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<WorkCorrelation> CalculateWorkCorrelationAsync(
        string projectKey,
        IReadOnlyList<long> gitLabProjectIds,
        int windowDays,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all GitLab activity (commits, MRs) related to a specific Jira issue
    /// </summary>
    /// <param name="issueKey">Jira issue key (e.g., "PROJECT-123")</param>
    /// <param name="gitLabProjectIds">GitLab project IDs to search</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<IssueActivity> GetIssueActivityAsync(
        string issueKey,
        IReadOnlyList<long> gitLabProjectIds,
        CancellationToken cancellationToken = default);
}
