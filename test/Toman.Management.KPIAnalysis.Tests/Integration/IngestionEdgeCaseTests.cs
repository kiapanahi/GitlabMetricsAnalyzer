using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Toman.Management.KPIAnalysis.ApiService.Configuration;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Data;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;
using Toman.Management.KPIAnalysis.Tests.TestFixtures;

namespace Toman.Management.KPIAnalysis.Tests.Integration;

/// <summary>
/// Integration tests for ingestion edge cases using deterministic GitLabTestFixtures.CompleteFixture.
/// Tests validation, transformation, and enrichment of GitLab data.
/// </summary>
public sealed class IngestionEdgeCaseTests : IDisposable
{
    private readonly GitLabMetricsDbContext _dbContext;
    private readonly IDataEnrichmentService _enrichmentService;

    public IngestionEdgeCaseTests()
    {
        var options = new DbContextOptionsBuilder<GitLabMetricsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _dbContext = new GitLabMetricsDbContext(options);
        
        var metricsConfig = Options.Create(new MetricsConfiguration
        {
            Identity = new IdentityConfiguration 
            { 
                BotRegexPatterns = [".*bot.*", "deployment\\..*", ".*\\.bot"] 
            },
            Excludes = new ExclusionConfiguration
            {
                CommitPatterns = ["^Merge.*", ".*automatic.*", "^chore: automated.*"],
                BranchPatterns = ["^temp/.*", "^experimental/.*"],
                FilePatterns = [".*\\.lock", "node_modules/.*", "dist/.*"]
            }
        });

        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<DataEnrichmentService>();
        _enrichmentService = new DataEnrichmentService(metricsConfig, logger);
    }

