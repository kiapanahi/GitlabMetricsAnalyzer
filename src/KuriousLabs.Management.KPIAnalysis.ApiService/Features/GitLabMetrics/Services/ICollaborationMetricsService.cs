namespace KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

/// <summary>
/// Service for calculating collaboration and review metrics from live GitLab data
/// </summary>
public interface ICollaborationMetricsService
{
    /// <summary>
    /// Calculates collaboration metrics for a developer across all projects
    /// </summary>
    /// <param name="userId">The GitLab user ID</param>
    /// <param name="windowDays">Number of days to look back (default: 30)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collaboration metrics result</returns>
    Task<CollaborationMetricsResult> CalculateCollaborationMetricsAsync(
        long userId,
        int windowDays = 30,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of collaboration metrics calculation
/// </summary>
public sealed class CollaborationMetricsResult
{
    /// <summary>
    /// One-line description of this metric
    /// </summary>
    public string Description => "Measures code review participation, peer feedback quality, and team collaboration effectiveness";

    /// <summary>
    /// The GitLab user ID
    /// </summary>
    public required long UserId { get; init; }

    /// <summary>
    /// The username
    /// </summary>
    public required string Username { get; init; }

    /// <summary>
    /// Number of days analyzed
    /// </summary>
    public required int WindowDays { get; init; }

    /// <summary>
    /// Start date of the analysis period (UTC)
    /// </summary>
    public required DateTime WindowStart { get; init; }

    /// <summary>
    /// End date of the analysis period (UTC)
    /// </summary>
    public required DateTime WindowEnd { get; init; }

    /// <summary>
    /// Metric 1: Number of review comments made by developer (as reviewer)
    /// Direction: ↑ good (participation)
    /// </summary>
    public required int ReviewCommentsGiven { get; init; }

    /// <summary>
    /// Metric 2: Number of comments received on developer's MRs
    /// Direction: context-dependent
    /// </summary>
    public required int ReviewCommentsReceived { get; init; }

    /// <summary>
    /// Metric 3: Number of approvals given by developer
    /// Direction: ↑ good
    /// </summary>
    public required int ApprovalsGiven { get; init; }

    /// <summary>
    /// Metric 4a: Number of resolved discussion threads
    /// </summary>
    public required int ResolvedDiscussionThreads { get; init; }

    /// <summary>
    /// Metric 4b: Number of unresolved discussion threads
    /// Direction: ↓ good for unresolved
    /// </summary>
    public required int UnresolvedDiscussionThreads { get; init; }

    /// <summary>
    /// Metric 5: MRs merged without external review/approval (count)
    /// Direction: ↓ good (indicates low review coverage)
    /// </summary>
    public required int SelfMergedMrsCount { get; init; }

    /// <summary>
    /// Metric 5b: Ratio of self-merged MRs to total merged MRs
    /// Direction: ↓ good
    /// </summary>
    public decimal? SelfMergedMrsRatio { get; init; }

    /// <summary>
    /// Metric 6: Median time to provide review after being assigned/requested (hours)
    /// Direction: ↓ good
    /// </summary>
    public decimal? ReviewTurnaroundTimeMedianH { get; init; }

    /// <summary>
    /// Metric 7: Average comment length (characters) as indicator of review depth
    /// Direction: ↑ good (quality indicator)
    /// </summary>
    public decimal? ReviewDepthScoreAvgChars { get; init; }

    /// <summary>
    /// Total number of MRs created by the developer in the window
    /// </summary>
    public required int TotalMrsCreated { get; init; }

    /// <summary>
    /// Total number of MRs merged in the window
    /// </summary>
    public required int TotalMrsMerged { get; init; }

    /// <summary>
    /// Number of MRs reviewed (where developer provided comments as reviewer)
    /// </summary>
    public required int MrsReviewed { get; init; }

    /// <summary>
    /// Projects included in the analysis
    /// </summary>
    public required List<ProjectCollaborationSummary> Projects { get; init; }

    /// <summary>
    /// Perspective details
    /// </summary>
    public required CollaborationPerspectives Perspectives { get; init; }
}

/// <summary>
/// Breakdown of metrics by perspective (as author vs as reviewer)
/// </summary>
public sealed class CollaborationPerspectives
{
    /// <summary>
    /// Metrics from the perspective of being an MR author
    /// </summary>
    public required AsAuthorPerspective AsAuthor { get; init; }

    /// <summary>
    /// Metrics from the perspective of being a reviewer
    /// </summary>
    public required AsReviewerPerspective AsReviewer { get; init; }
}

/// <summary>
/// Metrics when acting as an MR author
/// </summary>
public sealed class AsAuthorPerspective
{
    /// <summary>
    /// Number of MRs created
    /// </summary>
    public required int MrsCreated { get; init; }

    /// <summary>
    /// Comments received on own MRs
    /// </summary>
    public required int CommentsReceived { get; init; }

    /// <summary>
    /// Number of self-merged MRs (without approval)
    /// </summary>
    public required int SelfMergedMrs { get; init; }

    /// <summary>
    /// Discussion threads on own MRs
    /// </summary>
    public required int DiscussionThreads { get; init; }

    /// <summary>
    /// Resolved discussion threads on own MRs
    /// </summary>
    public required int ResolvedThreads { get; init; }
}

/// <summary>
/// Metrics when acting as a reviewer
/// </summary>
public sealed class AsReviewerPerspective
{
    /// <summary>
    /// Number of MRs reviewed
    /// </summary>
    public required int MrsReviewed { get; init; }

    /// <summary>
    /// Comments given on others' MRs
    /// </summary>
    public required int CommentsGiven { get; init; }

    /// <summary>
    /// Approvals provided
    /// </summary>
    public required int ApprovalsGiven { get; init; }

    /// <summary>
    /// Median turnaround time for reviews (hours)
    /// </summary>
    public decimal? ReviewTurnaroundMedianH { get; init; }

    /// <summary>
    /// Average review depth (comment length)
    /// </summary>
    public decimal? AvgReviewDepthChars { get; init; }
}

/// <summary>
/// Summary of collaboration metrics per project
/// </summary>
public sealed class ProjectCollaborationSummary
{
    public required long ProjectId { get; init; }
    public required string ProjectName { get; init; }
    public required int MrsCreated { get; init; }
    public required int MrsReviewed { get; init; }
    public required int CommentsGiven { get; init; }
    public required int CommentsReceived { get; init; }
}
