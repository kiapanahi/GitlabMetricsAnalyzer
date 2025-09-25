using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models;

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
    
    /// <summary>
    /// Get trend data for user metrics over time
    /// </summary>
    Task<UserMetricsTrendsResponse> GetUserMetricsTrendsAsync(long userId, DateTimeOffset fromDate, DateTimeOffset toDate, TrendPeriod period = TrendPeriod.Weekly, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Compare user metrics with team/organization averages
    /// </summary>
    Task<UserMetricsComparisonResponse> GetUserMetricsComparisonAsync(long userId, long[] comparisonUserIds, DateTimeOffset fromDate, DateTimeOffset toDate, CancellationToken cancellationToken = default);
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
    UserIssueManagementMetrics IssueManagement,
    UserCollaborationMetrics Collaboration,
    UserQualityMetrics Quality,
    UserProductivityMetrics Productivity,
    MetricsMetadataWithQuality Metadata
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
    string ProductivityScore, // High, Medium, Low
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
    int MergeRequestsReviewed,
    double AverageMRSize,
    TimeSpan? AverageMRCycleTime,
    TimeSpan? AverageTimeToFirstReview,
    TimeSpan? AverageTimeInReview,
    double ReviewParticipationRate,
    int ApprovalsGiven,
    int ApprovalsReceived,
    double SelfMergeRate
);

/// <summary>
/// Issue management metrics
/// </summary>
public sealed record UserIssueManagementMetrics(
    int IssuesCreated,
    int IssuesResolved,
    TimeSpan? AverageIssueResolutionTime,
    double IssueResolutionRate,
    int ReopenedIssues
);

/// <summary>
/// Collaboration metrics
/// </summary>
public sealed record UserCollaborationMetrics(
    int UniqueReviewers,
    int UniqueReviewees,
    int CrossTeamCollaborations,
    double KnowledgeSharingScore,
    int MentorshipActivities
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
/// Productivity metrics
/// </summary>
public sealed record UserProductivityMetrics(
    double VelocityScore,
    double EfficiencyScore,
    double ImpactScore,
    string ProductivityTrend, // Increasing, Stable, Decreasing
    int FocusTimeHours
);

/// <summary>
/// Single trend point in time series
/// </summary>
public sealed record UserMetricsTrendPoint(
    DateTimeOffset Date,
    int Commits,
    int MergeRequests,
    double PipelineSuccessRate,
    int LinesChanged,
    double ProductivityScore
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
    int TotalLinesChanged,
    double ProductivityScore
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
/// Enhanced metadata about metrics calculation with quality indicators
/// </summary>
public sealed record MetricsMetadataWithQuality(
    DateTimeOffset CalculatedAt,
    string DataSource,
    int TotalDataPoints,
    DateTimeOffset? LastDataUpdate,
    string DataQuality,
    double OverallConfidenceScore,
    List<string> DataQualityWarnings,
    Dictionary<string, MetricQuality>? MetricQualities = null
) ;

/// <summary>
/// Trend period enumeration
/// </summary>
public enum TrendPeriod
{
    Daily,
    Weekly,
    Monthly
}
