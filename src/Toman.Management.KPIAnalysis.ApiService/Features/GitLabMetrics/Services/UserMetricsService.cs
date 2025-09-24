using Microsoft.EntityFrameworkCore;

using System.Text.Json;

using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Data;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

/// <summary>
/// Enhanced user metrics service providing comprehensive developer productivity analysis
/// </summary>
public sealed class UserMetricsService : IUserMetricsService
{
    private readonly GitLabMetricsDbContext _dbContext;
    private readonly IGitLabService _gitLabService;
    private readonly ILogger<UserMetricsService> _logger;

    public UserMetricsService(
        GitLabMetricsDbContext dbContext,
        IGitLabService gitLabService,
        ILogger<UserMetricsService> logger)
    {
        _dbContext = dbContext;
        _gitLabService = gitLabService;
        _logger = logger;
    }

    public async Task<UserMetricsResponse> GetUserMetricsAsync(long userId, DateTimeOffset fromDate, DateTimeOffset toDate, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Calculating comprehensive metrics for user {UserId} from {FromDate} to {ToDate}", userId, fromDate, toDate);

        ValidateDateRange(fromDate, toDate);

        // Get user information
        var user = await GetUserInfoAsync(userId, cancellationToken);
        if (user is null)
        {
            throw new ArgumentException($"User with ID {userId} not found", nameof(userId));
        }

        // Fetch all user data in parallel for better performance
        var (commits, mergeRequests, pipelines, issues, reviewedMRs) = await FetchUserDataAsync(userId, fromDate, toDate, cancellationToken);

        // Calculate different metric categories
        var codeContribution = CalculateCodeContributionMetrics(commits, fromDate, toDate);
        var codeReview = await CalculateCodeReviewMetricsAsync(mergeRequests, reviewedMRs, cancellationToken);
        var issueManagement = CalculateIssueManagementMetrics(issues);
        var collaboration = await CalculateCollaborationMetricsAsync(mergeRequests, reviewedMRs, cancellationToken);
        var quality = CalculateQualityMetrics(pipelines, commits, mergeRequests);
        var productivity = CalculateProductivityMetrics(commits, mergeRequests, pipelines, fromDate, toDate);

        var metadata = new MetricsMetadata(
            DateTimeOffset.UtcNow,
            "GitLab API",
            commits.Count + mergeRequests.Count + pipelines.Count + issues.Count,
            commits.Select(c => c.IngestedAt)
                   .Concat(mergeRequests.Select(mr => mr.IngestedAt))
                   .Concat(pipelines.Select(p => p.IngestedAt))
                   .Concat(issues.Select(i => i.CreatedAt))
                   .DefaultIfEmpty()
                   .Max()
        );

        return new UserMetricsResponse(
            userId,
            user.Username,
            user.Email,
            fromDate,
            toDate,
            codeContribution,
            codeReview,
            issueManagement,
            collaboration,
            quality,
            productivity,
            metadata
        );
    }

