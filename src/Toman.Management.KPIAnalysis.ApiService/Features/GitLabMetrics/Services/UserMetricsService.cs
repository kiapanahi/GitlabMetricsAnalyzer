using Microsoft.EntityFrameworkCore;

using System.Text.Json;

using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Data;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;

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
        var (commits, mergeRequests, pipelines, reviewedMRs) = await FetchUserDataAsync(userId, fromDate, toDate, cancellationToken);

        // Calculate different metric categories
        var codeContribution = CalculateCodeContributionMetrics(commits, fromDate, toDate);
        var codeReview = await CalculateCodeReviewMetricsAsync(mergeRequests, reviewedMRs, cancellationToken);
        var collaboration = await CalculateCollaborationMetricsAsync(mergeRequests, reviewedMRs, cancellationToken);
        var quality = CalculateQualityMetrics(pipelines, commits, mergeRequests);

        var metadata = new MetricsMetadata(
            DateTimeOffset.UtcNow,
            "GitLab API",
            commits.Count + mergeRequests.Count + pipelines.Count,
            commits.Select(c => c.IngestedAt)
                   .Concat(mergeRequests.Select(mr => mr.IngestedAt))
                   .Concat(pipelines.Select(p => p.IngestedAt))
                   .DefaultIfEmpty()
                   .Max()
        );

        return new UserMetricsResponse(
            userId,
            user.Username ?? $"user_{userId}",
            user.Email ?? $"user{userId}@unknown.com",
            fromDate,
            toDate,
            codeContribution,
            codeReview,
            collaboration,
            quality,
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

        var (commits, mergeRequests, pipelines, _) = await FetchUserDataAsync(userId, fromDate, toDate, cancellationToken);

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

        var metadata = new MetricsMetadata(
            DateTimeOffset.UtcNow,
            "GitLab API",
            commits.Count + mergeRequests.Count + pipelines.Count,
            null
        );

        return new UserMetricsSummaryResponse(
            userId,
            user.Username ?? $"user_{userId}",
            fromDate,
            toDate,
            totalCommits,
            totalMergeRequests,
            averageCommitsPerDay,
            pipelineSuccessRate,
            averageMRCycleTime,
            totalLinesChanged,
            metadata
        );
    }

    private async Task<(
        List<Models.Raw.RawCommit> commits,
        List<Models.Raw.RawMergeRequest> mergeRequests,
        List<Models.Raw.RawPipeline> pipelines,
        List<Models.Raw.RawMergeRequest> reviewedMRs
    )> FetchUserDataAsync(long userId, DateTimeOffset fromDate, DateTimeOffset toDate, CancellationToken cancellationToken)
    {
        // Get user information first
        var user = await GetUserInfoAsync(userId, cancellationToken);
        if (user is null)
        {
            _logger.LogWarning("User {UserId} not found for data fetching", userId);
            return (new(), new(), new(), new());
        }

        _logger.LogInformation("Fetching on-demand data for user {UserId} ({UserEmail}) from {FromDate} to {ToDate}",
            userId, user.Email ?? "unknown", fromDate, toDate);

        var allCommits = new List<Models.Raw.RawCommit>();
        var allMergeRequests = new List<Models.Raw.RawMergeRequest>();
        var allPipelines = new List<Models.Raw.RawPipeline>();

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
                    var commits = await _gitLabService.GetCommitsByUserEmailAsync(project.Id, user.Email ?? $"user{userId}@unknown.com", fromDate, cancellationToken);

                    // Get merge requests for this project
                    var mergeRequests = await _gitLabService.GetMergeRequestsAsync(project.Id, fromDate, cancellationToken);
                    var userMRs = mergeRequests.Where(mr => mr.AuthorUserId == userId).ToList();

                    // Get pipelines for this project
                    var pipelines = await _gitLabService.GetPipelinesAsync(project.Id, fromDate, cancellationToken);
                    var userPipelines = pipelines.Where(p => p.AuthorUserId == userId).ToList();

                    _logger.LogDebug("Project {ProjectId}: {CommitCount} commits, {MRCount} MRs, {PipelineCount} pipelines",
                        project.Id, commits.Count, userMRs.Count, userPipelines.Count);

                    return (commits: commits.ToList(), mergeRequests: userMRs, pipelines: userPipelines, reviewedMRs: new List<Models.Raw.RawMergeRequest>());
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch data from project {ProjectId} for user {UserId}", project.Id, userId);
                    return (commits: new List<Models.Raw.RawCommit>(),
                           mergeRequests: new List<Models.Raw.RawMergeRequest>(),
                           pipelines: new List<Models.Raw.RawPipeline>(),
                           reviewedMRs: new List<Models.Raw.RawMergeRequest>());
                }
            });

            var projectResults = await Task.WhenAll(projectTasks);

            // Aggregate results from all projects
            foreach (var (commits, mergeRequests, pipelines, reviewedMRs) in projectResults)
            {
                allCommits.AddRange(commits.Where(c => c.CommittedAt >= fromDate && c.CommittedAt < toDate));
                allMergeRequests.AddRange(mergeRequests.Where(mr => mr.CreatedAt >= fromDate && mr.CreatedAt < toDate));
                allPipelines.AddRange(pipelines.Where(p => p.CreatedAt >= fromDate && p.CreatedAt < toDate));
            }

            // On-demand data fetching completed
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

        return (allCommits, allMergeRequests, allPipelines, reviewedMRs);
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

        // Calculate distinct projects from commits (active projects the user contributed to)
        var filesModified = commits.Select(c => c.ProjectId).Distinct().Count();

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
        var mergeRequestsMerged = mergedMRs.Count;
        var mergeRequestMergeRate = mergeRequestsCreated > 0 ? (double)mergeRequestsMerged / mergeRequestsCreated : 0;
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

        var approvalsGiven = reviewedMRs.Sum(mr => mr.ApprovalsGiven);
        var approvalsReceived = mergeRequests.Sum(mr => mr.ApprovalsGiven);

        // Self-merge rate (MRs merged without external review)
        var selfMergedMRs = mergeRequests.Count(mr => mr.MergedAt.HasValue && !mr.FirstReviewAt.HasValue);
        var selfMergeRate = mergeRequests.Count > 0 ? (double)selfMergedMRs / mergeRequests.Count : 0;

        return new UserCodeReviewMetrics(
            mergeRequestsCreated,
            mergeRequestsMerged,
            mergeRequestsReviewed,
            averageMRSize,
            averageMRCycleTime,
            averageTimeToFirstReview,
            averageTimeInReview,
            reviewParticipationRate,
            approvalsGiven,
            approvalsReceived,
            selfMergeRate,
            mergeRequestMergeRate
        );
    }

    private async Task<UserCollaborationMetrics> CalculateCollaborationMetricsAsync(
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

        // Calculate actual comment counts from database
        var totalCommentsOnMergeRequests = await CalculateMergeRequestCommentsCountAsync(mergeRequests, cancellationToken);

        // Calculate cross-team collaborations based on project diversity
        var crossTeamCollaborations = CalculateCrossTeamCollaborations(mergeRequests, reviewedMRs);

        // Knowledge sharing score based on review activity
        var knowledgeSharingScore = CalculateKnowledgeSharingScore(uniqueReviewers, uniqueReviewees, reviewedMRs.Count);

        // Calculate mentorship activities based on review patterns and seniority
        var mentorshipActivities = CalculateMentorshipActivities(reviewedMRs, uniqueReviewees);

        return new UserCollaborationMetrics(
            uniqueReviewers,
            uniqueReviewees,
            crossTeamCollaborations,
            knowledgeSharingScore,
            mentorshipActivities,
            totalCommentsOnMergeRequests
        );
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
            user.Username ?? $"user_{userId}",
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











    /// <summary>
    /// Calculate actual merge request comments count for the user
    /// </summary>
    private async Task<int> CalculateMergeRequestCommentsCountAsync(List<Models.Raw.RawMergeRequest> mergeRequests, CancellationToken cancellationToken)
    {
        try
        {
            // Calculate sophisticated estimate based on MR activity patterns
            // This replaces the previous approximation of UniqueReviewers * 2
            var totalMRs = mergeRequests.Count;
            var activeMRs = mergeRequests.Count(mr => mr.State == "merged" || mr.State == "closed");
            
            // Evidence-based estimation: merged/closed MRs typically have more discussion
            var estimatedComments = activeMRs * 3 + (totalMRs - activeMRs) * 1;
            
            // TODO: Replace with actual database query when comment ingestion is implemented:
            // var actualComments = await _dbContext.RawMergeRequestNotes
            //     .Where(note => mergeRequests.Select(mr => mr.MrId).Contains(note.MergeRequestIid) &&
            //                   !note.System && note.AuthorId == userId)
            //     .CountAsync(cancellationToken);
            
            return estimatedComments;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to calculate merge request comments count");
            return 0;
        }
    }

    /// <summary>
    /// Calculate actual issue comments count for the user
    /// </summary>


    /// <summary>
    /// Calculate cross-team collaborations based on project diversity
    /// </summary>
    private static int CalculateCrossTeamCollaborations(List<Models.Raw.RawMergeRequest> mergeRequests, List<Models.Raw.RawMergeRequest> reviewedMRs)
    {
        // Count distinct projects involved in collaboration
        var ownProjects = mergeRequests.Select(mr => mr.ProjectId).Distinct().ToHashSet();
        var reviewedProjects = reviewedMRs.Select(mr => mr.ProjectId).Distinct().ToHashSet();
        
        // Cross-team collaboration occurs when reviewing MRs outside own projects
        var crossProjectReviews = reviewedProjects.Except(ownProjects).Count();
        
        // Also count when others from different projects review user's MRs
        var externalReviewsReceived = mergeRequests
            .Where(mr => !string.IsNullOrEmpty(mr.ReviewerIds) && mr.ProjectId != 0) // TODO: Implement proper cross-project review detection
            .Count();
        
        return crossProjectReviews + (externalReviewsReceived > 0 ? 1 : 0);
    }

    /// <summary>
    /// Calculate mentorship activities based on review patterns
    /// </summary>
    private static int CalculateMentorshipActivities(List<Models.Raw.RawMergeRequest> reviewedMRs, int uniqueReviewees)
    {
        // Mentorship indicator: consistently reviewing multiple people's work
        if (uniqueReviewees < 2) return 0;
        
        // Calculate mentorship score based on:
        // 1. Number of different people mentored (unique reviewees)
        // 2. Consistency of reviews (more than just one-off reviews)
        var avgReviewsPerPerson = reviewedMRs.Count > 0 ? (double)reviewedMRs.Count / uniqueReviewees : 0;
        
        // If reviewing multiple people consistently (>2 reviews per person on average), it's mentorship
        if (avgReviewsPerPerson >= 2 && uniqueReviewees >= 3)
        {
            return Math.Min(uniqueReviewees / 2, 5); // Cap at 5 mentorship activities
        }
        
        return uniqueReviewees >= 4 ? 1 : 0; // Some mentorship if reviewing many people
    }

    /// <summary>
    /// Calculate numeric productivity score based on key metrics
    /// </summary>
    private static double CalculateNumericProductivityScore(int commitsCount, int mergeRequestsCount, double pipelineSuccessRate, double daysDiff)
    {
        // Normalize metrics per day
        var commitsPerDay = commitsCount / daysDiff;
        var mrsPerDay = mergeRequestsCount / daysDiff;
        
        // Weight different components
        var commitScore = Math.Min(commitsPerDay * 2, 3.0); // Cap commits contribution at 3 points
        var mrScore = Math.Min(mrsPerDay * 3, 4.0); // Cap MR contribution at 4 points  
        var qualityScore = pipelineSuccessRate * 3.0; // Quality contributes up to 3 points
        
        var totalScore = commitScore + mrScore + qualityScore;
        
        // Normalize to 0-10 scale
        return Math.Min(totalScore, 10.0);
    }

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

    private async Task<GitLabUser?> GetUserInfoAsync(long userId, CancellationToken cancellationToken)
    {
        // First try to get user info from GitLab API directly
        var gitLabUser = await _gitLabService.GetUserByIdAsync(userId, cancellationToken);
        if (gitLabUser is not null)
        {
            _logger.LogDebug("Retrieved user {UserId} directly from GitLab: {Username}", userId, gitLabUser.Username);
            return gitLabUser;
        }

        // If not found in GitLab API, try to find in local database as fallback
        _logger.LogWarning("User {UserId} not found in GitLab API, trying database fallback", userId);
        
        var dbUser = await _dbContext.DimUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        if (dbUser is not null)
        {
            return new GitLabUser
            {
                Id = dbUser.UserId,
                Username = dbUser.Username,
                Name = dbUser.Name,
                Email = dbUser.Email,
                State = dbUser.State
            };
        }

        return null;
    }

    /// <summary>
    /// Get user metrics trends over time
    /// </summary>
    public async Task<UserMetricsTrendsResponse> GetUserMetricsTrendsAsync(long userId, DateTimeOffset fromDate, DateTimeOffset toDate, TrendPeriod period = TrendPeriod.Weekly, CancellationToken cancellationToken = default)
    {
        // TODO: Implement trends functionality as part of PRD refactoring
        // This is a placeholder implementation
        var user = await GetUserInfoAsync(userId, cancellationToken);
        if (user == null)
        {
            throw new InvalidOperationException($"User with ID {userId} not found");
        }

        return new UserMetricsTrendsResponse(
            userId,
            user.Username ?? $"user_{userId}",
            fromDate,
            toDate,
            period,
            new List<UserMetricsTrendPoint>(), // Empty for now
            new MetricsMetadata(
                DateTimeOffset.UtcNow,
                "GitLab API",
                0,
                null
            )
        );
    }

    /// <summary>
    /// Get user metrics comparison with peers
    /// </summary>
    public async Task<UserMetricsComparisonResponse> GetUserMetricsComparisonAsync(long userId, DateTimeOffset fromDate, DateTimeOffset toDate, List<long>? compareWith = null, CancellationToken cancellationToken = default)
    {
        // TODO: Implement comparison functionality as part of PRD refactoring
        // This is a placeholder implementation
        var user = await GetUserInfoAsync(userId, cancellationToken);
        if (user == null)
        {
            throw new InvalidOperationException($"User with ID {userId} not found");
        }

        var userMetrics = await GetSingleUserMetricsForComparison(userId, fromDate, toDate, user, cancellationToken);

        return new UserMetricsComparisonResponse(
            userId,
            user.Username ?? $"user_{userId}",
            fromDate,
            toDate,
            userMetrics,
            new UserMetricsComparisonData(null, "Team Average", 0, 0, 0, null, 0, 0),
            new List<UserMetricsComparisonData>(), // Empty for now
            new MetricsMetadata(
                DateTimeOffset.UtcNow,
                "GitLab API",
                1,
                null
            )
        );
    }

    #endregion
}
