using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Toman.Management.KPIAnalysis.ApiService.Configuration;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

public interface IDataEnrichmentService
{
    /// <summary>
    /// Enrich merge request with additional analysis data
    /// </summary>
    RawMergeRequest EnrichMergeRequest(RawMergeRequest mergeRequest, IReadOnlyList<RawCommit>? commits = null);
    
    /// <summary>
    /// Enrich commit with file exclusion analysis
    /// </summary>
    RawCommit EnrichCommit(RawCommit commit, IEnumerable<string>? changedFiles = null);
    
    /// <summary>
    /// Check if a file should be excluded from metrics
    /// </summary>
    bool ShouldExcludeFile(string filePath);
    
    /// <summary>
    /// Check if a commit should be excluded from metrics
    /// </summary>
    bool ShouldExcludeCommit(string commitMessage);
    
    /// <summary>
    /// Check if a branch should be excluded from metrics
    /// </summary>
    bool ShouldExcludeBranch(string branchName);
}

public sealed class DataEnrichmentService : IDataEnrichmentService
{
    private readonly MetricsConfiguration _metricsConfig;
    private readonly ILogger<DataEnrichmentService> _logger;

    // Compiled regex patterns for performance
    private readonly Lazy<List<Regex>> _fileExcludePatterns;
    private readonly Lazy<List<Regex>> _commitExcludePatterns;
    private readonly Lazy<List<Regex>> _branchExcludePatterns;
    private readonly Lazy<List<Regex>> _hotfixPatterns;
    private readonly Lazy<List<Regex>> _revertPatterns;

    public DataEnrichmentService(
        IOptions<MetricsConfiguration> metricsConfig,
        ILogger<DataEnrichmentService> logger)
    {
        _metricsConfig = metricsConfig.Value;
        _logger = logger;

        _fileExcludePatterns = new Lazy<List<Regex>>(() =>
            CompilePatterns(_metricsConfig.Excludes.FilePatterns, "file exclusion"));
        
        _commitExcludePatterns = new Lazy<List<Regex>>(() =>
            CompilePatterns(_metricsConfig.Excludes.CommitPatterns, "commit exclusion"));
        
        _branchExcludePatterns = new Lazy<List<Regex>>(() =>
            CompilePatterns(_metricsConfig.Excludes.BranchPatterns, "branch exclusion"));

        // Default patterns for hotfix detection
        var hotfixPatterns = new List<string>
        {
            @"\bhotfix\b", @"\bhot-fix\b", @"\bfix\b", @"\bemergency\b", @"\bcritical\b", @"\burgent\b"
        };
        
        _hotfixPatterns = new Lazy<List<Regex>>(() =>
            CompilePatterns(hotfixPatterns, "hotfix detection"));

        // Default patterns for revert detection
        var revertPatterns = new List<string>
        {
            @"^revert\b", @"\brevert\b", @"^rollback\b", @"\brollback\b", @"\bundo\b"
        };
        
        _revertPatterns = new Lazy<List<Regex>>(() =>
            CompilePatterns(revertPatterns, "revert detection"));
    }

    public RawMergeRequest EnrichMergeRequest(RawMergeRequest mergeRequest, IReadOnlyList<RawCommit>? commits = null)
    {
        try
        {
            // Determine if it's a hotfix
            var isHotfix = IsHotfix(mergeRequest.Title, mergeRequest.SourceBranch, mergeRequest.Labels);
            
            // Determine if it's a revert
            var isRevert = IsRevert(mergeRequest.Title, commits);
            
            // Get first commit information
            var firstCommit = commits?.OrderBy(c => c.CommittedAt).FirstOrDefault();
            
            // Calculate enhanced statistics from commits
            var linesAdded = commits?.Sum(c => c.Additions) ?? 0;
            var linesDeleted = commits?.Sum(c => c.Deletions) ?? 0;
            var commitsCount = commits?.Count ?? 0;

            return new RawMergeRequest
            {
                Id = mergeRequest.Id,
                ProjectId = mergeRequest.ProjectId,
                ProjectName = mergeRequest.ProjectName,
                MrId = mergeRequest.MrId,
                AuthorUserId = mergeRequest.AuthorUserId,
                AuthorName = mergeRequest.AuthorName,
                Title = mergeRequest.Title,
                CreatedAt = mergeRequest.CreatedAt,
                MergedAt = mergeRequest.MergedAt,
                ClosedAt = mergeRequest.ClosedAt,
                State = mergeRequest.State,
                ChangesCount = mergeRequest.ChangesCount,
                SourceBranch = mergeRequest.SourceBranch,
                TargetBranch = mergeRequest.TargetBranch,
                ApprovalsRequired = mergeRequest.ApprovalsRequired,
                ApprovalsGiven = mergeRequest.ApprovalsGiven,
                FirstReviewAt = mergeRequest.FirstReviewAt,
                ReviewerIds = mergeRequest.ReviewerIds,
                IngestedAt = mergeRequest.IngestedAt,
                Labels = mergeRequest.Labels,
                FirstCommitSha = firstCommit?.CommitId,
                FirstCommitAt = firstCommit?.CommittedAt,
                FirstCommitMessage = firstCommit?.Message,
                IsHotfix = isHotfix,
                IsRevert = isRevert,
                IsDraft = IsDraft(mergeRequest.Title),
                HasConflicts = mergeRequest.HasConflicts,
                CommitsCount = commitsCount,
                LinesAdded = linesAdded,
                LinesDeleted = linesDeleted,
                WebUrl = mergeRequest.WebUrl
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enrich merge request {MrId} in project {ProjectId}", 
                mergeRequest.MrId, mergeRequest.ProjectId);
            return mergeRequest;
        }
    }