    public async Task<UserMetricsSummaryResponse> GetUserMetricsSummaryAsync(long userId, DateTimeOffset fromDate, DateTimeOffset toDate, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Calculating summary metrics for user {UserId} from {FromDate} to {ToDate}", userId, fromDate, toDate);

        ValidateDateRange(fromDate, toDate);

        var user = await GetUserInfoAsync(userId, cancellationToken);
        if (user is null)
        {
            throw new ArgumentException($"User with ID {userId} not found", nameof(userId));
        }

        var (commits, mergeRequests, pipelines, _, _) = await FetchUserDataAsync(userId, fromDate, toDate, cancellationToken);

        var daysDiff = Math.Max(1, (toDate - fromDate).TotalDays);
        var totalCommits = commits.Count;
        var totalMergeRequests = mergeRequests.Count;
        var averageCommitsPerDay = totalCommits / daysDiff;

        var successfulPipelines = pipelines.Count(p => p.IsSuccessful);
        var pipelineSuccessRate = pipelines.Count > 0 ? (double)successfulPipelines / pipelines.Count : 0.0;

        var mergedMRs = mergeRequests.Where(mr => mr.MergedAt.HasValue).ToList();
        var averageMRCycleTime = mergedMRs.Count > 0
            ? TimeSpan.FromTicks((long)mergedMRs.Average(mr => (mr.MergedAt!.Value - mr.CreatedAt).Ticks))
            : (TimeSpan?)null;

        var totalLinesChanged = commits.Sum(c => c.Additions + c.Deletions);
        var productivityScore = CalculateProductivityScore(totalCommits, totalMergeRequests, pipelineSuccessRate, daysDiff);

        var metadata = new MetricsMetadata(
            DateTimeOffset.UtcNow,
            "GitLab API",
            commits.Count + mergeRequests.Count + pipelines.Count,
            null
        );

        return new UserMetricsSummaryResponse(
            userId,
            user.Username,
            fromDate,
            toDate,
            totalCommits,
            totalMergeRequests,
            averageCommitsPerDay,
            pipelineSuccessRate,
            averageMRCycleTime,
            totalLinesChanged,
            productivityScore,
            metadata
        );
    }

    public async Task<UserMetricsTrendsResponse> GetUserMetricsTrendsAsync(long userId, DateTimeOffset fromDate, DateTimeOffset toDate, TrendPeriod period = TrendPeriod.Weekly, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Calculating trend metrics for user {UserId} from {FromDate} to {ToDate} with period {Period}", userId, fromDate, toDate, period);

        ValidateDateRange(fromDate, toDate);

        var user = await GetUserInfoAsync(userId, cancellationToken);
        if (user is null)
        {
            throw new ArgumentException($"User with ID {userId} not found", nameof(userId));
        }

        var trendPoints = new List<UserMetricsTrendPoint>();
        var currentDate = fromDate;

        while (currentDate < toDate)
        {
            var periodEnd = period switch
            {
                TrendPeriod.Daily => currentDate.AddDays(1),
                TrendPeriod.Weekly => currentDate.AddDays(7),
                TrendPeriod.Monthly => currentDate.AddMonths(1),
                _ => currentDate.AddDays(7)
            };

            if (periodEnd > toDate)
                periodEnd = toDate;

            var (commits, mergeRequests, pipelines, _, _) = await FetchUserDataAsync(userId, currentDate, periodEnd, cancellationToken);

            var successfulPipelines = pipelines.Count(p => p.IsSuccessful);
            var pipelineSuccessRate = pipelines.Count > 0 ? (double)successfulPipelines / pipelines.Count : 0.0;
            var linesChanged = commits.Sum(c => c.Additions + c.Deletions);
            var productivityScore = CalculateNumericProductivityScore(commits.Count, mergeRequests.Count, pipelineSuccessRate, (periodEnd - currentDate).TotalDays);

            trendPoints.Add(new UserMetricsTrendPoint(
                currentDate,
                commits.Count,
                mergeRequests.Count,
                pipelineSuccessRate,
                linesChanged,
                productivityScore
            ));

            currentDate = periodEnd;
        }

        var metadata = new MetricsMetadata(
            DateTimeOffset.UtcNow,
            "GitLab API",
            trendPoints.Sum(tp => tp.Commits + tp.MergeRequests),
            null
        );

        return new UserMetricsTrendsResponse(
            userId,
            user.Username,
            fromDate,
            toDate,
            period,
            trendPoints,
            metadata
        );
    }

