using System.Text.RegularExpressions;

using Microsoft.Extensions.Options;

using KuriousLabs.Management.KPIAnalysis.ApiService.Configuration;
using KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure;
using KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;

namespace KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

/// <summary>
/// Service implementation for calculating collaboration and review metrics from live GitLab data
/// </summary>
public sealed class CollaborationMetricsService : ICollaborationMetricsService
{
    private readonly IGitLabHttpClient _gitLabHttpClient;
    private readonly ILogger<CollaborationMetricsService> _logger;
    private readonly MetricsConfiguration _configuration;

    public CollaborationMetricsService(
        IGitLabHttpClient gitLabHttpClient,
        ILogger<CollaborationMetricsService> logger,
        IOptions<MetricsConfiguration> configuration)
    {
        _gitLabHttpClient = gitLabHttpClient;
        _logger = logger;
        _configuration = configuration.Value;
    }

    public async Task<CollaborationMetricsResult> CalculateCollaborationMetricsAsync(
        long userId,
        int windowDays = 30,
        CancellationToken cancellationToken = default)
    {
        if (windowDays <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(windowDays), windowDays, "Window days must be greater than 0");
        }

        _logger.LogInformation("Calculating collaboration metrics for user {UserId} over {WindowDays} days", userId, windowDays);

