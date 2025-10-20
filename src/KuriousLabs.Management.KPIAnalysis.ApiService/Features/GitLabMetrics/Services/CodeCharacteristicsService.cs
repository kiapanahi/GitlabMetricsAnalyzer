using System.Text.RegularExpressions;

using Microsoft.Extensions.Options;

using KuriousLabs.Management.KPIAnalysis.ApiService.Configuration;
using KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure;
using KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;

namespace KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

/// <summary>
/// Service implementation for calculating code characteristics metrics from live GitLab data
/// </summary>
public sealed class CodeCharacteristicsService : ICodeCharacteristicsService
{
    private readonly IGitLabHttpClient _gitLabHttpClient;
    private readonly ILogger<CodeCharacteristicsService> _logger;
    private readonly MetricsConfiguration _metricsConfig;

    public CodeCharacteristicsService(
        IGitLabHttpClient gitLabHttpClient,
        ILogger<CodeCharacteristicsService> logger,
        IOptions<MetricsConfiguration> metricsConfig)
    {
        _gitLabHttpClient = gitLabHttpClient;
        _logger = logger;
        _metricsConfig = metricsConfig.Value;
    }

    public async Task<CodeCharacteristicsResult> CalculateCodeCharacteristicsAsync(
        long userId,
        int windowDays = 30,
        CancellationToken cancellationToken = default)
    {
        if (windowDays <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(windowDays), windowDays, "Window days must be greater than 0");
        }

        _logger.LogInformation("Calculating code characteristics for user {UserId} over {WindowDays} days", userId, windowDays);

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

