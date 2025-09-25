using Microsoft.EntityFrameworkCore;

using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Data;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Facts;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

/// <summary>
/// Service for collecting and storing user metrics snapshots over time
/// </summary>
public sealed class UserMetricsCollectionService : IUserMetricsCollectionService
{
    private readonly GitLabMetricsDbContext _dbContext;
    private readonly IUserMetricsService _userMetricsService;
    private readonly ILogger<UserMetricsCollectionService> _logger;

    public UserMetricsCollectionService(
        GitLabMetricsDbContext dbContext,
        IUserMetricsService userMetricsService,
        ILogger<UserMetricsCollectionService> logger)
    {
        _dbContext = dbContext;
        _userMetricsService = userMetricsService;
        _logger = logger;
    }

    public async Task<FactUserMetrics> CollectAndStoreUserMetricsAsync(long userId, DateTimeOffset? fromDate = null, DateTimeOffset? toDate = null, CancellationToken cancellationToken = default)
    {
        // Default to 3 months period if not specified
        var defaultFromDate = DateTimeOffset.UtcNow.AddMonths(-3);
        var defaultToDate = DateTimeOffset.UtcNow;
        
        var from = fromDate ?? defaultFromDate;
        var to = toDate ?? defaultToDate;

        _logger.LogInformation("Collecting metrics for user {UserId} from {FromDate} to {ToDate}", userId, from, to);

        // Get comprehensive user metrics from the existing service
        var userMetrics = await _userMetricsService.GetUserMetricsAsync(userId, from, to, cancellationToken);

        // Check if we already have metrics for this exact period and user
        var existingMetrics = await _dbContext.FactUserMetrics
            .Where(m => m.UserId == userId && m.FromDate == from && m.ToDate == to)
            .OrderByDescending(m => m.CollectedAt)
            .FirstOrDefaultAsync(cancellationToken);

        // If we have recent metrics (within last hour), return them instead of collecting again
        if (existingMetrics is not null && existingMetrics.CollectedAt > DateTimeOffset.UtcNow.AddHours(-1))
        {
            _logger.LogInformation("Returning existing metrics for user {UserId} collected at {CollectedAt}", userId, existingMetrics.CollectedAt);
            return existingMetrics;
        }

        // Create new FactUserMetrics entity
        var factUserMetrics = new FactUserMetrics
        {
            UserId = userMetrics.UserId,
            Username = userMetrics.UserName,
            Email = userMetrics.Email ?? "-",
            CollectedAt = DateTimeOffset.UtcNow,
            FromDate = from,
            ToDate = to,
            PeriodDays = (int)(to - from).TotalDays,
            
            // Code Contribution Metrics
            TotalCommits = userMetrics.CodeContribution.TotalCommits,
            TotalLinesAdded = userMetrics.CodeContribution.TotalLinesAdded,
            TotalLinesDeleted = userMetrics.CodeContribution.TotalLinesDeleted,
            TotalLinesChanged = userMetrics.CodeContribution.TotalLinesChanged,
            AverageCommitsPerDay = userMetrics.CodeContribution.CommitsPerDay,
            AverageLinesChangedPerCommit = userMetrics.CodeContribution.AverageCommitSize,
            ActiveProjects = userMetrics.CodeContribution.FilesModified, // Now represents actual distinct projects from commits
            
            // Code Review Metrics
            TotalMergeRequestsCreated = userMetrics.CodeReview.MergeRequestsCreated,
            TotalMergeRequestsMerged = userMetrics.CodeReview.ApprovalsReceived, // Approximation
            TotalMergeRequestsReviewed = userMetrics.CodeReview.MergeRequestsReviewed,
            AverageMergeRequestCycleTimeHours = userMetrics.CodeReview.AverageMRCycleTime?.TotalHours ?? 0,
            MergeRequestMergeRate = 1.0 - userMetrics.CodeReview.SelfMergeRate, // Inverse approximation
            
            // Quality Metrics
            TotalPipelinesTriggered = userMetrics.Quality.PipelineFailures + (int)(userMetrics.Quality.PipelineSuccessRate * 100), // Approximation
            SuccessfulPipelines = (int)(userMetrics.Quality.PipelineSuccessRate * 100), // Approximation
            FailedPipelines = userMetrics.Quality.PipelineFailures,
            PipelineSuccessRate = userMetrics.Quality.PipelineSuccessRate,
            AveragePipelineDurationMinutes = 0, // Not available in current model
            
            // Issue Management Metrics
            TotalIssuesCreated = userMetrics.IssueManagement.IssuesCreated,
            TotalIssuesAssigned = userMetrics.IssueManagement.IssuesCreated, // Approximation
            TotalIssuesClosed = userMetrics.IssueManagement.IssuesResolved,
            AverageIssueResolutionTimeHours = userMetrics.IssueManagement.AverageIssueResolutionTime?.TotalHours ?? 0,
            
            // Collaboration Metrics
            TotalCommentsOnMergeRequests = userMetrics.Collaboration.UniqueReviewers * 2, // Approximation
            TotalCommentsOnIssues = userMetrics.Collaboration.MentorshipActivities, // Approximation
            CollaborationScore = userMetrics.Collaboration.KnowledgeSharingScore,
            
            // Productivity Metrics
            ProductivityScore = userMetrics.Productivity.VelocityScore,
            ProductivityLevel = DetermineProductivityLevel(userMetrics.Productivity.VelocityScore),
            CodeChurnRate = userMetrics.Quality.CodeRevertRate,
            ReviewThroughput = userMetrics.CodeReview.ReviewParticipationRate,
            
            // Metadata
            TotalDataPoints = userMetrics.Metadata.TotalDataPoints,
            DataQuality = DetermineDataQuality(userMetrics.Metadata.TotalDataPoints, (int)(to - from).TotalDays)
        };

        // Store in database
        _dbContext.FactUserMetrics.Add(factUserMetrics);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successfully stored metrics snapshot for user {UserId} with ID {MetricsId}", userId, factUserMetrics.Id);

        return factUserMetrics;
    }