    public async Task<UserMetricsComparisonResponse> GetUserMetricsComparisonAsync(long userId, long[] comparisonUserIds, DateTimeOffset fromDate, DateTimeOffset toDate, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Calculating comparison metrics for user {UserId} with {ComparisonCount} peers from {FromDate} to {ToDate}", userId, comparisonUserIds.Length, fromDate, toDate);

        ValidateDateRange(fromDate, toDate);

        var user = await GetUserInfoAsync(userId, cancellationToken);
        if (user is null)
        {
            throw new ArgumentException($"User with ID {userId} not found", nameof(userId));
        }

        // Calculate metrics for the target user
        var userMetrics = await CalculateComparisonMetricsForUserAsync(userId, fromDate, toDate, cancellationToken);

        // Calculate metrics for comparison users
        var peerMetrics = new List<UserMetricsComparisonData>();
        foreach (var comparisonUserId in comparisonUserIds)
        {
            var peerMetric = await CalculateComparisonMetricsForUserAsync(comparisonUserId, fromDate, toDate, cancellationToken);
            if (peerMetric is not null)
            {
                peerMetrics.Add(peerMetric);
            }
        }

        // Calculate team average
        var allMetrics = new[] { userMetrics }.Concat(peerMetrics).Where(m => m is not null).Cast<UserMetricsComparisonData>().ToList();
        var teamAverage = CalculateTeamAverage(allMetrics);

        var metadata = new MetricsMetadata(
            DateTimeOffset.UtcNow,
            "GitLab API",
            allMetrics.Count,
            null
        );

        return new UserMetricsComparisonResponse(
            userId,
            user.Username,
            fromDate,
            toDate,
            userMetrics!,
            teamAverage,
            peerMetrics,
            metadata
        );
    }

    #region Private Helper Methods

    private static void ValidateDateRange(DateTimeOffset fromDate, DateTimeOffset toDate)
    {
        if (toDate <= fromDate)
        {
            throw new ArgumentException("ToDate must be after FromDate");
        }

        if ((toDate - fromDate).TotalDays > 365)
        {
            throw new ArgumentException("Date range cannot exceed 365 days");
        }
    }

    private async Task<NGitLab.Models.User?> GetUserInfoAsync(long userId, CancellationToken cancellationToken)
    {
        // First try to get user info from GitLab API directly
        var gitLabUser = await _gitLabService.GetUserByIdAsync(userId, cancellationToken);
        if (gitLabUser is not null)
        {
            _logger.LogDebug("Retrieved user {UserId} directly from GitLab: {Username}", userId, gitLabUser.Username);
            return gitLabUser;
        }

        // If not found in GitLab API, try to find in local database as fallback
        var dbUser = await _dbContext.DimUsers
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        if (dbUser is not null)
        {
            _logger.LogDebug("Retrieved user {UserId} from local database: {Username}", userId, dbUser.Name);
            // Convert DimUser to NGitLab User for consistency
            return new NGitLab.Models.User
            {
                Id = dbUser.UserId,
                Username = dbUser.Username,
                Email = dbUser.Email,
                Name = dbUser.Name
            };
        }

        _logger.LogWarning("User {UserId} not found in GitLab API or local database", userId);
        return null;
    }