        // Fetch commits and MRs from all contributed projects in parallel
        var fetchDataTasks = contributedProjects.Select(async project =>
        {
            try
            {
                var commits = await _gitLabHttpClient.GetCommitsAsync(
                    project.Id,
                    new DateTimeOffset(windowStart),
                    cancellationToken);

                var mergeRequests = await _gitLabHttpClient.GetMergeRequestsAsync(
                    project.Id,
                    new DateTimeOffset(windowStart),
                    cancellationToken);

                // Filter commits by author email/name and within time window
                var userCommits = commits
                    .Where(c => c.AuthorEmail == user.Email || c.AuthorName == user.Name || c.AuthorName == user.Username)
                    .Where(c => c.CommittedDate.HasValue && c.CommittedDate.Value >= windowStart && c.CommittedDate.Value <= windowEnd)
                    .ToList();

                // Filter MRs by author and within time window (merged)
                var userMergeRequests = mergeRequests
                    .Where(mr => mr.Author?.Id == userId)
                    .Where(mr => mr.MergedAt.HasValue && mr.MergedAt.Value >= windowStart && mr.MergedAt.Value <= windowEnd)
                    .ToList();

                if (userCommits.Any() || userMergeRequests.Any())
                {
                    _logger.LogDebug("Found {CommitCount} commits and {MrCount} merged MRs for user {UserId} in project {ProjectId}",
                        userCommits.Count, userMergeRequests.Count, userId, project.Id);

                    return new ProjectData
                    {
                        ProjectId = project.Id,
                        ProjectName = project.Name ?? "Unknown",
                        Commits = userCommits,
                        MergeRequests = userMergeRequests
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch data for project {ProjectId}", project.Id);
                return null;
            }
        });

        var projectDataResults = await Task.WhenAll(fetchDataTasks);
        var projectData = projectDataResults.Where(pd => pd is not null).ToList();

        if (!projectData.Any())
        {
            _logger.LogWarning("No data found for user {UserId} in any project", userId);
            return CreateEmptyResult(user, windowDays, windowStart, windowEnd);
        }

        // Aggregate all commits and MRs
        var allCommits = projectData.SelectMany(pd => pd!.Commits).ToList();
        var allMergeRequests = projectData.SelectMany(pd => pd!.MergeRequests).ToList();

        _logger.LogInformation("Processing {CommitCount} commits and {MrCount} merged MRs for user {UserId}",
            allCommits.Count, allMergeRequests.Count, userId);

        // Calculate all metrics
        var commitFrequencyMetrics = CalculateCommitFrequencyMetrics(allCommits, windowDays);
        var commitSizeMetrics = CalculateCommitSizeMetrics(allCommits);
        var mrSizeDistribution = await CalculateMrSizeDistributionAsync(allMergeRequests, cancellationToken);
        var fileChurnMetrics = await CalculateFileChurnMetricsAsync(allCommits, cancellationToken);
        var squashMetrics = CalculateSquashMetrics(allMergeRequests);
        var commitMessageQualityMetrics = CalculateCommitMessageQualityMetrics(allCommits);
        var branchNamingMetrics = CalculateBranchNamingMetrics(allMergeRequests);

        // Build project summaries
        var projectSummaries = projectData.Select(pd => new ProjectCodeCharacteristicsSummary
        {
            ProjectId = pd!.ProjectId,
            ProjectName = pd.ProjectName,
            CommitCount = pd.Commits.Count,
            MergedMrCount = pd.MergeRequests.Count
        }).ToList();

        return new CodeCharacteristicsResult
        {
            CommitsPerDay = commitFrequencyMetrics.CommitsPerDay,
            CommitsPerWeek = commitFrequencyMetrics.CommitsPerWeek,
            TotalCommits = allCommits.Count,
            CommitDaysCount = commitFrequencyMetrics.DistinctDays,
            CommitSizeMedian = commitSizeMetrics.Median,
            CommitSizeP95 = commitSizeMetrics.P95,
            CommitSizeAverage = commitSizeMetrics.Average,
            MrSizeDistribution = mrSizeDistribution.Distribution,
            TotalMergedMrs = allMergeRequests.Count,
            TopFilesByChurn = fileChurnMetrics.TopFiles,
            SquashMergeRate = squashMetrics.Rate,
            SquashedMrsCount = squashMetrics.Count,
            AverageCommitMessageLength = commitMessageQualityMetrics.AverageLength,
            ConventionalCommitRate = commitMessageQualityMetrics.ConventionalRate,
            ConventionalCommitsCount = commitMessageQualityMetrics.ConventionalCount,
            BranchNamingComplianceRate = branchNamingMetrics.ComplianceRate,
            CompliantBranchesCount = branchNamingMetrics.CompliantCount,
            Projects = projectSummaries
        };
    }

    private (decimal CommitsPerDay, decimal CommitsPerWeek, int DistinctDays) CalculateCommitFrequencyMetrics(
        List<GitLabCommit> commits,
        int windowDays)
    {
        if (!commits.Any())
        {
            return (0, 0, 0);
        }

        var distinctDays = commits
            .Where(c => c.CommittedDate.HasValue)
            .Select(c => c.CommittedDate!.Value.Date)
            .Distinct()
            .Count();

        var commitsPerDay = windowDays > 0 ? (decimal)commits.Count / windowDays : 0;
        var commitsPerWeek = commitsPerDay * 7;

        return (commitsPerDay, commitsPerWeek, distinctDays);
    }

    private (decimal? Median, decimal? P95, decimal? Average) CalculateCommitSizeMetrics(
        List<GitLabCommit> commits)
    {
        var commitSizes = commits
            .Where(c => c.Stats is not null)
            .Select(c => c.Stats!.Additions + c.Stats.Deletions)
            .Where(size => size > 0)
            .OrderBy(size => size)
            .ToList();

        if (!commitSizes.Any())
        {
            return (null, null, null);
        }

        var medianIndex = commitSizes.Count / 2;
        var median = commitSizes.Count % 2 == 0
            ? (decimal)(commitSizes[medianIndex - 1] + commitSizes[medianIndex]) / 2
            : (decimal)commitSizes[medianIndex];

        var p95Index = (int)Math.Ceiling(commitSizes.Count * 0.95) - 1;
        var p95 = (decimal)commitSizes[Math.Min(p95Index, commitSizes.Count - 1)];

        var average = (decimal)commitSizes.Average();

        return (median, p95, average);
    }

    private async Task<(MrSizeDistribution Distribution, int Total)> CalculateMrSizeDistributionAsync(
        List<GitLabMergeRequest> mergeRequests,
        CancellationToken cancellationToken)
    {
        if (!mergeRequests.Any())
        {
            return (new MrSizeDistribution
            {
                SmallCount = 0,
                MediumCount = 0,
                LargeCount = 0,
                ExtraLargeCount = 0,
                SmallPercentage = 0,
                MediumPercentage = 0,
                LargePercentage = 0,
                ExtraLargePercentage = 0
            }, 0);
        }

        var config = _metricsConfig.CodeCharacteristics;
        var smallCount = 0;
        var mediumCount = 0;
        var largeCount = 0;
        var extraLargeCount = 0;

        foreach (var mr in mergeRequests)
        {
            try
            {
                // Get MR changes to calculate total lines changed
                var changes = await _gitLabHttpClient.GetMergeRequestChangesAsync(
                    mr.ProjectId,
                    mr.Iid,
                    cancellationToken);

                if (changes is null)
                {
                    continue;
                }

                var linesChanged = changes.Additions + changes.Deletions;

                if (linesChanged < config.SmallMrThreshold)
                {
                    smallCount++;
                }
                else if (linesChanged < config.MediumMrThreshold)
                {
                    mediumCount++;
                }
                else if (linesChanged < config.LargeMrThreshold)
                {
                    largeCount++;
                }
                else
                {
                    extraLargeCount++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get changes for MR {MrIid} in project {ProjectId}",
                    mr.Iid, mr.ProjectId);
            }
        }

        var total = smallCount + mediumCount + largeCount + extraLargeCount;

        if (total == 0)
        {
            return (new MrSizeDistribution
            {
                SmallCount = 0,
                MediumCount = 0,
                LargeCount = 0,
                ExtraLargeCount = 0,
                SmallPercentage = 0,
                MediumPercentage = 0,
                LargePercentage = 0,
                ExtraLargePercentage = 0
            }, 0);
        }

        return (new MrSizeDistribution
        {
            SmallCount = smallCount,
            MediumCount = mediumCount,
            LargeCount = largeCount,
            ExtraLargeCount = extraLargeCount,
            SmallPercentage = (decimal)smallCount / total * 100,
            MediumPercentage = (decimal)mediumCount / total * 100,
            LargePercentage = (decimal)largeCount / total * 100,
            ExtraLargePercentage = (decimal)extraLargeCount / total * 100
        }, total);
    }

    private Task<(List<FileChurnInfo> TopFiles, int TotalFiles)> CalculateFileChurnMetricsAsync(
        List<GitLabCommit> commits,
        CancellationToken cancellationToken)
    {
        // Note: GitLabCommit from our API doesn't include individual file diffs
        // We would need to call GetCommitDiffAsync for each commit, which is expensive
        // For now, we'll skip this detailed analysis to avoid performance issues
        // In a production implementation, consider caching or pre-aggregating this data
        // This would require significant API calls: one per commit, which could be hundreds of calls
        
        _logger.LogInformation("File churn analysis is not implemented in this version due to performance considerations. " +
            "Would require {CommitCount} additional API calls to fetch commit diffs.", commits.Count);
        
        return Task.FromResult((new List<FileChurnInfo>(), 0));
    }

    private (decimal Rate, int Count) CalculateSquashMetrics(List<GitLabMergeRequest> mergeRequests)
    {
        if (!mergeRequests.Any())
        {
            return (0, 0);
        }

        var squashedCount = mergeRequests.Count(mr => mr.Squash);
        var rate = (decimal)squashedCount / mergeRequests.Count;

        return (rate, squashedCount);
    }

    private (decimal AverageLength, decimal ConventionalRate, int ConventionalCount) CalculateCommitMessageQualityMetrics(
        List<GitLabCommit> commits)
    {
        if (!commits.Any())
        {
            return (0, 0, 0);
        }

        var config = _metricsConfig.CodeCharacteristics;
        
        // Calculate average message length
        var messageLengths = commits
            .Where(c => !string.IsNullOrWhiteSpace(c.Title))
            .Select(c => c.Title?.Length ?? 0)
            .ToList();

        var averageLength = messageLengths.Any() ? (decimal)messageLengths.Average() : 0;

        // Check for conventional commit format
        var conventionalCommitRegexes = config.ConventionalCommitPatterns
            .Select(pattern => new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled))
            .ToList();

        var excludedPatternRegexes = config.ExcludedCommitMessagePatterns
            .Select(pattern => new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled))
            .ToList();