    public async Task<List<FactUserMetrics>> GetUserMetricsHistoryAsync(long userId, int limit = 10, CancellationToken cancellationToken = default)
    {
        var metrics = await _dbContext.FactUserMetrics
            .Where(m => m.UserId == userId)
            .OrderByDescending(m => m.CollectedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Retrieved {Count} historical metrics snapshots for user {UserId}", metrics.Count, userId);

        return metrics;
    }

    public async Task<List<FactUserMetrics>> GetUserMetricsInRangeAsync(long userId, DateTimeOffset fromDate, DateTimeOffset toDate, CancellationToken cancellationToken = default)
    {
        var metrics = await _dbContext.FactUserMetrics
            .Where(m => m.UserId == userId && m.CollectedAt >= fromDate && m.CollectedAt <= toDate)
            .OrderByDescending(m => m.CollectedAt)
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Retrieved {Count} metrics snapshots for user {UserId} between {FromDate} and {ToDate}", 
            metrics.Count, userId, fromDate, toDate);

        return metrics;
    }

    public async Task<UserMetricsComparison?> CompareUserMetricsAsync(long userId, DateTimeOffset baselineCollectedAt, DateTimeOffset currentCollectedAt, CancellationToken cancellationToken = default)
    {
        var baselineMetrics = await _dbContext.FactUserMetrics
            .Where(m => m.UserId == userId && m.CollectedAt <= baselineCollectedAt)
            .OrderByDescending(m => m.CollectedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var currentMetrics = await _dbContext.FactUserMetrics
            .Where(m => m.UserId == userId && m.CollectedAt <= currentCollectedAt)
            .OrderByDescending(m => m.CollectedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (baselineMetrics is null || currentMetrics is null)
        {
            _logger.LogWarning("Could not find baseline or current metrics for user {UserId} comparison", userId);
            return null;
        }

        var changes = CalculateChanges(baselineMetrics, currentMetrics);

        return new UserMetricsComparison
        {
            UserId = userId,
            Username = currentMetrics.Username,
            BaselineMetrics = baselineMetrics,
            CurrentMetrics = currentMetrics,
            Changes = changes
        };
    }

    private static UserMetricsChanges CalculateChanges(FactUserMetrics baseline, FactUserMetrics current)
    {
        var changes = new UserMetricsChanges
        {
            // Code Contribution Changes
            CommitsChange = current.TotalCommits - baseline.TotalCommits,
            CommitsChangePercent = CalculatePercentChange(baseline.TotalCommits, current.TotalCommits),
            LinesChangedChange = current.TotalLinesChanged - baseline.TotalLinesChanged,
            LinesChangedChangePercent = CalculatePercentChange(baseline.TotalLinesChanged, current.TotalLinesChanged),
            CommitsPerDayChange = current.AverageCommitsPerDay - baseline.AverageCommitsPerDay,
            CommitsPerDayChangePercent = CalculatePercentChange(baseline.AverageCommitsPerDay, current.AverageCommitsPerDay),
            
            // Code Review Changes
            MergeRequestsCreatedChange = current.TotalMergeRequestsCreated - baseline.TotalMergeRequestsCreated,
            MergeRequestsCreatedChangePercent = CalculatePercentChange(baseline.TotalMergeRequestsCreated, current.TotalMergeRequestsCreated),
            CycleTimeChange = current.AverageMergeRequestCycleTimeHours - baseline.AverageMergeRequestCycleTimeHours,
            CycleTimeChangePercent = CalculatePercentChange(baseline.AverageMergeRequestCycleTimeHours, current.AverageMergeRequestCycleTimeHours),
            MergeRateChange = current.MergeRequestMergeRate - baseline.MergeRequestMergeRate,
            MergeRateChangePercent = CalculatePercentChange(baseline.MergeRequestMergeRate, current.MergeRequestMergeRate),
            
            // Quality Changes
            PipelineSuccessRateChange = current.PipelineSuccessRate - baseline.PipelineSuccessRate,
            PipelineSuccessRateChangePercent = CalculatePercentChange(baseline.PipelineSuccessRate, current.PipelineSuccessRate),
            PipelinesTriggeredChange = current.TotalPipelinesTriggered - baseline.TotalPipelinesTriggered,
            PipelinesTriggeredChangePercent = CalculatePercentChange(baseline.TotalPipelinesTriggered, current.TotalPipelinesTriggered),
            
            // Productivity Changes
            ProductivityScoreChange = current.ProductivityScore - baseline.ProductivityScore,
            ProductivityScoreChangePercent = CalculatePercentChange(baseline.ProductivityScore, current.ProductivityScore),
            ProductivityLevelChange = baseline.ProductivityLevel == current.ProductivityLevel 
                ? null 
                : $"{baseline.ProductivityLevel} → {current.ProductivityLevel}"
        };

        // Determine overall trend and key changes
        var improvements = new List<string>();
        var concerns = new List<string>();

        if (changes.CommitsChangePercent > 10) improvements.Add("Increased commit activity");
        else if (changes.CommitsChangePercent < -10) concerns.Add("Decreased commit activity");

        if (changes.PipelineSuccessRateChangePercent > 5) improvements.Add("Better pipeline success rate");
        else if (changes.PipelineSuccessRateChangePercent < -5) concerns.Add("Declining pipeline success rate");

        if (changes.CycleTimeChangePercent < -10) improvements.Add("Faster merge request cycle time");
        else if (changes.CycleTimeChangePercent > 10) concerns.Add("Slower merge request cycle time");

        if (changes.ProductivityScoreChangePercent > 5) improvements.Add("Higher productivity score");
        else if (changes.ProductivityScoreChangePercent < -5) concerns.Add("Lower productivity score");

        var overallTrend = "Stable";
        if (improvements.Count > concerns.Count) overallTrend = "Improving";
        else if (concerns.Count > improvements.Count) overallTrend = "Declining";

        return changes with 
        { 
            OverallTrend = overallTrend,
            KeyImprovements = improvements,
            AreasOfConcern = concerns
        };
    }

    private static double CalculatePercentChange(double baseline, double current)
    {
        if (baseline == 0) return current == 0 ? 0 : 100;
        return ((current - baseline) / baseline) * 100;
    }

    private static double CalculatePercentChange(int baseline, int current)
    {
        if (baseline == 0) return current == 0 ? 0 : 100;
        return ((double)(current - baseline) / baseline) * 100;
    }

    private static string DetermineDataQuality(int totalDataPoints, int periodDays)
    {
        var dataPointsPerDay = (double)totalDataPoints / periodDays;
        
        return dataPointsPerDay switch
        {
            >= 5 => "Excellent",
            >= 3 => "Good", 
            >= 1 => "Fair",
            _ => "Poor"
        };
    }

    private static string DetermineProductivityLevel(double velocityScore)
    {
        return velocityScore switch
        {
            >= 8.0 => "High",
            >= 5.0 => "Medium",
            _ => "Low"
        };
    }
}
