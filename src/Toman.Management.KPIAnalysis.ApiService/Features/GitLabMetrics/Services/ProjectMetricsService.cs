using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

/// <summary>
/// Service implementation for calculating project-level aggregation metrics
/// </summary>
public sealed class ProjectMetricsService(
    IGitLabHttpClient gitLabHttpClient,
    ILogger<ProjectMetricsService> logger) : IProjectMetricsService
{
    private readonly IGitLabHttpClient _gitLabHttpClient = gitLabHttpClient;
    private readonly ILogger<ProjectMetricsService> _logger = logger;
    private const int LongLivedBranchThresholdDays = 30;

    public async Task<ProjectMetricsResult> CalculateProjectMetricsAsync(
        long projectId,
        int windowDays = 30,
        CancellationToken cancellationToken = default)
    {
        var windowEnd = DateTime.UtcNow;
        var windowStart = windowEnd.AddDays(-windowDays);

        _logger.LogInformation("Calculating project metrics for project {ProjectId} from {WindowStart} to {WindowEnd}",
            projectId, windowStart, windowEnd);

        // Get project info
        var project = await _gitLabHttpClient.GetProjectByIdAsync(projectId, cancellationToken);
        if (project is null)
        {
            throw new InvalidOperationException($"Project {projectId} not found");
        }

        // Get commits in the window
        var commits = await _gitLabHttpClient.GetCommitsAsync(projectId, new DateTimeOffset(windowStart, TimeSpan.Zero), cancellationToken);
        var commitsInWindow = commits
            .Where(c => c.CommittedDate.HasValue &&
                       c.CommittedDate.Value >= windowStart &&
                       c.CommittedDate.Value <= windowEnd)
            .ToList();

        // Get MRs in the window
        var allMrs = await _gitLabHttpClient.GetMergeRequestsAsync(projectId, new DateTimeOffset(windowStart, TimeSpan.Zero), cancellationToken);
        var mergedMrs = allMrs
            .Where(mr => mr.State == "merged" &&
                       mr.MergedAt.HasValue &&
                       mr.MergedAt.Value >= windowStart &&
                       mr.MergedAt.Value <= windowEnd)
            .ToList();

        // Calculate unique contributors
        var commitAuthors = commitsInWindow
            .Where(c => c.AuthorEmail is not null)
            .Select(c => c.AuthorEmail!)
            .Distinct()
            .ToHashSet();
        var mrAuthors = mergedMrs
            .Select(mr => mr.Author?.Id ?? 0)
            .Where(id => id != 0)
            .Distinct()
            .ToHashSet();
        var uniqueContributors = commitAuthors.Count + mrAuthors.Count; // Simplified, may have overlap

        // Calculate cross-project contributors (contributors who also work on other projects)
        var crossProjectContributors = 0;
        foreach (var authorId in mrAuthors)
        {
            var userProjects = await _gitLabHttpClient.GetUserContributedProjectsAsync(authorId, cancellationToken);
            if (userProjects.Count > 1)
            {
                crossProjectContributors++;
            }
        }

        // Get branches and calculate long-lived branches
        var branches = await _gitLabHttpClient.GetBranchesAsync((int)projectId, cancellationToken);
        var longLivedBranches = branches
            .Where(b => !b.Merged &&
                       (windowEnd - b.Commit.CommittedDate.DateTime).TotalDays > LongLivedBranchThresholdDays)
            .Select(b => new LongLivedBranchInfo
            {
                Name = b.Name,
                AgeDays = (int)(windowEnd - b.Commit.CommittedDate.DateTime).TotalDays,
                LastCommitDate = b.Commit.CommittedDate.DateTime,
                IsMerged = b.Merged
            })
            .OrderByDescending(b => b.AgeDays)
            .ToList();

        var avgLongLivedBranchAgeDays = longLivedBranches.Any()
            ? (decimal?)longLivedBranches.Average(b => b.AgeDays)
            : null;

        // Calculate label usage distribution
        var labelUsage = new Dictionary<string, int>();
        foreach (var mr in mergedMrs)
        {
            if (mr.Labels is not null)
            {
                foreach (var label in mr.Labels)
                {
                    if (!labelUsage.ContainsKey(label))
                    {
                        labelUsage[label] = 0;
                    }
                    labelUsage[label]++;
                }
            }
        }

        // Get milestones and calculate completion rate
        var milestones = await _gitLabHttpClient.GetMilestonesAsync((int)projectId, cancellationToken);
        var completedMilestones = milestones.Where(m => m.State == "closed").ToList();
        var onTimeMilestones = completedMilestones
            .Where(m => m.DueDate.HasValue &&
                       m.UpdatedAt.DateTime <= m.DueDate.Value.ToDateTime(TimeOnly.MinValue))
            .ToList();

        var milestoneCompletionRate = completedMilestones.Any()
            ? (decimal?)((decimal)onTimeMilestones.Count / completedMilestones.Count * 100)
            : null;

        // Calculate review coverage
        const int minReviewersRequired = 1;
        var mrsWithSufficientReviewers = 0;

        foreach (var mr in mergedMrs)
        {
            var approvals = await _gitLabHttpClient.GetMergeRequestApprovalsAsync((int)projectId, (int)mr.Iid, cancellationToken);
            if (approvals is not null)
            {
                var reviewerCount = approvals.ApprovedBy?.Count ?? 0;
                if (reviewerCount >= minReviewersRequired)
                {
                    mrsWithSufficientReviewers++;
                }
            }
        }

        var reviewCoveragePercentage = mergedMrs.Any()
            ? (decimal?)((decimal)mrsWithSufficientReviewers / mergedMrs.Count * 100)
            : null;

        // Calculate total lines changed (would need to fetch MR changes for each MR)
        var totalLinesChanged = 0; // Simplified for now

        _logger.LogInformation(
            "Project metrics calculated for {ProjectId}: {Commits} commits, {MergedMrs} merged MRs, {LongLivedBranches} long-lived branches",
            projectId, commitsInWindow.Count, mergedMrs.Count, longLivedBranches.Count);

        return new ProjectMetricsResult
        {
            ProjectId = projectId,
            ProjectName = project.PathWithNamespace ?? $"Project-{projectId}",
            WindowDays = windowDays,
            WindowStart = windowStart,
            WindowEnd = windowEnd,
            TotalCommits = commitsInWindow.Count,
            TotalMergedMrs = mergedMrs.Count,
            TotalLinesChanged = totalLinesChanged,
            UniqueContributors = uniqueContributors,
            CrossProjectContributors = crossProjectContributors,
            LongLivedBranchCount = longLivedBranches.Count,
            AvgLongLivedBranchAgeDays = avgLongLivedBranchAgeDays,
            LongLivedBranches = longLivedBranches,
            LabelUsageDistribution = labelUsage,
            MilestoneCompletionRate = milestoneCompletionRate,
            CompletedMilestones = completedMilestones.Count,
            OnTimeMilestones = onTimeMilestones.Count,
            TotalMilestones = milestones.Count,
            ReviewCoveragePercentage = reviewCoveragePercentage,
            MinReviewersRequired = minReviewersRequired,
            MrsWithSufficientReviewers = mrsWithSufficientReviewers
        };
    }
}
