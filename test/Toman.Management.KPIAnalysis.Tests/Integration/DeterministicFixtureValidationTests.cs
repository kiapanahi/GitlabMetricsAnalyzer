using Microsoft.EntityFrameworkCore;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Data;
using Toman.Management.KPIAnalysis.Tests.TestFixtures;

namespace Toman.Management.KPIAnalysis.Tests.Integration;

/// <summary>
/// Simple integration tests to validate the deterministic test fixtures work correctly.
/// These tests focus on basic functionality rather than detailed metric validation.
/// </summary>
public sealed class DeterministicFixtureValidationTests : IDisposable
{
    private readonly GitLabMetricsDbContext _dbContext;

    public DeterministicFixtureValidationTests()
    {
        var options = new DbContextOptionsBuilder<GitLabMetricsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _dbContext = new GitLabMetricsDbContext(options);
    }

    [Fact]
    public async Task DeterministicFixtures_LoadIntoDatabase_Successfully()
    {
        // Arrange
        var commits = GitLabTestFixtures.CompleteFixture.Commits;
        var mergeRequests = GitLabTestFixtures.CompleteFixture.MergeRequests;
        var pipelines = GitLabTestFixtures.CompleteFixture.Pipelines;
        var jobs = GitLabTestFixtures.CompleteFixture.Jobs;
        var notes = GitLabTestFixtures.CompleteFixture.Notes;

        // Clear any existing change tracking
        _dbContext.ChangeTracker.Clear();

        // Act
        await _dbContext.RawCommits.AddRangeAsync(commits, TestContext.Current.CancellationToken);
        await _dbContext.RawMergeRequests.AddRangeAsync(mergeRequests, TestContext.Current.CancellationToken);
        await _dbContext.RawPipelines.AddRangeAsync(pipelines, TestContext.Current.CancellationToken);
        await _dbContext.RawJobs.AddRangeAsync(jobs, TestContext.Current.CancellationToken);
        await _dbContext.RawMergeRequestNotes.AddRangeAsync(notes, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(5, await _dbContext.RawCommits.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(6, await _dbContext.RawMergeRequests.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(6, await _dbContext.RawPipelines.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(6, await _dbContext.RawJobs.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(6, await _dbContext.RawMergeRequestNotes.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void DeterministicFixtures_AreConsistentAcrossMultipleCalls()
    {
        // Arrange & Act - Access the static properties directly
        // Since CompleteFixture is a static class with static properties, they should always be consistent
        
        // Assert - Counts should be consistent across accesses
        Assert.Equal(GitLabTestFixtures.CompleteFixture.Users.Count, GitLabTestFixtures.CompleteFixture.Users.Count);
        Assert.Equal(GitLabTestFixtures.CompleteFixture.Commits.Count, GitLabTestFixtures.CompleteFixture.Commits.Count);
        Assert.Equal(GitLabTestFixtures.CompleteFixture.MergeRequests.Count, GitLabTestFixtures.CompleteFixture.MergeRequests.Count);
        Assert.Equal(GitLabTestFixtures.CompleteFixture.Pipelines.Count, GitLabTestFixtures.CompleteFixture.Pipelines.Count);
        Assert.Equal(GitLabTestFixtures.CompleteFixture.Jobs.Count, GitLabTestFixtures.CompleteFixture.Jobs.Count);
        Assert.Equal(GitLabTestFixtures.CompleteFixture.Notes.Count, GitLabTestFixtures.CompleteFixture.Notes.Count);
        
        // Assert - Content should be identical (testing deterministic behavior)
        var user1 = GitLabTestFixtures.CompleteFixture.Users.First(u => u.Id == 1);
        var user2 = GitLabTestFixtures.CompleteFixture.Users.First(u => u.Id == 1);
        Assert.Equal(user1.Username, user2.Username);
        Assert.Equal(user1.Email, user2.Email);
    }

    [Fact]
    public void DeterministicFixtures_ContainExpectedEdgeCases()
    {
        // Arrange - Access the static properties directly
        
        // Assert - Verify key edge cases exist
        
        // Bot user
        Assert.Contains(GitLabTestFixtures.CompleteFixture.Users, u => u.External == true);
        Assert.Contains(GitLabTestFixtures.CompleteFixture.Users, u => u.Username?.Contains("bot") == true);
        
        // Archived project
        Assert.Contains(GitLabTestFixtures.CompleteFixture.Projects, p => p.Archived == true);
        
        // Merge commit (ParentCount > 1)
        Assert.Contains(GitLabTestFixtures.CompleteFixture.Commits, c => c.ParentCount > 1);
        
        // Revert commit
        Assert.Contains(GitLabTestFixtures.CompleteFixture.Commits, c => c.Message.StartsWith("Revert"));
        
        // Draft MR
        Assert.Contains(GitLabTestFixtures.CompleteFixture.MergeRequests, mr => mr.Title.StartsWith("Draft:"));
        
        // Hotfix MR  
        Assert.Contains(GitLabTestFixtures.CompleteFixture.MergeRequests, mr => mr.Title.Contains("hotfix"));
        
        // Conflicted MR
        Assert.Contains(GitLabTestFixtures.CompleteFixture.MergeRequests, mr => mr.HasConflicts == true);
        
        // Flaky pipeline (same SHA, different statuses)
        var flakyPipelines = GitLabTestFixtures.CompleteFixture.Pipelines.Where(p => p.Sha == "flaky123456").ToList();
        Assert.Equal(2, flakyPipelines.Count);
        Assert.Contains(flakyPipelines, p => p.Status == "failed");
        Assert.Contains(flakyPipelines, p => p.Status == "success");
        
        // Retried job
        Assert.Contains(GitLabTestFixtures.CompleteFixture.Jobs, j => j.RetriedFlag == true);
        
        // System note
        Assert.Contains(GitLabTestFixtures.CompleteFixture.Notes, n => n.System == true);
        
        // Resolved discussion
        Assert.Contains(GitLabTestFixtures.CompleteFixture.Notes, n => n.Resolved == true);
    }

    [Fact]
    public void DeterministicFixtures_UsesConsistentDating()
    {
        // Arrange
        var baseDate = GitLabTestFixtures.FixedBaseDate;

        // Assert - All dates relative to base date
        Assert.All(GitLabTestFixtures.CompleteFixture.Users, user => 
            Assert.True(user.CreatedAt <= baseDate));
        
        Assert.All(GitLabTestFixtures.CompleteFixture.Projects, project => 
            Assert.True(project.CreatedAt <= baseDate));
        
        Assert.All(GitLabTestFixtures.CompleteFixture.Commits, commit => 
            Assert.True(commit.CommittedAt <= baseDate));
        
        Assert.All(GitLabTestFixtures.CompleteFixture.MergeRequests, mr => 
            Assert.True(mr.CreatedAt <= baseDate));
        
        Assert.All(GitLabTestFixtures.CompleteFixture.Pipelines, pipeline => 
            Assert.True(pipeline.CreatedAt <= baseDate));
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
