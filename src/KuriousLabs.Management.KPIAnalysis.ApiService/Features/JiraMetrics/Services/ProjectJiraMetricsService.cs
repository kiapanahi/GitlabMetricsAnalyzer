using KuriousLabs.Management.KPIAnalysis.ApiService.Features.JiraMetrics.Infrastructure;
using KuriousLabs.Management.KPIAnalysis.ApiService.Features.JiraMetrics.Infrastructure.DTOs;
using KuriousLabs.Management.KPIAnalysis.ApiService.Features.JiraMetrics.Models;

namespace KuriousLabs.Management.KPIAnalysis.ApiService.Features.JiraMetrics.Services;

public interface IProjectJiraMetricsService
{
    /// <summary>
    /// Calculates Jira metrics for a specific project within a time window
    /// </summary>
    Task<ProjectJiraMetrics> CalculateProjectMetricsAsync(
        string projectKey,
        int windowDays,
        CancellationToken cancellationToken = default);
}

public sealed class ProjectJiraMetricsService : IProjectJiraMetricsService
{
    private readonly IJiraHttpClient _jiraHttpClient;
    private readonly ILogger<ProjectJiraMetricsService> _logger;

    public ProjectJiraMetricsService(
        IJiraHttpClient jiraHttpClient,
        ILogger<ProjectJiraMetricsService> logger)
    {
        _jiraHttpClient = jiraHttpClient;
        _logger = logger;
    }

    public async Task<ProjectJiraMetrics> CalculateProjectMetricsAsync(
        string projectKey,
        int windowDays,
        CancellationToken cancellationToken = default)
    {
        using var activity = Diagnostics.ActivitySource.StartActivity("CalculateProjectJiraMetrics");
        activity?.SetTag("project.key", projectKey);
        activity?.SetTag("window.days", windowDays);

        var endDate = DateTime.UtcNow;
        var startDate = endDate.AddDays(-windowDays);

        try
        {
            // Fetch project info
            var project = await _jiraHttpClient.GetProjectAsync(projectKey, cancellationToken);
            var projectName = project?.Name ?? projectKey;

            // Build JQL queries
            var backlogJql = $"project = {projectKey} AND status in (\"To Do\", Backlog, Open) ORDER BY created DESC";
            var inProgressJql = $"project = {projectKey} AND status in (\"In Progress\", \"In Development\", \"In Review\")";
            var completedJql = $"project = {projectKey} AND resolved >= -{windowDays}d";

            // Fetch issues
            var backlogTask = FetchAllIssuesAsync(backlogJql, cancellationToken);
            var inProgressTask = FetchAllIssuesAsync(inProgressJql, cancellationToken);
            var completedTask = FetchAllIssuesAsync(completedJql, cancellationToken);

            await Task.WhenAll(backlogTask, inProgressTask, completedTask);

            var backlogIssues = await backlogTask;
            var inProgressIssues = await inProgressTask;
            var completedIssues = await completedTask;

            // Calculate velocity (issues per week)
            var weeksInWindow = windowDays / 7.0;
            var velocity = weeksInWindow > 0 ? completedIssues.Count / weeksInWindow : 0;

            // Calculate average cycle time
            var cycleTimes = completedIssues
                .Where(i => i.Fields.Created.HasValue && i.Fields.ResolutionDate.HasValue)
                .Select(i => (i.Fields.ResolutionDate!.Value - i.Fields.Created!.Value).TotalHours)
                .ToList();

            var avgCycleTime = cycleTimes.Any() ? cycleTimes.Average() : 0;

            // Group by status
            var issuesByStatus = new Dictionary<string, int>();
            
            // Add backlog
            foreach (var issue in backlogIssues)
            {
                var status = issue.Fields.Status?.Name ?? "Unknown";
                issuesByStatus[status] = issuesByStatus.GetValueOrDefault(status) + 1;
            }
            
            // Add in progress
            foreach (var issue in inProgressIssues)
            {
                var status = issue.Fields.Status?.Name ?? "Unknown";
                issuesByStatus[status] = issuesByStatus.GetValueOrDefault(status) + 1;
            }

            _logger.LogInformation(
                "Calculated project metrics for {ProjectKey}: {Backlog} backlog, {InProgress} in progress, {Completed} completed",
                projectKey, backlogIssues.Count, inProgressIssues.Count, completedIssues.Count);

            return new ProjectJiraMetrics
            {
                ProjectKey = projectKey,
                ProjectName = projectName,
                BacklogSize = backlogIssues.Count,
                IssuesInProgress = inProgressIssues.Count,
                IssuesCompleted = completedIssues.Count,
                VelocityIssuesPerWeek = velocity,
                AverageCycleTimeHours = avgCycleTime,
                StartDate = startDate,
                EndDate = endDate,
                IssuesByStatus = issuesByStatus
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating project metrics for {ProjectKey}", projectKey);
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
                _logger.LogWarning("Reached maximum issue fetch limit for project metrics");
                break;
            }
        }

        return allIssues;
    }
}