        var conventionalCount = 0;

        foreach (var commit in commits)
        {
            var message = commit.Title ?? string.Empty;
            
            // Skip if message is in excluded patterns
            if (excludedPatternRegexes.Any(regex => regex.IsMatch(message)))
            {
                continue;
            }

            // Check minimum length
            if (message.Length < config.MinCommitMessageLength)
            {
                continue;
            }

            // Check if it matches conventional commit patterns
            if (conventionalCommitRegexes.Any(regex => regex.IsMatch(message)))
            {
                conventionalCount++;
            }
        }

        var conventionalRate = commits.Count > 0 ? (decimal)conventionalCount / commits.Count : 0;

        return (averageLength, conventionalRate, conventionalCount);
    }

    private (decimal ComplianceRate, int CompliantCount) CalculateBranchNamingMetrics(
        List<GitLabMergeRequest> mergeRequests)
    {
        if (!mergeRequests.Any())
        {
            return (0, 0);
        }

        var config = _metricsConfig.CodeCharacteristics;
        var branchPatternRegexes = config.BranchNamingPatterns
            .Select(pattern => new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled))
            .ToList();

        var compliantCount = 0;

        foreach (var mr in mergeRequests)
        {
            if (string.IsNullOrWhiteSpace(mr.SourceBranch))
            {
                continue;
            }

            if (branchPatternRegexes.Any(regex => regex.IsMatch(mr.SourceBranch)))
            {
                compliantCount++;
            }
        }

        var complianceRate = (decimal)compliantCount / mergeRequests.Count;

        return (complianceRate, compliantCount);
    }

    private CodeCharacteristicsResult CreateEmptyResult(
        GitLabUser user,
        int windowDays,
        DateTime windowStart,
        DateTime windowEnd)
    {
        return new CodeCharacteristicsResult
        {
            CommitsPerDay = 0,
            CommitsPerWeek = 0,
            TotalCommits = 0,
            CommitDaysCount = 0,
            CommitSizeMedian = null,
            CommitSizeP95 = null,
            CommitSizeAverage = null,
            MrSizeDistribution = new MrSizeDistribution
            {
                SmallCount = 0,
                MediumCount = 0,
                LargeCount = 0,
                ExtraLargeCount = 0,
                SmallPercentage = 0,
                MediumPercentage = 0,
                LargePercentage = 0,
                ExtraLargePercentage = 0
            },
            TotalMergedMrs = 0,
            TopFilesByChurn = new List<FileChurnInfo>(),
            SquashMergeRate = 0,
            SquashedMrsCount = 0,
            AverageCommitMessageLength = 0,
            ConventionalCommitRate = 0,
            ConventionalCommitsCount = 0,
            BranchNamingComplianceRate = 0,
            CompliantBranchesCount = 0,
            Projects = new List<ProjectCodeCharacteristicsSummary>()
        };
    }

    private sealed class ProjectData
    {
        public required long ProjectId { get; init; }
        public required string ProjectName { get; init; }
        public required List<GitLabCommit> Commits { get; init; }
        public required List<GitLabMergeRequest> MergeRequests { get; init; }
    }
}
