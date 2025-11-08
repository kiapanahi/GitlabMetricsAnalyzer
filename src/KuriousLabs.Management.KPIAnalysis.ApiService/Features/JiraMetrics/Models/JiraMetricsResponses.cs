namespace KuriousLabs.Management.KPIAnalysis.ApiService.Features.JiraMetrics.Models;

/// <summary>
/// Response DTO for issue tracking metrics
/// </summary>
public sealed record IssueTrackingMetrics
{
    /// <summary>
    /// Average time to resolve issues in hours
    /// </summary>
    public double AverageResolutionTimeHours { get; init; }

    /// <summary>
    /// Median time to resolve issues in hours
    /// </summary>
    public double MedianResolutionTimeHours { get; init; }

    /// <summary>
    /// Average cycle time (created to resolved) in hours
    /// </summary>
    public double AverageCycleTimeHours { get; init; }

    /// <summary>
    /// Median cycle time in hours
    /// </summary>
    public double MedianCycleTimeHours { get; init; }

    /// <summary>
    /// Number of issues created in the time window
    /// </summary>
    public int IssuesCreated { get; init; }

    /// <summary>
    /// Number of issues resolved in the time window
    /// </summary>
    public int IssuesResolved { get; init; }

    /// <summary>
    /// Number of issues currently open
    /// </summary>
    public int IssuesOpen { get; init; }

    /// <summary>
    /// Issues created per day
    /// </summary>
    public double CreationRate { get; init; }

    /// <summary>
    /// Issues resolved per day
    /// </summary>
    public double ResolutionRate { get; init; }

    /// <summary>
    /// Start date of the analysis window
    /// </summary>
    public DateTime StartDate { get; init; }

    /// <summary>
    /// End date of the analysis window
    /// </summary>
    public DateTime EndDate { get; init; }

    /// <summary>
    /// Breakdown by issue type
    /// </summary>
    public Dictionary<string, IssueTypeMetrics> ByIssueType { get; init; } = new();
}

/// <summary>
/// Metrics for a specific issue type
/// </summary>
public sealed record IssueTypeMetrics
{
    public string IssueType { get; init; } = string.Empty;
    public int Count { get; init; }
    public double AverageResolutionTimeHours { get; init; }
    public double MedianResolutionTimeHours { get; init; }
}

/// <summary>
/// Response DTO for user-level Jira metrics
/// </summary>
public sealed record UserJiraMetrics
{
    public string AccountId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public int IssuesAssigned { get; init; }
    public int IssuesResolved { get; init; }
    public int IssuesCreated { get; init; }
    public int IssuesOpen { get; init; }
    public double AverageResolutionTimeHours { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
}

/// <summary>
/// Response DTO for project-level Jira metrics
/// </summary>
public sealed record ProjectJiraMetrics
{
    public string ProjectKey { get; init; } = string.Empty;
    public string ProjectName { get; init; } = string.Empty;
    public int BacklogSize { get; init; }
    public int IssuesInProgress { get; init; }
    public int IssuesCompleted { get; init; }
    public double VelocityIssuesPerWeek { get; init; }
    public double AverageCycleTimeHours { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public Dictionary<string, int> IssuesByStatus { get; init; } = new();
}
