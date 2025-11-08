using KuriousLabs.Management.KPIAnalysis.ApiService.Features.JiraMetrics.Infrastructure;
using KuriousLabs.Management.KPIAnalysis.ApiService.Features.JiraMetrics.Infrastructure.DTOs;
using KuriousLabs.Management.KPIAnalysis.ApiService.Features.JiraMetrics.Models;

namespace KuriousLabs.Management.KPIAnalysis.ApiService.Features.JiraMetrics.Services;

public interface IUserJiraMetricsService
{
    /// <summary>
    /// Calculates Jira metrics for a specific user within a time window
    /// </summary>
    Task<UserJiraMetrics> CalculateUserMetricsAsync(
        string accountId,
        int windowDays,
        CancellationToken cancellationToken = default);
}

public sealed class UserJiraMetricsService : IUserJiraMetricsService
{
    private readonly IJiraHttpClient _jiraHttpClient;
    private readonly ILogger<UserJiraMetricsService> _logger;

    public UserJiraMetricsService(
        IJiraHttpClient jiraHttpClient,
        ILogger<UserJiraMetricsService> logger)
    {
        _jiraHttpClient = jiraHttpClient;
        _logger = logger;
    }

    public async Task<UserJiraMetrics> CalculateUserMetricsAsync(
        string accountId,
        int windowDays,
        CancellationToken cancellationToken = default)
    {
        using var activity = Diagnostics.ActivitySource.StartActivity("CalculateUserJiraMetrics");
        activity?.SetTag("user.accountId", accountId);
        activity?.SetTag("window.days", windowDays);

        var endDate = DateTime.UtcNow;
        var startDate = endDate.AddDays(-windowDays);

        try
        {
            // Fetch user info
            var user = await _jiraHttpClient.GetUserAsync(accountId, cancellationToken);
            var displayName = user?.DisplayName ?? accountId;

            // Build JQL queries
            var assignedJql = $"assignee = {accountId} AND updated >= -{windowDays}d";
            var resolvedJql = $"assignee = {accountId} AND resolved >= -{windowDays}d";
            var createdJql = $"reporter = {accountId} AND created >= -{windowDays}d";
            var openJql = $"assignee = {accountId} AND status not in (Done, Resolved, Closed)";

            // Fetch issues in parallel
            var assignedTask = FetchAllIssuesAsync(assignedJql, cancellationToken);
            var resolvedTask = FetchAllIssuesAsync(resolvedJql, cancellationToken);
            var createdTask = FetchAllIssuesAsync(createdJql, cancellationToken);
            var openTask = FetchAllIssuesAsync(openJql, cancellationToken);

            await Task.WhenAll(assignedTask, resolvedTask, createdTask, openTask);

            var assignedIssues = await assignedTask;
            var resolvedIssues = await resolvedTask;
            var createdIssues = await createdTask;
            var openIssues = await openTask;

            // Calculate average resolution time
            var resolutionTimes = resolvedIssues
                .Where(i => i.Fields.Created.HasValue && i.Fields.ResolutionDate.HasValue)
                .Select(i => (i.Fields.ResolutionDate!.Value - i.Fields.Created!.Value).TotalHours)
                .ToList();

            var avgResolutionTime = resolutionTimes.Any() ? resolutionTimes.Average() : 0;

            _logger.LogInformation(
                "Calculated user metrics for {AccountId}: {Assigned} assigned, {Resolved} resolved, {Created} created",
                accountId, assignedIssues.Count, resolvedIssues.Count, createdIssues.Count);

            return new UserJiraMetrics
            {
                AccountId = accountId,
                DisplayName = displayName,
                IssuesAssigned = assignedIssues.Count,
                IssuesResolved = resolvedIssues.Count,
                IssuesCreated = createdIssues.Count,
                IssuesOpen = openIssues.Count,
                AverageResolutionTimeHours = avgResolutionTime,
                StartDate = startDate,
                EndDate = endDate
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating user metrics for {AccountId}", accountId);
            throw;
        }
    }

    private async Task<List<JiraIssue>> FetchAllIssuesAsync(string jql, CancellationToken cancellationToken)
    {
        var allIssues = new List<JiraIssue>();
        var startAt = 0;
        const int maxResults = 100;
        var hasMore = true;

        while (hasMore)
        {
            var result = await _jiraHttpClient.SearchIssuesAsync(jql, startAt, maxResults, cancellationToken);
            allIssues.AddRange(result.Issues);
            startAt += maxResults;
            hasMore = startAt < result.Total;

            if (startAt > 10000)
            {
                _logger.LogWarning("Reached maximum issue fetch limit for user metrics");
                break;
            }
        }

        return allIssues;
    }
}