        // Get user details
        var user = await _gitLabHttpClient.GetUserByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            throw new InvalidOperationException($"User with ID {userId} not found");
        }

        var windowEnd = DateTime.UtcNow;
        var windowStart = windowEnd.AddDays(-windowDays);

        _logger.LogDebug("Fetching data for user {UserId} from {WindowStart} to {WindowEnd}", 
            userId, windowStart, windowEnd);

        // Get projects the user has contributed to
        var contributedProjects = await _gitLabHttpClient.GetUserContributedProjectsAsync(userId, cancellationToken);

        if (!contributedProjects.Any())
        {
            _logger.LogWarning("No contributed projects found for user {UserId}", userId);
            return CreateEmptyResult(user, windowDays, windowStart, windowEnd);
        }

        _logger.LogInformation("Found {ProjectCount} contributed projects for user {UserId}", 
            contributedProjects.Count, userId);

        // Fetch MRs and notes from all contributed projects in parallel
        var fetchDataTasks = contributedProjects.Select(async project =>
        {
            try
            {
                var mergeRequests = await _gitLabHttpClient.GetMergeRequestsAsync(
                    project.Id,
                    new DateTimeOffset(windowStart),
                    cancellationToken);

                // Filter MRs within time window
                var mrsInWindow = mergeRequests
                    .Where(mr => (mr.CreatedAt.HasValue && mr.CreatedAt.Value >= windowStart && mr.CreatedAt.Value <= windowEnd) ||
                                 (mr.UpdatedAt.HasValue && mr.UpdatedAt.Value >= windowStart && mr.UpdatedAt.Value <= windowEnd))
                    .ToList();

                return new ProjectData
                {
                    ProjectId = project.Id,
                    ProjectName = project.Name ?? "Unknown",
                    MergeRequests = mrsInWindow
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch data for project {ProjectId}", project.Id);
                return new ProjectData
                {
                    ProjectId = project.Id,
                    ProjectName = project.Name ?? "Unknown",
                    MergeRequests = []
                };
            }
        });

        var projectDataList = await Task.WhenAll(fetchDataTasks);
        var allMergeRequests = projectDataList.SelectMany(pd => pd.MergeRequests).ToList();

        if (!allMergeRequests.Any())
        {
            _logger.LogInformation("No merge requests found for user {UserId} in the time window", userId);
            return CreateEmptyResult(user, windowDays, windowStart, windowEnd);
        }

        // Fetch notes, discussions, and approvals for all MRs in parallel
        var enrichmentTasks = allMergeRequests.Select(async mr =>
        {
            try
            {
                var notesTask = _gitLabHttpClient.GetMergeRequestNotesAsync(mr.ProjectId, mr.Iid, cancellationToken);
                var discussionsTask = _gitLabHttpClient.GetMergeRequestDiscussionsAsync(mr.ProjectId, mr.Iid, cancellationToken);
                var approvalsTask = _gitLabHttpClient.GetMergeRequestApprovalsAsync(mr.ProjectId, mr.Iid, cancellationToken);

                await Task.WhenAll(notesTask, discussionsTask, approvalsTask);

                return new EnrichedMergeRequest
                {
                    MergeRequest = mr,
                    Notes = await notesTask,
                    Discussions = await discussionsTask,
                    Approvals = await approvalsTask
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to enrich MR {MrIid} in project {ProjectId}", mr.Iid, mr.ProjectId);
                return new EnrichedMergeRequest
                {
                    MergeRequest = mr,
                    Notes = Array.Empty<GitLabMergeRequestNote>(),
                    Discussions = Array.Empty<GitLabDiscussion>(),
                    Approvals = null
                };
            }
        });

        var enrichedMrs = await Task.WhenAll(enrichmentTasks);

        // Calculate all metrics
        var metrics = CalculateMetrics(userId, user, enrichedMrs, projectDataList, windowDays, windowStart, windowEnd);

        _logger.LogInformation("Successfully calculated collaboration metrics for user {UserId}", userId);
        return metrics;
    }

    private CollaborationMetricsResult CalculateMetrics(
        long userId,
        GitLabUser user,
        EnrichedMergeRequest[] enrichedMrs,
        ProjectData[] projectDataList,
        int windowDays,
        DateTime windowStart,
        DateTime windowEnd)
    {
        // Separate MRs by author
        var userMrs = enrichedMrs.Where(emr => emr.MergeRequest.Author?.Id == userId).ToList();
        var otherMrs = enrichedMrs.Where(emr => emr.MergeRequest.Author?.Id != userId).ToList();

        // Metric 1: Review Comments Given (as reviewer on others' MRs)
        var reviewCommentsGiven = 0;
        var mrsReviewedSet = new HashSet<(long ProjectId, long MrIid)>();
        var reviewTurnaroundTimes = new List<double>();

        foreach (var emr in otherMrs)
        {
            var userComments = emr.Notes
                .Where(n => !n.System && n.Author?.Id == userId)
                .Where(n => !IsBot(n.Author))
                .ToList();

            if (userComments.Any())
            {
                reviewCommentsGiven += userComments.Count;
                mrsReviewedSet.Add((emr.MergeRequest.ProjectId, emr.MergeRequest.Iid));

                // Calculate review turnaround time (time from MR creation to first comment)
                var firstComment = userComments.OrderBy(c => c.CreatedAt).FirstOrDefault();
                if (firstComment?.CreatedAt.HasValue == true && emr.MergeRequest.CreatedAt.HasValue)
                {
                    var turnaroundTime = (firstComment.CreatedAt.Value - emr.MergeRequest.CreatedAt.Value).TotalHours;
                    if (turnaroundTime > 0)
                    {
                        reviewTurnaroundTimes.Add(turnaroundTime);
                    }
                }
            }
        }

        var mrsReviewed = mrsReviewedSet.Count;

        // Metric 2: Review Comments Received (on developer's MRs)
        var reviewCommentsReceived = 0;
        foreach (var emr in userMrs)
        {
            var othersComments = emr.Notes
                .Where(n => !n.System && n.Author?.Id != userId)
                .Where(n => !IsBot(n.Author))
                .Count();
            reviewCommentsReceived += othersComments;
        }

        // Metric 3: Approvals Given
        var approvalsGiven = 0;
        foreach (var emr in otherMrs)
        {
            if (emr.Approvals?.ApprovedBy is not null)
            {
                var userApproval = emr.Approvals.ApprovedBy
                    .FirstOrDefault(a => a.User?.Id == userId);
                if (userApproval is not null)
                {
                    approvalsGiven++;
                }
            }
        }

        // Metric 4: Discussion Threads (resolved/unresolved)
        var resolvedThreads = 0;
        var unresolvedThreads = 0;

        foreach (var emr in userMrs)
        {
            foreach (var discussion in emr.Discussions)
            {
                if (discussion.IndividualNote)
                {
                    continue; // Skip individual notes, only count threaded discussions
                }

                var notes = discussion.Notes ?? new List<GitLabMergeRequestNote>();
                if (!notes.Any())
                {
                    continue;
                }

                // Check if all notes in the discussion are resolved
                var allResolved = notes.All(n => !n.Resolvable || n.Resolved);
                if (allResolved && notes.Any(n => n.Resolvable))
                {
                    resolvedThreads++;
                }
                else if (notes.Any(n => n.Resolvable && !n.Resolved))
                {
                    unresolvedThreads++;
                }
            }
        }

        // Metric 5: Self-Merged MRs
        var selfMergedMrs = 0;
        var totalMrsMerged = userMrs.Count(emr => emr.MergeRequest.State == "merged");

        foreach (var emr in userMrs)
        {
            if (emr.MergeRequest.State == "merged")
            {
                // Self-merged: author == merger && no approvals
                var hasApprovals = emr.Approvals?.ApprovedBy?.Any() == true;
                var hasExternalComments = emr.Notes
                    .Any(n => !n.System && n.Author?.Id != userId && !IsBot(n.Author));

                if (!hasApprovals && !hasExternalComments)
                {
                    selfMergedMrs++;
                }
            }
        }

        var selfMergedRatio = totalMrsMerged > 0 ? (decimal)selfMergedMrs / totalMrsMerged : (decimal?)null;

        // Metric 6: Review Turnaround Time (median)
        var reviewTurnaroundMedian = ComputeMedian(reviewTurnaroundTimes);

        // Metric 7: Review Depth Score (average comment length)
        var allReviewComments = otherMrs
            .SelectMany(emr => emr.Notes
                .Where(n => !n.System && n.Author?.Id == userId && !IsBot(n.Author)))
            .ToList();

        var reviewDepthScore = allReviewComments.Any()
            ? (decimal)allReviewComments.Average(c => c.Body?.Length ?? 0)
            : (decimal?)null;

        // Build project summaries
        var projectSummaries = BuildProjectSummaries(userId, projectDataList, enrichedMrs);

        // Build perspectives
        var perspectives = new CollaborationPerspectives
        {
            AsAuthor = new AsAuthorPerspective
            {
                MrsCreated = userMrs.Count,
                CommentsReceived = reviewCommentsReceived,
                SelfMergedMrs = selfMergedMrs,
                DiscussionThreads = resolvedThreads + unresolvedThreads,
                ResolvedThreads = resolvedThreads
            },
            AsReviewer = new AsReviewerPerspective
            {
                MrsReviewed = mrsReviewed,
                CommentsGiven = reviewCommentsGiven,
                ApprovalsGiven = approvalsGiven,
                ReviewTurnaroundMedianH = reviewTurnaroundMedian,
                AvgReviewDepthChars = reviewDepthScore
            }
        };

        return new CollaborationMetricsResult
        {
            UserId = userId,
            Username = user.Username ?? "Unknown",
            WindowDays = windowDays,
            WindowStart = windowStart,
            WindowEnd = windowEnd,
            ReviewCommentsGiven = reviewCommentsGiven,
            ReviewCommentsReceived = reviewCommentsReceived,
            ApprovalsGiven = approvalsGiven,
            ResolvedDiscussionThreads = resolvedThreads,
            UnresolvedDiscussionThreads = unresolvedThreads,
            SelfMergedMrsCount = selfMergedMrs,
            SelfMergedMrsRatio = selfMergedRatio,
            ReviewTurnaroundTimeMedianH = reviewTurnaroundMedian,
            ReviewDepthScoreAvgChars = reviewDepthScore,
            TotalMrsCreated = userMrs.Count,
            TotalMrsMerged = totalMrsMerged,
            MrsReviewed = mrsReviewed,
            Projects = projectSummaries,
            Perspectives = perspectives
        };
    }

    private List<ProjectCollaborationSummary> BuildProjectSummaries(
        long userId,
        ProjectData[] projectDataList,
        EnrichedMergeRequest[] enrichedMrs)
    {
        var summaries = new List<ProjectCollaborationSummary>();

        foreach (var project in projectDataList)
        {
            var projectMrs = enrichedMrs.Where(emr => emr.MergeRequest.ProjectId == project.ProjectId).ToList();
            
            var mrsCreated = projectMrs.Count(emr => emr.MergeRequest.Author?.Id == userId);
            var mrsReviewed = projectMrs
                .Where(emr => emr.MergeRequest.Author?.Id != userId)
                .Count(emr => emr.Notes.Any(n => !n.System && n.Author?.Id == userId));

            var commentsGiven = projectMrs
                .Where(emr => emr.MergeRequest.Author?.Id != userId)
                .SelectMany(emr => emr.Notes)
                .Count(n => !n.System && n.Author?.Id == userId && !IsBot(n.Author));

            var commentsReceived = projectMrs
                .Where(emr => emr.MergeRequest.Author?.Id == userId)
                .SelectMany(emr => emr.Notes)
                .Count(n => !n.System && n.Author?.Id != userId && !IsBot(n.Author));

            if (mrsCreated > 0 || mrsReviewed > 0 || commentsGiven > 0 || commentsReceived > 0)
            {
                summaries.Add(new ProjectCollaborationSummary
                {
                    ProjectId = project.ProjectId,
                    ProjectName = project.ProjectName,
                    MrsCreated = mrsCreated,
                    MrsReviewed = mrsReviewed,
                    CommentsGiven = commentsGiven,
                    CommentsReceived = commentsReceived
                });
            }
        }

        return summaries;
    }

    private bool IsBot(GitLabUser? user)
    {
        if (user is null)
        {
            return false;
        }

        if (_configuration.Identity?.BotRegexPatterns is null || !_configuration.Identity.BotRegexPatterns.Any())
        {
            // Default bot patterns if none configured
            var defaultPatterns = new[] { "bot$", "^bot-", "\\[bot\\]", "-ci$", "^ci-" };
            return defaultPatterns.Any(pattern => 
                Regex.IsMatch(user.Username ?? string.Empty, pattern, RegexOptions.IgnoreCase));
        }

        return _configuration.Identity.BotRegexPatterns.Any(pattern =>
            Regex.IsMatch(user.Username ?? string.Empty, pattern, RegexOptions.IgnoreCase));
    }

    private static decimal? ComputeMedian(List<double> values)
    {
        if (!values.Any())
        {
            return null;
        }

        var sorted = values.OrderBy(v => v).ToList();
        var count = sorted.Count;

        if (count % 2 == 0)
        {
            return (decimal)((sorted[count / 2 - 1] + sorted[count / 2]) / 2.0);
        }

        return (decimal)sorted[count / 2];
    }

    private static CollaborationMetricsResult CreateEmptyResult(
        GitLabUser user,
        int windowDays,
        DateTime windowStart,
        DateTime windowEnd)
    {
        return new CollaborationMetricsResult
        {
            UserId = user.Id,
            Username = user.Username ?? "Unknown",
            WindowDays = windowDays,
            WindowStart = windowStart,
            WindowEnd = windowEnd,
            ReviewCommentsGiven = 0,
            ReviewCommentsReceived = 0,
            ApprovalsGiven = 0,
            ResolvedDiscussionThreads = 0,
            UnresolvedDiscussionThreads = 0,
            SelfMergedMrsCount = 0,
            SelfMergedMrsRatio = null,
            ReviewTurnaroundTimeMedianH = null,
            ReviewDepthScoreAvgChars = null,
            TotalMrsCreated = 0,
            TotalMrsMerged = 0,
            MrsReviewed = 0,
            Projects = [],
            Perspectives = new CollaborationPerspectives
            {
                AsAuthor = new AsAuthorPerspective
                {
                    MrsCreated = 0,
                    CommentsReceived = 0,
                    SelfMergedMrs = 0,
                    DiscussionThreads = 0,
                    ResolvedThreads = 0
                },
                AsReviewer = new AsReviewerPerspective
                {
                    MrsReviewed = 0,
                    CommentsGiven = 0,
                    ApprovalsGiven = 0,
                    ReviewTurnaroundMedianH = null,
                    AvgReviewDepthChars = null
                }
            }
        };
    }

    private sealed class ProjectData
    {
        public required long ProjectId { get; init; }
        public required string ProjectName { get; init; }
        public required List<GitLabMergeRequest> MergeRequests { get; init; }
    }

    private sealed class EnrichedMergeRequest
    {
        public required GitLabMergeRequest MergeRequest { get; init; }
        public required IReadOnlyList<GitLabMergeRequestNote> Notes { get; init; }
        public required IReadOnlyList<GitLabDiscussion> Discussions { get; init; }
        public required GitLabMergeRequestApprovals? Approvals { get; init; }
    }
}
