using System.Diagnostics;
using KuriousLabs.Management.KPIAnalysis.ApiService.Features.CrossSystemMetrics.Infrastructure;
using KuriousLabs.Management.KPIAnalysis.ApiService.Features.CrossSystemMetrics.Models;
using KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure;
using KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;
using KuriousLabs.Management.KPIAnalysis.ApiService.Features.JiraMetrics.Infrastructure;

namespace KuriousLabs.Management.KPIAnalysis.ApiService.Features.CrossSystemMetrics.Services;

/// <summary>
/// Implementation of cross-system metrics service
/// </summary>
public sealed class CrossSystemMetricsService : ICrossSystemMetricsService
{
    private readonly IJiraHttpClient _jiraHttpClient;
    private readonly IJiraIssueKeyParser _issueKeyParser;
    private readonly IIdentityMappingService _identityMapping;
    private readonly IGitLabService _gitLabService;
    private readonly ILogger<CrossSystemMetricsService> _logger;

    public CrossSystemMetricsService(
        IJiraHttpClient jiraHttpClient,
        IJiraIssueKeyParser issueKeyParser,
        IIdentityMappingService identityMapping,
        IGitLabService gitLabService,
        ILogger<CrossSystemMetricsService> logger)
    {
        _jiraHttpClient = jiraHttpClient;
        _issueKeyParser = issueKeyParser;
        _identityMapping = identityMapping;
        _gitLabService = gitLabService;
        _logger = logger;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<DeliveryMetrics> CalculateDeliveryMetricsAsync(
        string projectKey,
        IReadOnlyList<long> gitLabProjectIds,
        int windowDays,
        CancellationToken cancellationToken = default)
    {
        using var activity = Diagnostics.ActivitySource.StartActivity("CalculateDeliveryMetrics");
        activity?.SetTag("project_key", projectKey);
        activity?.SetTag("window_days", windowDays);
        activity?.SetTag("gitlab_projects_count", gitLabProjectIds.Count);

        ArgumentException.ThrowIfNullOrWhiteSpace(projectKey);
        ArgumentNullException.ThrowIfNull(gitLabProjectIds);
        if (windowDays < 1 || windowDays > 365)
        {
            throw new ArgumentException("Window days must be between 1 and 365", nameof(windowDays));
        }

        var startDate = DateTime.UtcNow.AddDays(-windowDays);
        var endDate = DateTime.UtcNow;

        // Get Jira issues for the project
        var jql = $"project = {projectKey} AND updated >= -{windowDays}d ORDER BY updated DESC";
        var jiraResult = await _jiraHttpClient.SearchIssuesAsync(jql, 0, 1000, cancellationToken);
        var jiraIssues = jiraResult.Issues;

        _logger.LogInformation("Found {Count} Jira issues for project {ProjectKey}", jiraIssues.Count, projectKey);

        // Get GitLab commits and MRs for specified projects
        var allCommits = new List<RawCommit>();
        var allMergeRequests = new List<RawMergeRequest>();

        foreach (var projectId in gitLabProjectIds)
        {
            try
            {
                var commits = await _gitLabService.GetCommitsAsync(projectId, startDate, cancellationToken);
                allCommits.AddRange(commits);

                var mergeRequests = await _gitLabService.GetMergeRequestsAsync(projectId, startDate, cancellationToken);
                allMergeRequests.AddRange(mergeRequests);

                _logger.LogDebug("Retrieved {CommitCount} commits and {MrCount} MRs from GitLab project {ProjectId}",
                    commits.Count, mergeRequests.Count, projectId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to retrieve data from GitLab project {ProjectId}", projectId);
            }
        }

        _logger.LogInformation("Retrieved total {CommitCount} commits and {MrCount} MRs from {ProjectCount} GitLab projects",
            allCommits.Count, allMergeRequests.Count, gitLabProjectIds.Count);

        // Build mapping of issue keys to commits and MRs
        var issueToCommits = new Dictionary<string, List<RawCommit>>(StringComparer.OrdinalIgnoreCase);
        var issueToMRs = new Dictionary<string, List<RawMergeRequest>>(StringComparer.OrdinalIgnoreCase);

        foreach (var commit in allCommits)
        {
            var issueKeys = _issueKeyParser.ExtractIssueKeys(commit.Message);
            foreach (var issueKey in issueKeys)
            {
                if (!issueToCommits.ContainsKey(issueKey))
                {
                    issueToCommits[issueKey] = [];
                }
                issueToCommits[issueKey].Add(commit);
            }
        }

        foreach (var mr in allMergeRequests)
        {
            var issueKeys = _issueKeyParser.ExtractIssueKeys(mr.Title);
            foreach (var issueKey in issueKeys)
            {
                if (!issueToMRs.ContainsKey(issueKey))
                {
                    issueToMRs[issueKey] = [];
                }
                issueToMRs[issueKey].Add(mr);
            }
        }

        // Calculate delivery metrics
        var leadTimes = new List<double>();
        var cycleTimes = new List<double>();
        var issuesWithCode = 0;
        var issuesWithoutCode = 0;

        foreach (var issue in jiraIssues)
        {
            var issueKey = issue.Key;
            var hasCommits = issueToCommits.ContainsKey(issueKey);
            var hasMRs = issueToMRs.ContainsKey(issueKey);

            if (!hasCommits && !hasMRs)
            {
                issuesWithoutCode++;
                continue;
            }

            issuesWithCode++;

            // Calculate cycle time: time from issue creation to first merged MR
            if (hasMRs)
            {
                var mergedMRs = issueToMRs[issueKey].Where(mr => mr.MergedAt.HasValue).ToList();
                if (mergedMRs.Count > 0 && issue.Fields.Created.HasValue)
                {
                    var firstMerge = mergedMRs.Min(mr => mr.MergedAt!.Value);
                    var created = issue.Fields.Created.Value;
                    var cycleTime = (firstMerge - created).TotalHours;
                    if (cycleTime > 0)
                    {
                        cycleTimes.Add(cycleTime);
                    }
                }
            }
        }

        return new DeliveryMetrics
        {
            ProjectKey = projectKey,
            AverageLeadTimeHours = leadTimes.Count > 0 ? leadTimes.Average() : 0,
            MedianLeadTimeHours = leadTimes.Count > 0 ? leadTimes.OrderBy(x => x).ElementAt(leadTimes.Count / 2) : 0,
            AverageCycleTimeHours = cycleTimes.Count > 0 ? cycleTimes.Average() : 0,
            MedianCycleTimeHours = cycleTimes.Count > 0 ? cycleTimes.OrderBy(x => x).ElementAt(cycleTimes.Count / 2) : 0,
            IssuesWithDeliveryData = issuesWithCode,
            IssuesWithoutCode = issuesWithoutCode,
            StartDate = startDate,
            EndDate = endDate
        };
    }

    /// <inheritdoc />
    public async Task<WorkCorrelation> CalculateWorkCorrelationAsync(
        string projectKey,
        IReadOnlyList<long> gitLabProjectIds,
        int windowDays,
        CancellationToken cancellationToken = default)
    {
        using var activity = Diagnostics.ActivitySource.StartActivity("CalculateWorkCorrelation");
        activity?.SetTag("project_key", projectKey);
        activity?.SetTag("window_days", windowDays);
        activity?.SetTag("gitlab_projects_count", gitLabProjectIds.Count);

        ArgumentException.ThrowIfNullOrWhiteSpace(projectKey);
        ArgumentNullException.ThrowIfNull(gitLabProjectIds);
        if (windowDays < 1 || windowDays > 365)
        {
            throw new ArgumentException("Window days must be between 1 and 365", nameof(windowDays));
        }

        var startDate = DateTime.UtcNow.AddDays(-windowDays);
        var endDate = DateTime.UtcNow;

        // Get Jira issues
        var jql = $"project = {projectKey} AND updated >= -{windowDays}d ORDER BY updated DESC";
        var jiraResult = await _jiraHttpClient.SearchIssuesAsync(jql, 0, 1000, cancellationToken);
        var jiraIssues = jiraResult.Issues;

        // Get GitLab data for specified projects
        var allCommits = new List<RawCommit>();
        var allMergeRequests = new List<RawMergeRequest>();

        foreach (var projectId in gitLabProjectIds)
        {
            try
            {
                var commits = await _gitLabService.GetCommitsAsync(projectId, startDate, cancellationToken);
                allCommits.AddRange(commits);

                var mergeRequests = await _gitLabService.GetMergeRequestsAsync(projectId, startDate, cancellationToken);
                allMergeRequests.AddRange(mergeRequests);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to retrieve data from GitLab project {ProjectId}", projectId);
            }
        }

        // Analyze correlation
        var commitsWithJira = 0;
        var mrsWithJira = 0;
        var issuesWithActivity = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var commit in allCommits)
        {
            var issueKeys = _issueKeyParser.ExtractIssueKeys(commit.Message);
            if (issueKeys.Count > 0)
            {
                commitsWithJira++;
                foreach (var key in issueKeys)
                {
                    issuesWithActivity.Add(key);
                }
            }
        }

        foreach (var mr in allMergeRequests)
        {
            var issueKeys = _issueKeyParser.ExtractIssueKeys(mr.Title);
            if (issueKeys.Count > 0)
            {
                mrsWithJira++;
                foreach (var key in issueKeys)
                {
                    issuesWithActivity.Add(key);
                }
            }
        }

        var jiraIssuesWithCode = jiraIssues.Count(issue => issuesWithActivity.Contains(issue.Key));

        return new WorkCorrelation
        {
            ProjectKey = projectKey,
            TotalCommits = allCommits.Count,
            CommitsWithJiraReference = commitsWithJira,
            OrphanedCommits = allCommits.Count - commitsWithJira,
            TotalMergeRequests = allMergeRequests.Count,
            MergeRequestsWithJiraReference = mrsWithJira,
            OrphanedMergeRequests = allMergeRequests.Count - mrsWithJira,
            TotalJiraIssues = jiraIssues.Count,
            JiraIssuesWithGitLabActivity = jiraIssuesWithCode,
            JiraIssuesWithoutCode = jiraIssues.Count - jiraIssuesWithCode,
            StartDate = startDate,
            EndDate = endDate
        };
    }

    /// <inheritdoc />
    public async Task<IssueActivity> GetIssueActivityAsync(
        string issueKey,
        IReadOnlyList<long> gitLabProjectIds,
        CancellationToken cancellationToken = default)
    {
        using var activity = Diagnostics.ActivitySource.StartActivity("GetIssueActivity");
        activity?.SetTag("issue_key", issueKey);
        activity?.SetTag("gitlab_projects_count", gitLabProjectIds.Count);

        ArgumentException.ThrowIfNullOrWhiteSpace(issueKey);
        ArgumentNullException.ThrowIfNull(gitLabProjectIds);

        // Get Jira issue
        var issue = await _jiraHttpClient.GetIssueAsync(issueKey, cancellationToken);
        if (issue is null)
        {
            throw new InvalidOperationException($"Jira issue {issueKey} not found");
        }

        // Get related GitLab activity from specified projects
        var allCommits = new List<RawCommit>();
        var allMergeRequests = new List<RawMergeRequest>();

        foreach (var projectId in gitLabProjectIds)
        {
            try
            {
                // Get all commits for the project (no date filter to find all refs to this issue)
                var commits = await _gitLabService.GetCommitsAsync(projectId, null, cancellationToken);
                var relevantCommits = commits.Where(c => _issueKeyParser.ExtractIssueKeys(c.Message).Contains(issueKey, StringComparer.OrdinalIgnoreCase));
                allCommits.AddRange(relevantCommits);

                // Get all MRs for the project
                var mergeRequests = await _gitLabService.GetMergeRequestsAsync(projectId, null, cancellationToken);
                var relevantMRs = mergeRequests.Where(mr => _issueKeyParser.ExtractIssueKeys(mr.Title).Contains(issueKey, StringComparer.OrdinalIgnoreCase));
                allMergeRequests.AddRange(relevantMRs);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to retrieve data from GitLab project {ProjectId} for issue {IssueKey}", projectId, issueKey);
            }
        }

        return new IssueActivity
        {
            IssueKey = issue.Key,
            IssueSummary = issue.Fields.Summary ?? string.Empty,
            IssueStatus = issue.Fields.Status?.Name ?? "Unknown",
            Commits = allCommits
                .OrderByDescending(c => c.CommittedAt)
                .Select(c => new RelatedCommit
                {
                    CommitSha = c.CommitId,
                    Message = c.Message,
                    Author = c.AuthorName,
                    CommittedAt = c.CommittedAt,
                    ProjectName = c.ProjectName
                }).ToList(),
            MergeRequests = allMergeRequests
                .OrderByDescending(mr => mr.CreatedAt)
                .Select(mr => new RelatedMergeRequest
                {
                    MergeRequestIid = mr.MrId,
                    Title = mr.Title,
                    State = mr.State,
                    Author = mr.AuthorName,
                    CreatedAt = mr.CreatedAt,
                    MergedAt = mr.MergedAt,
                    ProjectName = mr.ProjectName
                }).ToList()
        };
    }
}
