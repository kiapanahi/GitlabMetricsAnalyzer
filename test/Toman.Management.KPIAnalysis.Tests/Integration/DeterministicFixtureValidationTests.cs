using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Toman.Management.KPIAnalysis.ApiService.Configuration;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Data;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;
using Toman.Management.KPIAnalysis.Tests.TestFixtures;
using Xunit;

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

        // Act
        await _dbContext.RawCommits.AddRangeAsync(commits);
        await _dbContext.RawMergeRequests.AddRangeAsync(mergeRequests);
        await _dbContext.RawPipelines.AddRangeAsync(pipelines);
        await _dbContext.RawJobs.AddRangeAsync(jobs);
        await _dbContext.RawMergeRequestNotes.AddRangeAsync(notes);
        await _dbContext.SaveChangesAsync();

        // Assert
        Assert.Equal(5, await _dbContext.RawCommits.CountAsync());
        Assert.Equal(6, await _dbContext.RawMergeRequests.CountAsync());
        Assert.Equal(6, await _dbContext.RawPipelines.CountAsync());
        Assert.Equal(6, await _dbContext.RawJobs.CountAsync());
        Assert.Equal(6, await _dbContext.RawMergeRequestNotes.CountAsync());
    }

    [Fact]
    public void DeterministicFixtures_AreConsistentAcrossMultipleCalls()
    {
        // Arrange & Act
        var fixture1 = GitLabTestFixtures.CompleteFixture;
        var fixture2 = GitLabTestFixtures.CompleteFixture;

        // Assert - Counts should be identical
        Assert.Equal(fixture1.Users.Count, fixture2.Users.Count);
        Assert.Equal(fixture1.Commits.Count, fixture2.Commits.Count);
        Assert.Equal(fixture1.MergeRequests.Count, fixture2.MergeRequests.Count);
        Assert.Equal(fixture1.Pipelines.Count, fixture2.Pipelines.Count);
        Assert.Equal(fixture1.Jobs.Count, fixture2.Jobs.Count);
        Assert.Equal(fixture1.Notes.Count, fixture2.Notes.Count);

        // Assert - Content should be identical  
        var user1 = fixture1.Users.First(u => u.Id == 1);
        var user2 = fixture2.Users.First(u => u.Id == 1);
        Assert.Equal(user1.Username, user2.Username);
        Assert.Equal(user1.Email, user2.Email);
    }

    [Fact]
    public void DeterministicFixtures_ContainExpectedEdgeCases()
    {
        // Arrange
        var fixtures = GitLabTestFixtures.CompleteFixture;

        // Assert - Verify key edge cases exist
        
        // Bot user
        Assert.Contains(fixtures.Users, u => u.External == true);
        Assert.Contains(fixtures.Users, u => u.Username.Contains("bot"));
        
        // Archived project
        Assert.Contains(fixtures.Projects, p => p.Archived == true);
        
        // Merge commit (ParentCount > 1)
        Assert.Contains(fixtures.Commits, c => c.ParentCount > 1);
        
        // Revert commit
        Assert.Contains(fixtures.Commits, c => c.Message.StartsWith("Revert"));
        
        // Draft MR
        Assert.Contains(fixtures.MergeRequests, mr => mr.Title.StartsWith("Draft:"));
        
        // Hotfix MR  
        Assert.Contains(fixtures.MergeRequests, mr => mr.Title.Contains("hotfix"));
        
        // Conflicted MR
        Assert.Contains(fixtures.MergeRequests, mr => mr.HasConflicts == true);
        
        // Flaky pipeline (same SHA, different statuses)
        var flakyPipelines = fixtures.Pipelines.Where(p => p.Sha == "flaky123456").ToList();
        Assert.Equal(2, flakyPipelines.Count);
        Assert.Contains(flakyPipelines, p => p.Status == "failed");
        Assert.Contains(flakyPipelines, p => p.Status == "success");
        
        // Retried job
        Assert.Contains(fixtures.Jobs, j => j.RetriedFlag == true);
        
        // System note
        Assert.Contains(fixtures.Notes, n => n.System == true);
        
        // Resolved discussion
        Assert.Contains(fixtures.Notes, n => n.Resolved == true);
    }

    [Fact]
    public void DeterministicFixtures_UsesConsistentDating()
    {
        // Arrange
        var fixtures = GitLabTestFixtures.CompleteFixture;
        var baseDate = GitLabTestFixtures.FixedBaseDate;

        // Assert - All dates relative to base date
        Assert.All(fixtures.Users, user => 
            Assert.True(user.CreatedAt <= baseDate));
        
        Assert.All(fixtures.Projects, project => 
            Assert.True(project.CreatedAt <= baseDate));
        
        Assert.All(fixtures.Commits, commit => 
            Assert.True(commit.CommittedAt <= baseDate));
        
        Assert.All(fixtures.MergeRequests, mr => 
            Assert.True(mr.CreatedAt <= baseDate));
        
        Assert.All(fixtures.Pipelines, pipeline => 
            Assert.True(pipeline.CreatedAt <= baseDate));
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}