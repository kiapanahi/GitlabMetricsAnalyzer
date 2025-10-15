using Microsoft.Extensions.Options;

using Toman.Management.KPIAnalysis.ApiService.Configuration;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

/// <summary>
/// Service implementation for calculating team-level aggregation metrics
/// </summary>
public sealed class TeamMetricsService(
    IGitLabHttpClient gitLabHttpClient,
    IOptions<MetricsConfiguration> metricsConfig,
    ILogger<TeamMetricsService> logger) : ITeamMetricsService
{
    private readonly IGitLabHttpClient _gitLabHttpClient = gitLabHttpClient;
    private readonly MetricsConfiguration _metricsConfig = metricsConfig.Value;
    private readonly ILogger<TeamMetricsService> _logger = logger;

    public async Task<TeamMetricsResult> CalculateTeamMetricsAsync(
        string teamId,
        int windowDays = 30,
        CancellationToken cancellationToken = default)
    {
        var windowEnd = DateTime.UtcNow;
        var windowStart = windowEnd.AddDays(-windowDays);

        _logger.LogInformation("Calculating team metrics for team {TeamId} from {WindowStart} to {WindowEnd}",
            teamId, windowStart, windowEnd);

        // Get team definition from configuration
        var team = _metricsConfig.TeamMapping?.Teams.FirstOrDefault(t => t.Id == teamId);
        if (team is null)
        {
            throw new InvalidOperationException($"Team '{teamId}' not found in configuration");
        }

        if (team.Members.Count == 0)
        {
            _logger.LogWarning("Team {TeamId} has no members configured", teamId);
            return CreateEmptyResult(team, windowDays, windowStart, windowEnd);
        }

        // Get all projects
        var allProjects = await _gitLabHttpClient.GetProjectsAsync(cancellationToken);
        var projectDict = allProjects.ToDictionary(p => (long)p.Id, p => p);

        // Aggregate data across all team members
        var allMergedMrs = new List<GitLabMergeRequest>();
        var projectContributions = new Dictionary<long, HashSet<long>>(); // projectId -> set of userIds
        var mrReviewerCounts = new Dictionary<long, int>(); // mrId -> reviewer count
        var cycleTimes = new List<decimal>();

        foreach (var userId in team.Members)
        {
            try
            {
                var userProjects = await _gitLabHttpClient.GetUserContributedProjectsAsync(userId, cancellationToken);

                foreach (var userProject in userProjects)
                {
                    var projectId = userProject.Id;
                    
                    // Track project contributions
                    if (!projectContributions.ContainsKey(projectId))
                    {
                        projectContributions[projectId] = [];
                    }
                    projectContributions[projectId].Add(userId);

                    // Get MRs for this project
                    var projectMrs = await _gitLabHttpClient.GetMergeRequestsAsync(
                        projectId,
                        new DateTimeOffset(windowStart, TimeSpan.Zero),
                        cancellationToken);

                    // Filter for merged MRs by this user in the window
                    var userMergedMrs = projectMrs
                        .Where(mr => mr.Author?.Id == userId &&
                                   mr.State == "merged" &&
                                   mr.MergedAt.HasValue &&
                                   mr.MergedAt.Value >= windowStart &&
                                   mr.MergedAt.Value <= windowEnd)
                        .ToList();

                    allMergedMrs.AddRange(userMergedMrs);

                    // Calculate cycle times and review coverage for these MRs
                    foreach (var mr in userMergedMrs)
                    {
                        if (mr.CreatedAt.HasValue && mr.MergedAt.HasValue)
                        {
                            var cycleTime = (decimal)(mr.MergedAt.Value - mr.CreatedAt.Value).TotalHours;
                            if (cycleTime >= 0)
                            {
                                cycleTimes.Add(cycleTime);
                            }
                        }

                        // Get approvals to count reviewers
                        var approvals = await _gitLabHttpClient.GetMergeRequestApprovalsAsync(
                            (int)projectId,
                            (int)mr.Iid,
                            cancellationToken);

                        if (approvals is not null)
                        {
                            var reviewerCount = approvals.ApprovedBy?.Count ?? 0;
                            mrReviewerCounts[mr.Id] = reviewerCount;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch data for user {UserId} in team {TeamId}", userId, teamId);
            }
        }

        // Calculate metrics
        var totalMergedMrs = allMergedMrs.Count;
        
        // Calculate total commits by fetching commits for each project in the window
        var totalCommits = 0;
        var projectCommitCounts = new Dictionary<long, int>();
        
        foreach (var projectId in projectContributions.Keys)
        {
            var projectCommits = await _gitLabHttpClient.GetCommitsAsync(
                projectId,
                new DateTimeOffset(windowStart, TimeSpan.Zero),
                cancellationToken);
            
            // Count commits in the window - all commits in team projects count towards team metrics
            var commitsInWindow = projectCommits.Count(c => 
                c.CommittedDate.HasValue &&
                c.CommittedDate.Value >= windowStart &&
                c.CommittedDate.Value <= windowEnd);
            
            projectCommitCounts[projectId] = commitsInWindow;
            totalCommits += commitsInWindow;
        }
        
        // Calculate total lines changed by fetching MR changes for each merged MR
        var totalLinesChanged = 0;
        foreach (var mr in allMergedMrs)
        {
            var changes = await _gitLabHttpClient.GetMergeRequestChangesAsync(
                mr.ProjectId,
                mr.Iid,
                cancellationToken);
            
            if (changes is not null)
            {
                totalLinesChanged += changes.Total;
            }
        }

        var avgMrCycleTimeP50H = cycleTimes.Any()
            ? ComputeMedian(cycleTimes)
            : (decimal?)null;

        var crossProjectContributors = team.Members.Count(userId =>
            projectContributions.Values.Count(userSet => userSet.Contains(userId)) > 1);

        var totalProjectsTouched = projectContributions.Count;

        // Calculate review coverage (default minimum: 1 reviewer)
        const int minReviewersRequired = 1;
        var mrsWithSufficientReviewers = mrReviewerCounts.Count(kvp => kvp.Value >= minReviewersRequired);
        var teamReviewCoveragePercentage = totalMergedMrs > 0
            ? (decimal)mrsWithSufficientReviewers / totalMergedMrs * 100
            : (decimal?)null;

        // Build project activity scores
        var projectActivities = projectContributions
            .Select(kvp =>
            {
                var projectId = kvp.Key;
                var contributors = kvp.Value;
                var projectMrs = allMergedMrs.Where(mr => mr.ProjectId == projectId).ToList();

                return new ProjectActivityScore
                {
                    ProjectId = projectId,
                    ProjectName = projectDict.TryGetValue(projectId, out var p) ? p.PathWithNamespace ?? $"Project-{projectId}" : $"Project-{projectId}",
                    CommitCount = projectCommitCounts.GetValueOrDefault(projectId, 0),
                    MergedMrCount = projectMrs.Count,
                    ContributorCount = contributors.Count
                };
            })
            .OrderByDescending(p => p.MergedMrCount)
            .ToList();

        _logger.LogInformation(
            "Team metrics calculated for {TeamId}: {MergedMrs} merged MRs, {Commits} commits, {LinesChanged} lines changed, {Projects} projects, {CrossProjectContributors} cross-project contributors",
            teamId, totalMergedMrs, totalCommits, totalLinesChanged, totalProjectsTouched, crossProjectContributors);

        return new TeamMetricsResult
        {
            TeamId = team.Id,
            TeamName = team.Name,
            MemberCount = team.Members.Count,
            WindowDays = windowDays,
            WindowStart = windowStart,
            WindowEnd = windowEnd,
            TotalMergedMrs = totalMergedMrs,
            TotalCommits = totalCommits,
            TotalLinesChanged = totalLinesChanged,
            AvgMrCycleTimeP50H = avgMrCycleTimeP50H,
            CrossProjectContributors = crossProjectContributors,
            TotalProjectsTouched = totalProjectsTouched,
            TeamReviewCoveragePercentage = teamReviewCoveragePercentage,
            MinReviewersRequired = minReviewersRequired,
            MrsWithSufficientReviewers = mrsWithSufficientReviewers,
            ProjectActivities = projectActivities
        };
    }

    private static TeamMetricsResult CreateEmptyResult(TeamDefinition team, int windowDays, DateTime windowStart, DateTime windowEnd)
    {
        return new TeamMetricsResult
        {
            TeamId = team.Id,
            TeamName = team.Name,
            MemberCount = team.Members.Count,
            WindowDays = windowDays,
            WindowStart = windowStart,
            WindowEnd = windowEnd,
            TotalMergedMrs = 0,
            TotalCommits = 0,
            TotalLinesChanged = 0,
            AvgMrCycleTimeP50H = null,
            CrossProjectContributors = 0,
            TotalProjectsTouched = 0,
            TeamReviewCoveragePercentage = null,
            MinReviewersRequired = 1,
            MrsWithSufficientReviewers = 0,
            ProjectActivities = []
        };
    }

    private static decimal ComputeMedian(List<decimal> values)
    {
        var sorted = values.OrderBy(x => x).ToList();
        var count = sorted.Count;

        if (count == 0)
            return 0;

        if (count % 2 == 1)
            return sorted[count / 2];

        return (sorted[count / 2 - 1] + sorted[count / 2]) / 2;
    }
}
