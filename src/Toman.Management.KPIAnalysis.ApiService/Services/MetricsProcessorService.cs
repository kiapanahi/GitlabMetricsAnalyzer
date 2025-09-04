using Microsoft.EntityFrameworkCore;

using Toman.Management.KPIAnalysis.ApiService.Data;
using Toman.Management.KPIAnalysis.ApiService.Data.Extensions;
using Toman.Management.KPIAnalysis.ApiService.Models.Facts;
using Toman.Management.KPIAnalysis.ApiService.Models.Raw;

namespace Toman.Management.KPIAnalysis.ApiService.Services;

public interface IMetricsProcessorService
{
    Task ProcessFactsAsync(CancellationToken cancellationToken = default);
}

public sealed class MetricsProcessorService : IMetricsProcessorService
{
    private readonly GitLabMetricsDbContext _dbContext;
    private readonly ILogger<MetricsProcessorService> _logger;

    public MetricsProcessorService(GitLabMetricsDbContext dbContext, ILogger<MetricsProcessorService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task ProcessFactsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting metrics processing");

        await ProcessMergeRequestFactsAsync(cancellationToken);
        await ProcessPipelineFactsAsync(cancellationToken);
        await ProcessGitHygieneFactsAsync(cancellationToken);
        await ProcessReleaseFactsAsync(cancellationToken);

        _logger.LogInformation("Completed metrics processing");
    }

    private async Task ProcessMergeRequestFactsAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Processing MR facts");

        var mergeRequests = await _dbContext.RawMergeRequests
            .Where(mr => mr.State == "merged" && mr.MergedAt.HasValue)
            .ToListAsync(cancellationToken);

        var facts = new List<FactMergeRequest>();

        foreach (var mr in mergeRequests)
        {
            if (!mr.MergedAt.HasValue) continue;

            // Calculate cycle time (created to merged)
            var cycleTimeHours = (decimal)(mr.MergedAt.Value - mr.CreatedAt).TotalHours;

            // Calculate review wait time (first review would need additional data)
            var reviewWaitHours = mr.FirstReviewAt.HasValue 
                ? (decimal)(mr.FirstReviewAt.Value - mr.CreatedAt).TotalHours 
                : 0;

            // Rework count (would need force-push data)
            var reworkCount = 0;

            var fact = new FactMergeRequest
            {
                MrId = mr.MrId,
                ProjectId = mr.ProjectId,
                CycleTimeHours = cycleTimeHours,
                ReviewWaitHours = reviewWaitHours,
                ReworkCount = reworkCount,
                LinesAdded = 0,   // Would need commit data linking
                LinesRemoved = 0  // Would need commit data linking
            };

            facts.Add(fact);
        }

