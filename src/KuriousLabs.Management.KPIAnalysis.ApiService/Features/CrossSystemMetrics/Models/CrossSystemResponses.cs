namespace KuriousLabs.Management.KPIAnalysis.ApiService.Features.CrossSystemMetrics.Models;

/// <summary>
/// Response DTO for delivery metrics across Jira and GitLab
/// </summary>
public sealed record DeliveryMetrics
{
    /// <summary>
    /// Jira project key
    /// </summary>
    public string ProjectKey { get; init; } = string.Empty;

    /// <summary>
    /// Average lead time from Jira "In Progress" to GitLab "Merged" in hours
    /// </summary>
    public double AverageLeadTimeHours { get; init; }

    /// <summary>
    /// Median lead time from Jira "In Progress" to GitLab "Merged" in hours
    /// </summary>
    public double MedianLeadTimeHours { get; init; }

    /// <summary>
    /// Average cycle time from Jira "Created" to GitLab "Merged" in hours
    /// </summary>
    public double AverageCycleTimeHours { get; init; }

    /// <summary>
    /// Median cycle time from Jira "Created" to GitLab "Merged" in hours
    /// </summary>
    public double MedianCycleTimeHours { get; init; }

    /// <summary>
    /// Number of issues with complete delivery data
    /// </summary>
    public int IssuesWithDeliveryData { get; init; }

    /// <summary>
    /// Number of issues without GitLab activity
    /// </summary>
    public int IssuesWithoutCode { get; init; }

    /// <summary>
    /// Start date of analysis window
    /// </summary>
    public DateTime StartDate { get; init; }

    /// <summary>
    /// End date of analysis window
    /// </summary>
    public DateTime EndDate { get; init; }
}

/// <summary>
/// Response DTO for work correlation between Jira and GitLab
/// </summary>
public sealed record WorkCorrelation
{
    /// <summary>
    /// Project key being analyzed
    /// </summary>
    public string ProjectKey { get; init; } = string.Empty;

    /// <summary>
    /// Total number of commits analyzed
    /// </summary>
    public int TotalCommits { get; init; }

    /// <summary>
    /// Number of commits linked to Jira issues
    /// </summary>
    public int CommitsWithJiraReference { get; init; }

    /// <summary>
    /// Number of commits without Jira issue references (orphaned)
    /// </summary>
    public int OrphanedCommits { get; init; }

    /// <summary>
    /// Total number of merge requests analyzed
    /// </summary>
    public int TotalMergeRequests { get; init; }

    /// <summary>
    /// Number of merge requests linked to Jira issues
    /// </summary>
    public int MergeRequestsWithJiraReference { get; init; }

    /// <summary>
    /// Number of merge requests without Jira references (orphaned)
    /// </summary>
    public int OrphanedMergeRequests { get; init; }

    /// <summary>
    /// Total number of Jira issues analyzed
    /// </summary>
    public int TotalJiraIssues { get; init; }

    /// <summary>
    /// Number of Jira issues with GitLab activity
    /// </summary>
    public int JiraIssuesWithGitLabActivity { get; init; }

    /// <summary>
    /// Number of Jira issues without any GitLab activity
    /// </summary>
    public int JiraIssuesWithoutCode { get; init; }

    /// <summary>
    /// Percentage of commits with Jira references
    /// </summary>
    public double CommitCorrelationPercentage => TotalCommits > 0
        ? (CommitsWithJiraReference / (double)TotalCommits) * 100
        : 0;

    /// <summary>
    /// Percentage of merge requests with Jira references
    /// </summary>
    public double MergeRequestCorrelationPercentage => TotalMergeRequests > 0
        ? (MergeRequestsWithJiraReference / (double)TotalMergeRequests) * 100
        : 0;

    /// <summary>
    /// Percentage of Jira issues with code activity
    /// </summary>
    public double JiraCodeCoveragePercentage => TotalJiraIssues > 0
        ? (JiraIssuesWithGitLabActivity / (double)TotalJiraIssues) * 100
        : 0;

    /// <summary>
    /// Start date of analysis window
    /// </summary>
    public DateTime StartDate { get; init; }

    /// <summary>
    /// End date of analysis window
    /// </summary>
    public DateTime EndDate { get; init; }
}

/// <summary>
/// Response DTO for GitLab activity related to a specific Jira issue
/// </summary>
public sealed record IssueActivity
{
    /// <summary>
    /// Jira issue key
    /// </summary>
    public string IssueKey { get; init; } = string.Empty;

    /// <summary>
    /// Jira issue summary/title
    /// </summary>
    public string IssueSummary { get; init; } = string.Empty;

    /// <summary>
    /// Jira issue status
    /// </summary>
    public string IssueStatus { get; init; } = string.Empty;

    /// <summary>
    /// Related commits
    /// </summary>
    public List<RelatedCommit> Commits { get; init; } = [];

    /// <summary>
    /// Related merge requests
    /// </summary>
    public List<RelatedMergeRequest> MergeRequests { get; init; } = [];

    /// <summary>
    /// Whether this issue has any GitLab activity
    /// </summary>
    public bool HasGitLabActivity => Commits.Count > 0 || MergeRequests.Count > 0;
}

/// <summary>
/// Commit related to a Jira issue
/// </summary>
public sealed record RelatedCommit
{
    public string CommitSha { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string Author { get; init; } = string.Empty;
    public DateTime CommittedAt { get; init; }
    public string ProjectName { get; init; } = string.Empty;
}

/// <summary>
/// Merge request related to a Jira issue
/// </summary>
public sealed record RelatedMergeRequest
{
    public long MergeRequestIid { get; init; }
    public string Title { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string Author { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? MergedAt { get; init; }
    public string ProjectName { get; init; } = string.Empty;
}
