using KuriousLabs.Management.KPIAnalysis.ApiService.Features.JiraMetrics.Infrastructure;
using KuriousLabs.Management.KPIAnalysis.ApiService.Features.JiraMetrics.Infrastructure.DTOs;
using KuriousLabs.Management.KPIAnalysis.ApiService.Features.JiraMetrics.Models;

namespace KuriousLabs.Management.KPIAnalysis.ApiService.Features.JiraMetrics.Services;

public interface IIssueTrackingMetricsService
{
    /// <summary>
    /// Calculates issue tracking metrics for a project within a time window
    /// </summary>
    /// <param name="projectKey">The Jira project key</param>
    /// <param name="windowDays">Number of days to look back</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Issue tracking metrics</returns>
    Task<IssueTrackingMetrics> CalculateIssueTrackingMetricsAsync(
        string projectKey,
        int windowDays,
        CancellationToken cancellationToken = default);
}

public sealed class IssueTrackingMetricsService : IIssueTrackingMetricsService
{
    private readonly IJiraHttpClient _jiraHttpClient;
    private readonly ILogger<IssueTrackingMetricsService> _logger;

    public IssueTrackingMetricsService(
        IJiraHttpClient jiraHttpClient,
        ILogger<IssueTrackingMetricsService> logger)
    {
        _jiraHttpClient = jiraHttpClient;
        _logger = logger;
    }

    public async Task<IssueTrackingMetrics> CalculateIssueTrackingMetricsAsync(
        string projectKey,
        int windowDays,
        CancellationToken cancellationToken = default)
    {
        using var activity = Diagnostics.ActivitySource.StartActivity("CalculateIssueTrackingMetrics");
        activity?.SetTag("project.key", projectKey);
        activity?.SetTag("window.days", windowDays);

        var endDate = DateTime.UtcNow;
        var startDate = endDate.AddDays(-windowDays);

        try
        {
            // Build JQL queries for different scenarios
            var createdJql = $"project = {projectKey} AND created >= -{windowDays}d";
            var resolvedJql = $"project = {projectKey} AND resolved >= -{windowDays}d";
            var openJql = $"project = {projectKey} AND status != Done AND status != Resolved AND status != Closed";

            // Fetch issues in parallel
            var createdTask = FetchAllIssuesAsync(createdJql, cancellationToken);
            var resolvedTask = FetchAllIssuesAsync(resolvedJql, cancellationToken);
            var openTask = FetchAllIssuesAsync(openJql, cancellationToken);

            await Task.WhenAll(createdTask, resolvedTask, openTask);

            var createdIssues = await createdTask;
            var resolvedIssues = await resolvedTask;
            var openIssues = await openTask;

            // Calculate resolution times for resolved issues
            var resolutionTimes = resolvedIssues
                .Where(i => i.Fields.Created.HasValue && i.Fields.ResolutionDate.HasValue)
                .Select(i => (i.Fields.ResolutionDate!.Value - i.Fields.Created!.Value).TotalHours)
                .OrderBy(h => h)
                .ToList();

            var avgResolutionTime = resolutionTimes.Any() ? resolutionTimes.Average() : 0;
            var medianResolutionTime = resolutionTimes.Any() ? 
                resolutionTimes[resolutionTimes.Count / 2] : 0;

            // Calculate cycle times (all resolved issues)
            var cycleTimes = resolvedIssues
                .Where(i => i.Fields.Created.HasValue && i.Fields.ResolutionDate.HasValue)
                .Select(i => (i.Fields.ResolutionDate!.Value - i.Fields.Created!.Value).TotalHours)
                .OrderBy(h => h)
                .ToList();

            var avgCycleTime = cycleTimes.Any() ? cycleTimes.Average() : 0;
            var medianCycleTime = cycleTimes.Any() ? cycleTimes[cycleTimes.Count / 2] : 0;

            // Calculate rates
            var daysInWindow = windowDays > 0 ? windowDays : 1;
            var creationRate = (double)createdIssues.Count / daysInWindow;
            var resolutionRate = (double)resolvedIssues.Count / daysInWindow;

            // Group by issue type
            var byIssueType = resolvedIssues
                .Where(i => i.Fields.IssueType is not null)
                .GroupBy(i => i.Fields.IssueType!.Name)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        var typeResolutionTimes = g
                            .Where(i => i.Fields.Created.HasValue && i.Fields.ResolutionDate.HasValue)
                            .Select(i => (i.Fields.ResolutionDate!.Value - i.Fields.Created!.Value).TotalHours)
                            .OrderBy(h => h)
                            .ToList();

                        return new IssueTypeMetrics
                        {
                            IssueType = g.Key,
                            Count = g.Count(),
                            AverageResolutionTimeHours = typeResolutionTimes.Any() ? 
                                typeResolutionTimes.Average() : 0,
                            MedianResolutionTimeHours = typeResolutionTimes.Any() ? 
                                typeResolutionTimes[typeResolutionTimes.Count / 2] : 0
                        };
                    });

            _logger.LogInformation(
                "Calculated issue tracking metrics for project {ProjectKey}: {Created} created, {Resolved} resolved, {Open} open",
                projectKey, createdIssues.Count, resolvedIssues.Count, openIssues.Count);

            return new IssueTrackingMetrics
            {
                AverageResolutionTimeHours = avgResolutionTime,
                MedianResolutionTimeHours = medianResolutionTime,
                AverageCycleTimeHours = avgCycleTime,
                MedianCycleTimeHours = medianCycleTime,
                IssuesCreated = createdIssues.Count,
                IssuesResolved = resolvedIssues.Count,
                IssuesOpen = openIssues.Count,
                CreationRate = creationRate,
                ResolutionRate = resolutionRate,
                StartDate = startDate,
                EndDate = endDate,
                ByIssueType = byIssueType
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating issue tracking metrics for project {ProjectKey}", projectKey);
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
            
            // Prevent infinite loops
            if (startAt > 10000)
            {
                _logger.LogWarning("Reached maximum issue fetch limit of 10000 for JQL: {Jql}", jql);
                break;
            }
        }

        return allIssues;
    }
}
