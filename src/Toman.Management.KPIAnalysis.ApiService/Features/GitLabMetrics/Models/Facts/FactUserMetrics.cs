namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Facts;

/// <summary>
/// Fact table for storing timestamped user metrics snapshots for historical comparison
/// </summary>
public sealed class FactUserMetrics
{
    public long Id { get; init; }

    /// <summary>
    /// The GitLab user ID
    /// </summary>
    public long UserId { get; init; }

    /// <summary>
    /// Username from GitLab
    /// </summary>
    public required string Username { get; init; }

    /// <summary>
    /// User's email address
    /// </summary>
    public required string Email { get; init; }

    /// <summary>
    /// When this metrics snapshot was collected
    /// </summary>
    public DateTime CollectedAt { get; init; }

    /// <summary>
    /// Start date for the metrics calculation period
    /// </summary>
    public DateTimeOffset FromDate { get; init; }

    /// <summary>
    /// End date for the metrics calculation period
    /// </summary>
    public DateTimeOffset ToDate { get; init; }

    /// <summary>
    /// Duration of the metrics period in days
    /// </summary>
    public int PeriodDays { get; init; }

    // Code Contribution Metrics
    public int TotalCommits { get; init; }
    public int TotalLinesAdded { get; init; }
    public int TotalLinesDeleted { get; init; }
    public int TotalLinesChanged { get; init; }
    public double AverageCommitsPerDay { get; init; }
    public double AverageLinesChangedPerCommit { get; init; }
    public int ActiveProjects { get; init; }

    // Code Review Metrics
    public int TotalMergeRequestsCreated { get; init; }
    public int TotalMergeRequestsMerged { get; init; }
    public int TotalMergeRequestsReviewed { get; init; }
    public double AverageMergeRequestCycleTimeHours { get; init; }
    public double MergeRequestMergeRate { get; init; }

    // Quality Metrics
    public int TotalPipelinesTriggered { get; init; }
    public int SuccessfulPipelines { get; init; }
    public int FailedPipelines { get; init; }
    public double PipelineSuccessRate { get; init; }
    public double AveragePipelineDurationMinutes { get; init; }

    // Collaboration Metrics
    public int TotalCommentsOnMergeRequests { get; init; }
    public int TotalCommentsOnIssues { get; init; }
    public double CollaborationScore { get; init; }

    // Issue Management Metrics (to be removed in PRD refactoring)
    public int TotalIssuesCreated { get; init; }
    public int TotalIssuesAssigned { get; init; }
    public int TotalIssuesClosed { get; init; }
    public double AverageIssueResolutionTimeHours { get; init; }

    // Productivity Metrics
    public double ProductivityScore { get; init; }
    public string? ProductivityLevel { get; init; } // High, Medium, Low
    public double CodeChurnRate { get; init; }
    public double ReviewThroughput { get; init; }

    // Metadata
    public int TotalDataPoints { get; init; }
    public string? DataQuality { get; init; } // Excellent, Good, Fair, Poor
}