        await _dbContext.UpsertRangeAsync(facts, cancellationToken);
        _logger.LogDebug("Processed {Count} MR facts", facts.Count);
    }

    private async Task ProcessPipelineFactsAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Processing pipeline facts");

        var pipelines = await _dbContext.RawPipelines
            .OrderBy(p => p.ProjectId)
            .ThenBy(p => p.CreatedAt)
            .ToListAsync(cancellationToken);

        var facts = new List<FactPipeline>();
        var pipelinesByProject = pipelines.GroupBy(p => p.ProjectId);

        foreach (var projectPipelines in pipelinesByProject)
        {
            var orderedPipelines = projectPipelines.OrderBy(p => p.CreatedAt).ToList();

            for (var i = 0; i < orderedPipelines.Count; i++)
            {
                var pipeline = orderedPipelines[i];

                // Calculate MTG (Mean Time to Green)
                var mtgSeconds = CalculateMtgSeconds(orderedPipelines, i);

                // Infer if it's a production deployment
                var isProd = IsProductionPipeline(pipeline);

                // Check if it's a rollback
                var isRollback = IsRollbackPipeline(pipeline);

                // Check if it's a flaky candidate
                var isFlakyCandidate = IsFlakyCandidate(orderedPipelines, i);

                var fact = new FactPipeline
                {
                    PipelineId = pipeline.PipelineId,
                    ProjectId = pipeline.ProjectId,
                    MtgSeconds = mtgSeconds,
                    IsProd = isProd,
                    IsRollback = isRollback,
                    IsFlakyCandidate = isFlakyCandidate,
                    DurationSec = pipeline.DurationSec
                };

                facts.Add(fact);
            }
        }

        await _dbContext.UpsertRangeAsync(facts, cancellationToken);
        _logger.LogDebug("Processed {Count} pipeline facts", facts.Count);
    }

    private async Task ProcessGitHygieneFactsAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Processing git hygiene facts");

        // Get commits on default branches
        var projects = await _dbContext.DimProjects.ToListAsync(cancellationToken);
        var facts = new List<FactGitHygiene>();

        foreach (var project in projects)
        {
            var commits = await _dbContext.RawCommits
                .Where(c => c.ProjectId == project.ProjectId)
                .ToListAsync(cancellationToken);

            var commitsByDay = commits
                .GroupBy(c => DateOnly.FromDateTime(c.CommittedAt.Date))
                .ToList();

            foreach (var dayCommits in commitsByDay)
            {
                // This is simplified - would need more data to properly detect direct pushes
                var directPushes = dayCommits.Count(c => !c.IsSigned); // Heuristic
                var unsignedCommits = dayCommits.Count(c => !c.IsSigned);

                var fact = new FactGitHygiene
                {
                    ProjectId = project.ProjectId,
                    Day = dayCommits.Key,
                    DirectPushesDefault = directPushes,
                    ForcePushesProtected = 0, // Would need audit log data
                    UnsignedCommitCount = unsignedCommits
                };

                facts.Add(fact);
            }
        }

        await _dbContext.UpsertRangeAsync(facts, cancellationToken);
        _logger.LogDebug("Processed {Count} git hygiene facts", facts.Count);
    }

    private async Task ProcessReleaseFactsAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Processing release facts");

        var releases = await _dbContext.DimReleases.ToListAsync(cancellationToken);
        var facts = new List<FactRelease>();

        foreach (var release in releases)
        {
            var cadenceBucket = CalculateReleaseCadenceBucket(release.ReleasedAt);

            var fact = new FactRelease
            {
                TagName = release.TagName,
                ProjectId = release.ProjectId,
                IsSemver = release.SemverValid,
                CadenceBucket = cadenceBucket
            };

            facts.Add(fact);
        }

        await _dbContext.UpsertRangeAsync(facts, cancellationToken);
        _logger.LogDebug("Processed {Count} release facts", facts.Count);
    }

    private static int CalculateMtgSeconds(List<RawPipeline> pipelines, int currentIndex)
    {
        var current = pipelines[currentIndex];
        
        // If current pipeline passed, MTG is 0
        if (current.Status == "success")
            return 0;

        // Look for the next successful pipeline on the same ref
        for (var i = currentIndex + 1; i < pipelines.Count; i++)
        {
            var next = pipelines[i];
            if (next.Ref == current.Ref && next.Status == "success")
            {
                return (int)(next.CreatedAt - current.CreatedAt).TotalSeconds;
            }
        }

        return 0;
    }

    private static bool IsProductionPipeline(RawPipeline pipeline)
    {
        // Inference rules from PRD:
        // - Pipeline on default branch AND/OR environment=production AND/OR associated with release tag
        
        var isDefaultBranch = pipeline.Ref.EndsWith("/main") || 
                             pipeline.Ref.EndsWith("/master") || 
                             pipeline.Ref.EndsWith("/production");
        
        var isProdEnvironment = pipeline.Environment?.ToLowerInvariant().Contains("prod") == true;
        
        var isReleaseTag = pipeline.Ref.StartsWith("refs/tags/v") || 
                          pipeline.Ref.StartsWith("refs/tags/release");

        return isDefaultBranch || isProdEnvironment || isReleaseTag;
    }

    private static bool IsRollbackPipeline(RawPipeline pipeline)
    {
        // Simple heuristic - would need more sophisticated detection
        return pipeline.Ref.ToLowerInvariant().Contains("rollback") ||
               pipeline.Ref.ToLowerInvariant().Contains("revert");
    }

    private static bool IsFlakyCandidate(List<RawPipeline> pipelines, int currentIndex)
    {
        var current = pipelines[currentIndex];
        
        if (current.Status != "failed")
            return false;

        // Look for a retry on the same SHA that succeeded
        for (var i = currentIndex + 1; i < pipelines.Count; i++)
        {
            var next = pipelines[i];
            if (next.Sha == current.Sha && next.Status == "success")
            {
                return true;
            }
        }

        return false;
    }

    private static string CalculateReleaseCadenceBucket(DateTimeOffset releasedAt)
    {
        var now = DateTimeOffset.UtcNow;
        var daysSinceRelease = (now - releasedAt).TotalDays;

        return daysSinceRelease switch
        {
            <= 7 => "weekly",
            <= 14 => "bi-weekly", 
            <= 30 => "monthly",
            <= 90 => "quarterly",
            _ => "annual+"
        };
    }
}