    private async Task<(
        List<Models.Raw.RawCommit> commits,
        List<Models.Raw.RawMergeRequest> mergeRequests,
        List<Models.Raw.RawPipeline> pipelines,
        List<Models.Raw.RawIssue> issues,
        List<Models.Raw.RawMergeRequest> reviewedMRs
    )> FetchUserDataAsync(long userId, DateTimeOffset fromDate, DateTimeOffset toDate, CancellationToken cancellationToken)
    {
        // Get user information first
        var user = await GetUserInfoAsync(userId, cancellationToken);
        if (user is null)
        {
            _logger.LogWarning("User {UserId} not found for data fetching", userId);
            return (new(), new(), new(), new(), new());
        }

        _logger.LogInformation("Fetching on-demand data for user {UserId} ({UserEmail}) from {FromDate} to {ToDate}",
            userId, user.Email, fromDate, toDate);

        var allCommits = new List<Models.Raw.RawCommit>();
        var allMergeRequests = new List<Models.Raw.RawMergeRequest>();
        var allPipelines = new List<Models.Raw.RawPipeline>();
        var allIssues = new List<Models.Raw.RawIssue>();

        try
        {
            // Get projects the user is involved with
            var userProjects = await _gitLabService.GetUserProjectsAsync(userId, cancellationToken);
            _logger.LogDebug("Found {ProjectCount} projects for user {UserId}", userProjects.Count, userId);

            // Fetch data from each project in parallel
            var projectTasks = userProjects.Select(async project =>
            {
                try
                {
                    // Get commits filtered by user email
                    var commits = await _gitLabService.GetCommitsByUserEmailAsync(project.Id, user.Email!, fromDate, cancellationToken);

                    // Get merge requests for this project
                    var mergeRequests = await _gitLabService.GetMergeRequestsAsync(project.Id, fromDate, cancellationToken);
                    var userMRs = mergeRequests.Where(mr => mr.AuthorUserId == userId).ToList();

                    // Get pipelines for this project
                    var pipelines = await _gitLabService.GetPipelinesAsync(project.Id, fromDate, cancellationToken);
                    var userPipelines = pipelines.Where(p => p.AuthorUserId == userId).ToList();

                    _logger.LogDebug("Project {ProjectId}: {CommitCount} commits, {MRCount} MRs, {PipelineCount} pipelines",
                        project.Id, commits.Count, userMRs.Count, userPipelines.Count);

                    return (commits: commits.ToList(), mergeRequests: userMRs, pipelines: userPipelines);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch data from project {ProjectId} for user {UserId}", project.Id, userId);
                    return (commits: new List<Models.Raw.RawCommit>(),
                           mergeRequests: new List<Models.Raw.RawMergeRequest>(),
                           pipelines: new List<Models.Raw.RawPipeline>());
                }
            });

            var projectResults = await Task.WhenAll(projectTasks);

            // Aggregate results from all projects
            foreach (var (commits, mergeRequests, pipelines) in projectResults)
            {
                allCommits.AddRange(commits.Where(c => c.CommittedAt >= fromDate && c.CommittedAt < toDate));
                allMergeRequests.AddRange(mergeRequests.Where(mr => mr.CreatedAt >= fromDate && mr.CreatedAt < toDate));
                allPipelines.AddRange(pipelines.Where(p => p.CreatedAt >= fromDate && p.CreatedAt < toDate));
            }

            // Note: Issues are typically project-scoped and would need additional API calls
            // For now, returning empty list - this could be enhanced later
            _logger.LogDebug("Issue fetching not implemented in on-demand mode yet");

            _logger.LogInformation("Fetched on-demand data for user {UserId}: {CommitCount} commits, {MRCount} MRs, {PipelineCount} pipelines",
                userId, allCommits.Count, allMergeRequests.Count, allPipelines.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch on-demand data for user {UserId}", userId);
        }

        // Find MRs where the user was a reviewer (from the fetched MRs)
        var reviewedMRs = allMergeRequests
            .Where(mr => mr.ReviewerIds != null && mr.ReviewerIds.Contains(userId.ToString()))
            .ToList();

        return (allCommits, allMergeRequests, allPipelines, allIssues, reviewedMRs);
    }

    private static UserCodeContributionMetrics CalculateCodeContributionMetrics(
        List<Models.Raw.RawCommit> commits,
        DateTimeOffset fromDate,
        DateTimeOffset toDate)
    {
        var daysDiff = Math.Max(1, (toDate - fromDate).TotalDays);
        var totalCommits = commits.Count;
        var commitsPerDay = totalCommits / daysDiff;

        var totalLinesAdded = commits.Sum(c => c.Additions);
        var totalLinesDeleted = commits.Sum(c => c.Deletions);
        var totalLinesChanged = totalLinesAdded + totalLinesDeleted;

        var averageCommitSize = commits.Count > 0 ? (double)totalLinesChanged / commits.Count : 0;

        // Simple file count estimation (would need more data for accurate count)
        var filesModified = commits.Count; // Placeholder

        // Weekend and evening commits (assuming work hours 9-17, weekdays)
        var weekendCommits = commits.Count(c => c.CommittedAt.DayOfWeek is DayOfWeek.Thursday or DayOfWeek.Friday);
        var eveningCommits = commits.Count(c => c.CommittedAt.Hour < 9 || c.CommittedAt.Hour > 17);

        return new UserCodeContributionMetrics(
            totalCommits,
            commitsPerDay,
            totalLinesAdded,
            totalLinesDeleted,
            totalLinesChanged,
            averageCommitSize,
            filesModified,
            new List<string>(), // Would need file extension analysis
            weekendCommits,
            eveningCommits
        );
    }

    private async Task<UserCodeReviewMetrics> CalculateCodeReviewMetricsAsync(
        List<Models.Raw.RawMergeRequest> mergeRequests,
        List<Models.Raw.RawMergeRequest> reviewedMRs,
        CancellationToken cancellationToken)
    {
        var mergeRequestsCreated = mergeRequests.Count;
        var mergeRequestsReviewed = reviewedMRs.Count;

        var averageMRSize = mergeRequests.Count > 0 ? mergeRequests.Average(mr => mr.ChangesCount) : 0;

        var mergedMRs = mergeRequests.Where(mr => mr.MergedAt.HasValue).ToList();
        var averageMRCycleTime = mergedMRs.Count > 0
            ? TimeSpan.FromTicks((long)mergedMRs.Average(mr => (mr.MergedAt!.Value - mr.CreatedAt).Ticks))
            : (TimeSpan?)null;

        var mrsWithFirstReview = mergeRequests.Where(mr => mr.FirstReviewAt.HasValue).ToList();
        var averageTimeToFirstReview = mrsWithFirstReview.Count > 0
            ? TimeSpan.FromTicks((long)mrsWithFirstReview.Average(mr => (mr.FirstReviewAt!.Value - mr.CreatedAt).Ticks))
            : (TimeSpan?)null;

        var averageTimeInReview = mrsWithFirstReview.Count > 0 && mergedMRs.Count > 0
            ? TimeSpan.FromTicks((long)mrsWithFirstReview.Where(mr => mr.MergedAt.HasValue)
                .Average(mr => (mr.MergedAt!.Value - mr.FirstReviewAt!.Value).Ticks))
            : (TimeSpan?)null;

        // Calculate total possible reviews (excluding own MRs)
        var firstMR = mergeRequests.FirstOrDefault();
        var currentUserAuthId = firstMR?.AuthorUserId ?? 0;
        var totalPossibleReviews = await _dbContext.RawMergeRequests
            .CountAsync(mr => mr.AuthorUserId != currentUserAuthId, cancellationToken);

        var reviewParticipationRate = totalPossibleReviews > 0 ? (double)mergeRequestsReviewed / totalPossibleReviews : 0;

        var approvalsGiven = reviewedMRs.Sum(mr => mr.ApprovalsGiven); // This would need more detailed tracking
        var approvalsReceived = mergeRequests.Sum(mr => mr.ApprovalsGiven);

        // Self-merge rate (MRs merged without external review)
        var selfMergedMRs = mergeRequests.Count(mr => mr.MergedAt.HasValue && !mr.FirstReviewAt.HasValue);
        var selfMergeRate = mergeRequests.Count > 0 ? (double)selfMergedMRs / mergeRequests.Count : 0;

        return new UserCodeReviewMetrics(
            mergeRequestsCreated,
            mergeRequestsReviewed,
            averageMRSize,
            averageMRCycleTime,
            averageTimeToFirstReview,
            averageTimeInReview,
            reviewParticipationRate,
            approvalsGiven,
            approvalsReceived,
            selfMergeRate
        );
    }

    private static UserIssueManagementMetrics CalculateIssueManagementMetrics(List<Models.Raw.RawIssue> issues)
    {
        var issuesCreated = issues.Count;
        var issuesResolved = issues.Count(i => i.ClosedAt.HasValue);

        var resolvedIssues = issues.Where(i => i.ClosedAt.HasValue).ToList();
        var averageIssueResolutionTime = resolvedIssues.Count > 0
            ? TimeSpan.FromTicks((long)resolvedIssues.Average(i => (i.ClosedAt!.Value - i.CreatedAt).Ticks))
            : (TimeSpan?)null;

        var issueResolutionRate = issuesCreated > 0 ? (double)issuesResolved / issuesCreated : 0;
        var reopenedIssues = issues.Sum(i => i.ReopenedCount);

        return new UserIssueManagementMetrics(
            issuesCreated,
            issuesResolved,
            averageIssueResolutionTime,
            issueResolutionRate,
            reopenedIssues
        );
    }

    private Task<UserCollaborationMetrics> CalculateCollaborationMetricsAsync(
        List<Models.Raw.RawMergeRequest> mergeRequests,
        List<Models.Raw.RawMergeRequest> reviewedMRs,
        CancellationToken cancellationToken)
    {
        // Count unique reviewers who reviewed this user's MRs
        var uniqueReviewers = mergeRequests
            .Where(mr => !string.IsNullOrEmpty(mr.ReviewerIds))
            .SelectMany(mr => ParseReviewerIds(mr.ReviewerIds!))
            .Distinct()
            .Count();

        // Count unique authors whose MRs this user reviewed
        var uniqueReviewees = reviewedMRs
            .Select(mr => mr.AuthorUserId)
            .Distinct()
            .Count();

        // Cross-team collaborations would need project/team mapping
        var crossTeamCollaborations = 0; // Placeholder

        // Knowledge sharing score based on review activity
        var knowledgeSharingScore = CalculateKnowledgeSharingScore(uniqueReviewers, uniqueReviewees, reviewedMRs.Count);

        // Mentorship activities would need more detailed analysis
        var mentorshipActivities = 0; // Placeholder

        return Task.FromResult(new UserCollaborationMetrics(
            uniqueReviewers,
            uniqueReviewees,
            crossTeamCollaborations,
            knowledgeSharingScore,
            mentorshipActivities
        ));
    }

    private static UserQualityMetrics CalculateQualityMetrics(
        List<Models.Raw.RawPipeline> pipelines,
        List<Models.Raw.RawCommit> commits,
        List<Models.Raw.RawMergeRequest> mergeRequests)
    {
        var successfulPipelines = pipelines.Count(p => p.IsSuccessful);
        var pipelineSuccessRate = pipelines.Count > 0 ? (double)successfulPipelines / pipelines.Count : 0;
        var pipelineFailures = pipelines.Count - successfulPipelines;

        // Code revert rate would need commit message analysis
        var codeRevertRate = 0.0; // Placeholder

        // Bug fix ratio would need issue linking
        var bugFixRatio = 0.0; // Placeholder

        // Test coverage would need additional data
        var testCoverage = 0.0; // Placeholder

        // Security issues would need integration with security scanning
        var securityIssues = 0; // Placeholder

        return new UserQualityMetrics(
            pipelineSuccessRate,
            pipelineFailures,
            codeRevertRate,
            bugFixRatio,
            testCoverage,
            securityIssues
        );
    }

    private static UserProductivityMetrics CalculateProductivityMetrics(
        List<Models.Raw.RawCommit> commits,
        List<Models.Raw.RawMergeRequest> mergeRequests,
        List<Models.Raw.RawPipeline> pipelines,
        DateTimeOffset fromDate,
        DateTimeOffset toDate)
    {
        var daysDiff = Math.Max(1, (toDate - fromDate).TotalDays);

        // Velocity score based on commits and MRs
        var velocityScore = CalculateVelocityScore(commits.Count, mergeRequests.Count, daysDiff);

        // Efficiency score based on pipeline success rate and MR cycle time
        var efficiencyScore = CalculateEfficiencyScore(pipelines, mergeRequests);

        // Impact score based on lines changed and MR complexity
        var impactScore = CalculateImpactScore(commits, mergeRequests);

        // Productivity trend would need historical comparison
        var productivityTrend = "Stable"; // Placeholder

        // Focus time estimation based on commit patterns
        var focusTimeHours = EstimateFocusTime(commits, daysDiff);

        return new UserProductivityMetrics(
            velocityScore,
            efficiencyScore,
            impactScore,
            productivityTrend,
            focusTimeHours
        );
    }

    private async Task<UserMetricsComparisonData?> CalculateComparisonMetricsForUserAsync(
        long userId,
        DateTimeOffset fromDate,
        DateTimeOffset toDate,
        CancellationToken cancellationToken)
    {
        var user = await GetUserInfoAsync(userId, cancellationToken);
        if (user is null) return null;

        var (commits, mergeRequests, pipelines, _, _) = await FetchUserDataAsync(userId, fromDate, toDate, cancellationToken);

        var successfulPipelines = pipelines.Count(p => p.IsSuccessful);
        var pipelineSuccessRate = pipelines.Count > 0 ? (double)successfulPipelines / pipelines.Count : 0;

        var mergedMRs = mergeRequests.Where(mr => mr.MergedAt.HasValue).ToList();
        var averageMRCycleTime = mergedMRs.Count > 0
            ? TimeSpan.FromTicks((long)mergedMRs.Average(mr => (mr.MergedAt!.Value - mr.CreatedAt).Ticks))
            : (TimeSpan?)null;

        var totalLinesChanged = commits.Sum(c => c.Additions + c.Deletions);
        var daysDiff = Math.Max(1, (toDate - fromDate).TotalDays);
        var productivityScore = CalculateNumericProductivityScore(commits.Count, mergeRequests.Count, pipelineSuccessRate, daysDiff);

        return new UserMetricsComparisonData(
            userId,
            user.Username,
            commits.Count,
            mergeRequests.Count,
            pipelineSuccessRate,
            averageMRCycleTime,
            totalLinesChanged,
            productivityScore
        );
    }

    private static UserMetricsComparisonData CalculateTeamAverage(List<UserMetricsComparisonData> allMetrics)
    {
        if (allMetrics.Count == 0)
        {
            return new UserMetricsComparisonData(null, "Team Average", 0, 0, 0, null, 0, 0);
        }

        var avgCommits = (int)allMetrics.Average(m => m.TotalCommits);
        var avgMRs = (int)allMetrics.Average(m => m.TotalMergeRequests);
        var avgPipelineSuccess = allMetrics.Average(m => m.PipelineSuccessRate);
        var avgLinesChanged = (int)allMetrics.Average(m => m.TotalLinesChanged);
        var avgProductivity = allMetrics.Average(m => m.ProductivityScore);

        // Calculate average cycle time for non-null values
        var validCycleTimes = allMetrics.Where(m => m.AverageMRCycleTime.HasValue).ToList();
        var avgCycleTime = validCycleTimes.Count > 0
            ? TimeSpan.FromTicks((long)validCycleTimes.Average(m => m.AverageMRCycleTime!.Value.Ticks))
            : (TimeSpan?)null;

        return new UserMetricsComparisonData(
            null,
            "Team Average",
            avgCommits,
            avgMRs,
            avgPipelineSuccess,
            avgCycleTime,
            avgLinesChanged,
            avgProductivity
        );
    }

    #region Calculation Helpers

    private static string CalculateProductivityScore(int commits, int mergeRequests, double pipelineSuccessRate, double days)
    {
        var score = CalculateNumericProductivityScore(commits, mergeRequests, pipelineSuccessRate, days);

        return score switch
        {
            >= 7.5 => "High",
            >= 5.0 => "Medium",
            _ => "Low"
        };
    }

    private static double CalculateNumericProductivityScore(int commits, int mergeRequests, double pipelineSuccessRate, double days)
    {
        var commitsPerDay = commits / Math.Max(1, days);
        var mrsPerWeek = mergeRequests / Math.Max(1, days / 7);

        // Weighted scoring algorithm
        var score = (commitsPerDay * 2) + (mrsPerWeek * 3) + (pipelineSuccessRate * 5);

        return Math.Min(10, Math.Max(0, score));
    }

    private static List<long> ParseReviewerIds(string reviewerIds)
    {
        try
        {
            // Simple parsing for array of numbers in JSON format
            var trimmed = reviewerIds.Trim('[', ']', ' ');
            if (string.IsNullOrEmpty(trimmed))
                return new List<long>();

            var parts = trimmed.Split(',', StringSplitOptions.RemoveEmptyEntries);
            var ids = new List<long>();

            foreach (var part in parts)
            {
                if (long.TryParse(part.Trim(), out var id))
                {
                    ids.Add(id);
                }
            }

            return ids;
        }
        catch
        {
            return new List<long>();
        }
    }

    private static double CalculateKnowledgeSharingScore(int uniqueReviewers, int uniqueReviewees, int totalReviews)
    {
        // Score based on diversity of collaboration and review activity
        var diversityScore = (uniqueReviewers + uniqueReviewees) / 2.0;
        var activityScore = Math.Min(10, totalReviews / 5.0);

        return Math.Min(10, (diversityScore + activityScore) / 2);
    }

    private static double CalculateVelocityScore(int commits, int mergeRequests, double days)
    {
        var commitsPerDay = commits / Math.Max(1, days);
        var mrsPerWeek = mergeRequests / Math.Max(1, days / 7);

        return Math.Min(10, (commitsPerDay * 2) + (mrsPerWeek * 3));
    }

    private static double CalculateEfficiencyScore(List<Models.Raw.RawPipeline> pipelines, List<Models.Raw.RawMergeRequest> mergeRequests)
    {
        var pipelineSuccessRate = pipelines.Count > 0 ? pipelines.Count(p => p.IsSuccessful) / (double)pipelines.Count : 0;

        var quickMRs = mergeRequests.Count(mr => mr.CycleTime?.TotalDays <= 2);
        var quickMRRate = mergeRequests.Count > 0 ? quickMRs / (double)mergeRequests.Count : 0;

        return Math.Min(10, (pipelineSuccessRate * 5) + (quickMRRate * 5));
    }

    private static double CalculateImpactScore(List<Models.Raw.RawCommit> commits, List<Models.Raw.RawMergeRequest> mergeRequests)
    {
        var totalLinesChanged = commits.Sum(c => c.Additions + c.Deletions);
        var averageCommitSize = commits.Count > 0 ? totalLinesChanged / (double)commits.Count : 0;
        var averageMRSize = mergeRequests.Count > 0 ? mergeRequests.Average(mr => mr.ChangesCount) : 0;

        // Score based on reasonable change sizes (not too small, not too large)
        var commitSizeScore = CalculateOptimalSizeScore(averageCommitSize, 50, 200);
        var mrSizeScore = CalculateOptimalSizeScore(averageMRSize, 100, 500);

        return Math.Min(10, (commitSizeScore + mrSizeScore) / 2);
    }

    private static double CalculateOptimalSizeScore(double size, double optimalMin, double optimalMax)
    {
        if (size >= optimalMin && size <= optimalMax)
            return 10;

        if (size < optimalMin)
            return Math.Max(0, 10 * (size / optimalMin));

        return Math.Max(0, 10 * (optimalMax / size));
    }

    private static int EstimateFocusTime(List<Models.Raw.RawCommit> commits, double days)
    {
        // Estimate focus time based on commit patterns
        // Commits close together in time suggest focused work sessions
        var commitDays = commits.GroupBy(c => c.CommittedAt.Date).Count();
        var avgCommitsPerActiveDay = commitDays > 0 ? commits.Count / (double)commitDays : 0;

        // Estimate 2-4 hours of focus time per day with commits
        var estimatedHoursPerDay = Math.Min(8, Math.Max(1, avgCommitsPerActiveDay * 0.5));

        return (int)(commitDays * estimatedHoursPerDay);
    }

    #endregion

    #endregion
}
