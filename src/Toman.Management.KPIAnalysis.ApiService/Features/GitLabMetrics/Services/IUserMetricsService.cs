namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

/// <summary>
/// Enhanced user metrics service providing comprehensive developer productivity analysis
/// </summary>
public interface IUserMetricsService
{
    /// <summary>
    /// Get comprehensive metrics for a specific user
    /// </summary>
    Task<UserMetricsResponse> GetUserMetricsAsync(long userId, DateTimeOffset fromDate, DateTimeOffset toDate, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get a summary of key metrics for a user
    /// </summary>
    Task<UserMetricsSummaryResponse> GetUserMetricsSummaryAsync(long userId, DateTimeOffset fromDate, DateTimeOffset toDate, CancellationToken cancellationToken = default);
}

/// <summary>
/// Comprehensive user metrics response
/// </summary>
public sealed record UserMetricsResponse(
    long UserId,
    string UserName,
    string Email,
    DateTimeOffset FromDate,
    DateTimeOffset ToDate,
    UserCodeContributionMetrics CodeContribution,
    UserCodeReviewMetrics CodeReview,
    UserCollaborationMetrics Collaboration,
    UserQualityMetrics Quality,
    MetricsMetadata Metadata
);

/// <summary>
/// Summary of key user metrics
/// </summary>
public sealed record UserMetricsSummaryResponse(
    long UserId,
    string UserName,
    DateTimeOffset FromDate,
    DateTimeOffset ToDate,
    int TotalCommits,
    int TotalMergeRequests,
    double AverageCommitsPerDay,
    double PipelineSuccessRate,
    TimeSpan? AverageMRCycleTime,
    int TotalLinesChanged,
    MetricsMetadata Metadata
);

/// <summary>
/// User metrics trends over time
/// </summary>
public sealed record UserMetricsTrendsResponse(
    long UserId,
    string UserName,
    DateTimeOffset FromDate,
    DateTimeOffset ToDate,
    TrendPeriod Period,
    List<UserMetricsTrendPoint> TrendPoints,
    MetricsMetadata Metadata
);

/// <summary>
/// User metrics comparison with peers
/// </summary>
public sealed record UserMetricsComparisonResponse(
    long UserId,
    string UserName,
    DateTimeOffset FromDate,
    DateTimeOffset ToDate,
    UserMetricsComparisonData UserMetrics,
    UserMetricsComparisonData TeamAverage,
    List<UserMetricsComparisonData> PeerMetrics,
    MetricsMetadata Metadata
);

/// <summary>
/// Code contribution metrics
/// </summary>
public sealed record UserCodeContributionMetrics(
    int TotalCommits,
    double CommitsPerDay,
    int TotalLinesAdded,
    int TotalLinesDeleted,
    int TotalLinesChanged,
    double AverageCommitSize,
    int FilesModified, // Represents distinct active projects (count of unique ProjectIds from commits)
    List<string> TopLanguages,
    int WeekendCommits,
    int EveningCommits
);

/// <summary>
/// Code review metrics
/// </summary>
public sealed record UserCodeReviewMetrics(
    int MergeRequestsCreated,
    int MergeRequestsMerged,
    int MergeRequestsReviewed,
    double AverageMRSize,
    TimeSpan? AverageMRCycleTime,
    TimeSpan? AverageTimeToFirstReview,
    TimeSpan? AverageTimeInReview,
    double ReviewParticipationRate,
    int ApprovalsGiven,
    int ApprovalsReceived,
    double SelfMergeRate,
    double MergeRequestMergeRate
);

/// <summary>
/// Collaboration metrics
/// </summary>
public sealed record UserCollaborationMetrics(
    int UniqueReviewers,
    int UniqueReviewees,
    int CrossTeamCollaborations,
    double KnowledgeSharingScore,
    int MentorshipActivities,
    int TotalCommentsOnMergeRequests
);

/// <summary>
/// Quality metrics
/// </summary>
public sealed record UserQualityMetrics(
    double PipelineSuccessRate,
    int PipelineFailures,
    double CodeRevertRate,
    double BugFixRatio,
    double TestCoverage,
    int SecurityIssues
);

/// <summary>
/// Single trend point in time series
/// </summary>
public sealed record UserMetricsTrendPoint(
    DateTimeOffset Date,
    int Commits,
    int MergeRequests,
    double PipelineSuccessRate,
    int LinesChanged
);

/// <summary>
/// Comparison data structure
/// </summary>
public sealed record UserMetricsComparisonData(
    long? UserId,
    string Name,
    int TotalCommits,
    int TotalMergeRequests,
    double PipelineSuccessRate,
    TimeSpan? AverageMRCycleTime,
    int TotalLinesChanged
);

/// <summary>
/// Metadata about metrics calculation
/// </summary>
public sealed record MetricsMetadata(
    DateTimeOffset CalculatedAt,
    string DataSource,
    int TotalDataPoints,
    DateTimeOffset? LastDataUpdate
);

/// <summary>
/// Trend period enumeration
/// </summary>
public enum TrendPeriod
{
    Daily,
    Weekly,
    Monthly
}