    public RawCommit EnrichCommit(RawCommit commit, IEnumerable<string>? changedFiles = null)
    {
        try
        {
            if (changedFiles is null)
            {
                return commit;
            }

            var files = changedFiles.ToList();
            var excludedFiles = files.Where(ShouldExcludeFile).ToList();
            var includedFiles = files.Except(excludedFiles).ToList();

            // Calculate the proportion of excluded lines
            var totalFiles = files.Count;
            var excludedFileCount = excludedFiles.Count;
            var inclusionRatio = totalFiles > 0 ? (double)(totalFiles - excludedFileCount) / totalFiles : 1.0;

            return new RawCommit
            {
                Id = commit.Id,
                ProjectId = commit.ProjectId,
                ProjectName = commit.ProjectName,
                CommitId = commit.CommitId,
                AuthorUserId = commit.AuthorUserId,
                AuthorName = commit.AuthorName,
                AuthorEmail = commit.AuthorEmail,
                CommittedAt = commit.CommittedAt,
                Message = commit.Message,
                Additions = commit.Additions,
                Deletions = commit.Deletions,
                IsSigned = commit.IsSigned,
                IngestedAt = commit.IngestedAt,
                FilesChanged = totalFiles,
                FilesChangedExcluded = includedFiles.Count,
                AdditionsExcluded = (int)(commit.Additions * inclusionRatio),
                DeletionsExcluded = (int)(commit.Deletions * inclusionRatio),
                IsMergeCommit = IsMergeCommit(commit.Message, commit.ParentCount),
                ParentCount = commit.ParentCount,
                ParentShas = commit.ParentShas,
                WebUrl = commit.WebUrl,
                ShortSha = commit.CommitId.Length >= 8 ? commit.CommitId[..8] : commit.CommitId
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enrich commit {CommitId} in project {ProjectId}", 
                commit.CommitId, commit.ProjectId);
            return commit;
        }
    }

    public bool ShouldExcludeFile(string filePath)
    {
        return MatchesAnyPattern(_fileExcludePatterns.Value, filePath);
    }

    public bool ShouldExcludeCommit(string commitMessage)
    {
        return MatchesAnyPattern(_commitExcludePatterns.Value, commitMessage);
    }

    public bool ShouldExcludeBranch(string branchName)
    {
        return MatchesAnyPattern(_branchExcludePatterns.Value, branchName);
    }

    private bool IsHotfix(string title, string sourceBranch, string? labels)
    {
        // Check title
        if (MatchesAnyPattern(_hotfixPatterns.Value, title))
            return true;

        // Check branch name
        if (MatchesAnyPattern(_hotfixPatterns.Value, sourceBranch))
            return true;

        // Check labels
        if (!string.IsNullOrEmpty(labels))
        {
            try
            {
                var labelList = JsonSerializer.Deserialize<string[]>(labels);
                if (labelList?.Any(label => MatchesAnyPattern(_hotfixPatterns.Value, label)) == true)
                    return true;
            }
            catch (JsonException ex)
            {
                _logger.LogDebug(ex, "Failed to parse labels JSON: {Labels}", labels);
            }
        }

        return false;
    }

    private bool IsRevert(string title, IReadOnlyList<RawCommit>? commits)
    {
        // Check title
        if (MatchesAnyPattern(_revertPatterns.Value, title))
            return true;

        // Check commit messages
        if (commits?.Any(c => MatchesAnyPattern(_revertPatterns.Value, c.Message)) == true)
            return true;

        return false;
    }

    private static bool IsDraft(string title)
    {
        return title.StartsWith("Draft:", StringComparison.OrdinalIgnoreCase) ||
               title.StartsWith("WIP:", StringComparison.OrdinalIgnoreCase) ||
               title.Contains("[WIP]", StringComparison.OrdinalIgnoreCase) ||
               title.Contains("(WIP)", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMergeCommit(string message, int parentCount)
    {
        return parentCount > 1 || 
               message.StartsWith("Merge branch", StringComparison.OrdinalIgnoreCase) ||
               message.StartsWith("Merge pull request", StringComparison.OrdinalIgnoreCase);
    }

    private List<Regex> CompilePatterns(IEnumerable<string> patterns, string patternType)
    {
        var compiledPatterns = new List<Regex>();
        
        foreach (var pattern in patterns)
        {
            try
            {
                var regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
                compiledPatterns.Add(regex);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to compile {PatternType} regex pattern: {Pattern}", patternType, pattern);
            }
        }

        return compiledPatterns;
    }

    private bool MatchesAnyPattern(List<Regex> patterns, string input)
    {
        if (string.IsNullOrEmpty(input))
            return false;

        foreach (var pattern in patterns)
        {
            try
            {
                if (pattern.IsMatch(input))
                    return true;
            }
            catch (RegexMatchTimeoutException ex)
            {
                _logger.LogWarning(ex, "Regex match timeout for pattern on input: {Input}", input);
            }
        }

        return false;
    }
}