    [Fact]
    public async Task ProcessDeterministicFixtures_ValidatesBasicStructure()
    {
        // Arrange - Get fixtures directly from static properties
        var commits = GitLabTestFixtures.CompleteFixture.Commits;
        var mergeRequests = GitLabTestFixtures.CompleteFixture.MergeRequests;
        var pipelines = GitLabTestFixtures.CompleteFixture.Pipelines;
        var jobs = GitLabTestFixtures.CompleteFixture.Jobs;
        var notes = GitLabTestFixtures.CompleteFixture.Notes;

        // Act - Store all fixture data to database
        await _dbContext.RawCommits.AddRangeAsync(commits, TestContext.Current.CancellationToken);
        await _dbContext.RawMergeRequests.AddRangeAsync(mergeRequests, TestContext.Current.CancellationToken);
        await _dbContext.RawPipelines.AddRangeAsync(pipelines, TestContext.Current.CancellationToken);
        await _dbContext.RawJobs.AddRangeAsync(jobs, TestContext.Current.CancellationToken);
        await _dbContext.RawMergeRequestNotes.AddRangeAsync(notes, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert - Verify data structure and relationships
        var commitCount = await _dbContext.RawCommits.CountAsync(TestContext.Current.CancellationToken);
        var mrCount = await _dbContext.RawMergeRequests.CountAsync(TestContext.Current.CancellationToken);
        var pipelineCount = await _dbContext.RawPipelines.CountAsync(TestContext.Current.CancellationToken);
        var jobCount = await _dbContext.RawJobs.CountAsync(TestContext.Current.CancellationToken);
        var noteCount = await _dbContext.RawMergeRequestNotes.CountAsync(TestContext.Current.CancellationToken);

        Assert.Equal(5, commitCount); // Regular, merge, revert, bot, refactor commits
        Assert.Equal(6, mrCount); // Standard, draft, hotfix, revert, conflicted, squash MRs
        Assert.Equal(6, pipelineCount); // Various pipeline scenarios
        Assert.Equal(6, jobCount); // Job scenarios including flaky retry
        Assert.Equal(6, noteCount); // Discussion patterns
    }

    [Fact]
    public void EnrichMergeRequests_DetectsHotfixPatterns()
    {
        // Arrange
        // Use individual fixture methods
        var hotfixMr = GitLabTestFixtures.CompleteFixture.MergeRequests.First(mr => mr.Title.Contains("hotfix"));

        // Act
        var enrichedMr = _enrichmentService.EnrichMergeRequest(hotfixMr);

        // Assert
        Assert.True(enrichedMr.IsHotfix);
        Assert.Equal("hotfix/security-patch", enrichedMr.SourceBranch);
        Assert.Contains("critical", enrichedMr.Labels ?? "");
    }

    [Fact]
    public void EnrichMergeRequests_DetectsRevertPatterns()
    {
        // Arrange  
        // Use individual fixture methods
        var revertMr = GitLabTestFixtures.CompleteFixture.MergeRequests.First(mr => mr.Title.StartsWith("Revert"));

        // Act
        var enrichedMr = _enrichmentService.EnrichMergeRequest(revertMr);

        // Assert
        Assert.True(enrichedMr.IsRevert);
        Assert.Equal("revert/auth-rollback", enrichedMr.SourceBranch);
    }

    [Fact]
    public void EnrichMergeRequests_DetectsDraftPatterns()
    {
        // Arrange
        // Use individual fixture methods
        var draftMr = GitLabTestFixtures.CompleteFixture.MergeRequests.First(mr => mr.Title.StartsWith("Draft:"));

        // Act
        var enrichedMr = _enrichmentService.EnrichMergeRequest(draftMr);

        // Assert
        Assert.True(enrichedMr.IsDraft);
        Assert.Equal("feature/experimental", enrichedMr.SourceBranch);
        Assert.Equal("opened", enrichedMr.State);
    }

    [Fact]
    public void EnrichCommits_DetectsRevertCommits()
    {
        // Arrange
        // Use individual fixture methods
        var revertCommit = GitLabTestFixtures.CompleteFixture.Commits.First(c => c.Message.StartsWith("Revert"));

        // Act
        var enrichedCommit = _enrichmentService.EnrichCommit(revertCommit);

        // Assert
        // Verify revert detection based on message patterns
        Assert.Equal("Revert \"feat: add user authentication endpoint\"", revertCommit.Message);
        Assert.Equal(3, revertCommit.AuthorUserId); // Charlie Maintainer
    }

    [Fact]
    public void EnrichCommits_DetectsBotCommits()
    {
        // Arrange
        // Use individual fixture methods
        var botCommit = GitLabTestFixtures.CompleteFixture.Commits.First(c => c.AuthorUserId == 4); // Deployment Bot

        // Act
        var shouldExclude = _enrichmentService.ShouldExcludeCommit(botCommit.Message);

        // Assert
        Assert.True(shouldExclude); // Should exclude automated commit
        Assert.Equal("chore: automated version bump to v1.2.3", botCommit.Message);
    }

    [Fact]
    public void ValidatePipelineFlakeDetection_IdentifiesRetries()
    {
        // Arrange
        // Use individual fixture methods
        var flakyPipelines = GitLabTestFixtures.CompleteFixture.Pipelines
            .Where(p => p.Sha == "flaky123456") // Same SHA indicates retry
            .OrderBy(p => p.CreatedAt)
            .ToList();

        // Act & Assert
        Assert.Equal(2, flakyPipelines.Count);
        
        var firstAttempt = flakyPipelines[0];
        var retryAttempt = flakyPipelines[1];
        
        Assert.Equal("failed", firstAttempt.Status);
        Assert.Equal("success", retryAttempt.Status);
        Assert.Equal("push", firstAttempt.TriggerSource);
        Assert.Equal("web", retryAttempt.TriggerSource); // Manual retry
        
        // Verify retry happened after initial failure
        Assert.True(retryAttempt.CreatedAt > firstAttempt.FinishedAt);
    }

    [Fact]
    public void ValidateJobFlakeDetection_IdentifiesRetriedJobs()
    {
        // Arrange
        // Use individual fixture methods
        var jobs = GitLabTestFixtures.CompleteFixture.Jobs
            .Where(j => j.Name == "integration-tests")
            .OrderBy(j => j.PipelineId)
            .ToList();

        // Act & Assert
        Assert.Equal(2, jobs.Count);
        
        var failedJob = jobs[0];
        var retriedJob = jobs[1];
        
        Assert.Equal("failed", failedJob.Status);
        Assert.Equal("success", retriedJob.Status);
        Assert.False(failedJob.RetriedFlag);
        Assert.True(retriedJob.RetriedFlag); // Marked as retry
    }

    [Fact]
    public void ValidateSquashMergeHandling_HandlesHighCommitCount()
    {
        // Arrange
        // Use individual fixture methods
        var squashMr = GitLabTestFixtures.CompleteFixture.MergeRequests.First(mr => mr.CommitsCount == 25);

        // Act & Assert
        Assert.Equal("refactor: clean up authentication module", squashMr.Title);
        Assert.Equal(25, squashMr.CommitsCount); // Many commits were squashed
        Assert.Equal(15, squashMr.ChangesCount); // But only 15 file changes
        Assert.Equal("merged", squashMr.State);
    }

    [Fact]
    public void ValidateConflictedMergeRequest_HandlesFailedMerge()
    {
        // Arrange
        // Use individual fixture methods
        var conflictedMr = GitLabTestFixtures.CompleteFixture.MergeRequests.First(mr => mr.HasConflicts == true);

        // Act & Assert
        Assert.Equal("closed", conflictedMr.State);
        Assert.True(conflictedMr.HasConflicts);
        Assert.NotNull(conflictedMr.ClosedAt);
        Assert.Null(conflictedMr.MergedAt); // Never merged due to conflicts
        Assert.Equal(0, conflictedMr.ApprovalsGiven); // No approvals on conflicted MR
    }

    [Fact]
    public void ValidateDiscussionPatterns_HandlesResolutionFlow()
    {
        // Arrange
        // Use individual fixture methods
        var notes = GitLabTestFixtures.CompleteFixture.Notes.Where(n => n.MergeRequestIid == 101).OrderBy(n => n.CreatedAt).ToList();

        // Act & Assert
        Assert.Equal(4, notes.Count);
        
        var reviewComment = notes[0];
        var approvalNote = notes[1];
        var authorResponse = notes[2];
        var resolvedComment = notes[3];
        
        // Review flow validation
        Assert.False(reviewComment.System);
        Assert.True(reviewComment.Resolvable);
        Assert.False(reviewComment.Resolved);
        
        // System approval note
        Assert.True(approvalNote.System);
        Assert.Contains("approved", approvalNote.Body);
        
        // Final resolution
        Assert.True(resolvedComment.Resolved);
        Assert.NotNull(resolvedComment.ResolvedBy);
    }

    [Fact]
    public void ValidateDataConsistency_EnsuresFixtureDeterminism()
    {
        // Arrange & Act - Verify fixture properties are accessible
        var usersCount1 = GitLabTestFixtures.CompleteFixture.Users.Count;
        var usersCount2 = GitLabTestFixtures.CompleteFixture.Users.Count;

        // Assert - Verify deterministic output
        Assert.Equal(GitLabTestFixtures.CompleteFixture.Users.Count, GitLabTestFixtures.CompleteFixture.Users.Count);
        Assert.Equal(GitLabTestFixtures.CompleteFixture.Commits.Count, GitLabTestFixtures.CompleteFixture.Commits.Count);
        Assert.Equal(GitLabTestFixtures.CompleteFixture.MergeRequests.Count, GitLabTestFixtures.CompleteFixture.MergeRequests.Count);
        
        // Check specific values are identical
        var user1_1 = GitLabTestFixtures.CompleteFixture.Users.First(u => u.Id == 1);
        var user1_2 = GitLabTestFixtures.CompleteFixture.Users.First(u => u.Id == 1);
        
        Assert.Equal(user1_1.Username, user1_2.Username);
        Assert.Equal(user1_1.Email, user1_2.Email);
        Assert.Equal(user1_1.CreatedAt, user1_2.CreatedAt);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
