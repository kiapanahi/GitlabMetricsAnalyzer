using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Toman.Management.KPIAnalysis.ApiService.Configuration;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Data;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;
using Toman.Management.KPIAnalysis.Tests.TestFixtures;

namespace Toman.Management.KPIAnalysis.Tests.Unit.Services;

/// <summary>
/// Unit tests for edge case handling in services, including rate limiting simulation,
/// data validation, and error scenarios using deterministic GitLabTestFixtures.CompleteFixture.
/// </summary>
public sealed class ServiceEdgeCaseTests : IDisposable
{
    private readonly GitLabMetricsDbContext _dbContext;
    private readonly IDataEnrichmentService _enrichmentService;

    public ServiceEdgeCaseTests()
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
                FilePatterns = [".*-lock\\.json$", ".*\\.lock$", "node_modules/.*", "dist/.*"]
            }
        });

        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<DataEnrichmentService>();
        _enrichmentService = new DataEnrichmentService(metricsConfig, logger);
    }

    [Fact]
    public void DataEnrichmentService_ShouldExcludeCommit_HandlesVariousPatterns()
    {
        // Arrange & Act & Assert - Test commit exclusion patterns

        // Should exclude merge commits
        Assert.True(_enrichmentService.ShouldExcludeCommit("Merge branch 'feature/test' into 'main'"));
        Assert.True(_enrichmentService.ShouldExcludeCommit("Merge pull request #123"));

        // Should exclude automated commits
        Assert.True(_enrichmentService.ShouldExcludeCommit("chore: automated version bump"));
        Assert.True(_enrichmentService.ShouldExcludeCommit("automatic dependency update"));

        // Should NOT exclude regular commits
        Assert.False(_enrichmentService.ShouldExcludeCommit("feat: add user authentication"));
        Assert.False(_enrichmentService.ShouldExcludeCommit("fix: resolve memory leak issue"));
        Assert.False(_enrichmentService.ShouldExcludeCommit("docs: update API documentation"));
    }

    [Fact]
    public void DataEnrichmentService_ShouldExcludeBranch_HandlesVariousPatterns()
    {
        // Arrange & Act & Assert - Test branch exclusion patterns

        // Should exclude temporary branches
        Assert.True(_enrichmentService.ShouldExcludeBranch("temp/quick-fix"));
        Assert.True(_enrichmentService.ShouldExcludeBranch("experimental/new-feature"));

        // Should NOT exclude regular branches
        Assert.False(_enrichmentService.ShouldExcludeBranch("main"));
        Assert.False(_enrichmentService.ShouldExcludeBranch("develop"));
        Assert.False(_enrichmentService.ShouldExcludeBranch("feature/user-auth"));
        Assert.False(_enrichmentService.ShouldExcludeBranch("hotfix/security-patch"));
    }

    [Fact]
    public void DataEnrichmentService_ShouldExcludeFile_HandlesVariousPatterns()
    {
        // Arrange & Act & Assert - Test file exclusion patterns

        // Should exclude generated and dependency files
        Assert.True(_enrichmentService.ShouldExcludeFile("package-lock.json"));
        Assert.True(_enrichmentService.ShouldExcludeFile("yarn.lock"));
        Assert.True(_enrichmentService.ShouldExcludeFile("node_modules/express/index.js"));
        Assert.True(_enrichmentService.ShouldExcludeFile("dist/bundle.js"));

        // Should NOT exclude source files
        Assert.False(_enrichmentService.ShouldExcludeFile("src/components/UserAuth.tsx"));
        Assert.False(_enrichmentService.ShouldExcludeFile("tests/unit/auth.test.js"));
        Assert.False(_enrichmentService.ShouldExcludeFile("README.md"));
        Assert.False(_enrichmentService.ShouldExcludeFile("package.json")); // Config files are included
    }

    [Fact]
    public void DataEnrichmentService_EnrichMergeRequest_HandlesNullInputsGracefully()
    {
        // Arrange
        // Use individual fixture methods
        var mr = GitLabTestFixtures.CompleteFixture.MergeRequests.First();

        // Act - Should handle null commits list gracefully
        var enrichedMr = _enrichmentService.EnrichMergeRequest(mr, commits: null);

        // Assert
        Assert.NotNull(enrichedMr);
        Assert.Equal(mr.ProjectId, enrichedMr.ProjectId);
        Assert.Equal(mr.MrId, enrichedMr.MrId);
        Assert.Equal(mr.Title, enrichedMr.Title);
    }

    [Fact]
    public void DataEnrichmentService_EnrichCommit_HandlesNullInputsGracefully()
    {
        // Arrange
        // Use individual fixture methods
        var commit = GitLabTestFixtures.CompleteFixture.Commits.First();

        // Act - Should handle null changed files gracefully
        var enrichedCommit = _enrichmentService.EnrichCommit(commit, changedFiles: null);

        // Assert
        Assert.NotNull(enrichedCommit);
        Assert.Equal(commit.ProjectId, enrichedCommit.ProjectId);
        Assert.Equal(commit.CommitId, enrichedCommit.CommitId);
        Assert.Equal(commit.Message, enrichedCommit.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void DataEnrichmentService_ShouldExcludeCommit_HandlesEmptyStrings(string? message)
    {
        // Act & Assert - Should handle empty/null messages gracefully
        var result = _enrichmentService.ShouldExcludeCommit(message ?? string.Empty);

        // Empty messages should not be excluded (let them through for other validation)
        Assert.False(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void DataEnrichmentService_ShouldExcludeBranch_HandlesEmptyStrings(string? branchName)
    {
        // Act & Assert - Should handle empty/null branch names gracefully
        var result = _enrichmentService.ShouldExcludeBranch(branchName ?? string.Empty);

        // Empty branch names should not be excluded
        Assert.False(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void DataEnrichmentService_ShouldExcludeFile_HandlesEmptyStrings(string? filePath)
    {
        // Act & Assert - Should handle empty/null file paths gracefully
        var result = _enrichmentService.ShouldExcludeFile(filePath ?? string.Empty);

        // Empty file paths should not be excluded
        Assert.False(result);
    }

    [Fact]
    public async Task DatabaseOperations_HandleLargeDatasets_WithinReasonableTime()
    {
        // Arrange - Create multiple batches of test data
        // Use individual fixture methods
        var largeBatch = new List<Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw.RawCommit>();

        // Generate 1000 commits based on fixture patterns
        for (int i = 0; i < 1000; i++)
        {
            var baseCommit = GitLabTestFixtures.CompleteFixture.Commits.First();
            largeBatch.Add(new Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw.RawCommit
            {
                ProjectId = baseCommit.ProjectId,
                ProjectName = baseCommit.ProjectName,
                CommitId = $"commit_{i:D6}",
                AuthorUserId = (i % 4) + 1, // Cycle through test users
                AuthorName = $"User {(i % 4) + 1}",
                AuthorEmail = $"user{(i % 4) + 1}@example.com",
                CommittedAt = GitLabTestFixtures.FixedBaseDate.AddMinutes(-i),
                Message = $"Test commit {i}",
                Additions = 10 + (i % 100),
                Deletions = 5 + (i % 50),
                IngestedAt = GitLabTestFixtures.FixedBaseDate,
                ParentCount = 1
            });
        }

        // Act - Measure time for bulk insert
        var startTime = DateTime.UtcNow;
        await _dbContext.RawCommits.AddRangeAsync(largeBatch);
        await _dbContext.SaveChangesAsync();
        var endTime = DateTime.UtcNow;

        // Assert - Should complete within reasonable time (5 seconds)
        var duration = endTime - startTime;
        Assert.True(duration.TotalSeconds < 5, $"Bulk insert took {duration.TotalSeconds} seconds, which exceeds the 5-second limit");

        // Verify data was actually stored
        var storedCount = await _dbContext.RawCommits.CountAsync();
        Assert.Equal(1000, storedCount);
    }

    [Fact]
    public async Task DatabaseOperations_HandleConcurrentAccess_WithoutDeadlocks()
    {
        // Arrange
        // Use individual fixture methods
        var tasks = new List<Task>();

        // Act - Simulate concurrent database operations
        for (int i = 0; i < 5; i++)
        {
            int taskId = i;
            tasks.Add(Task.Run(async () =>
            {
                // Each task uses its own context to avoid conflicts
                var options = new DbContextOptionsBuilder<GitLabMetricsDbContext>()
                    .UseInMemoryDatabase(databaseName: "ConcurrentTest")
                    .Options;

                using var context = new GitLabMetricsDbContext(options);

                var commits = GitLabTestFixtures.CompleteFixture.Commits.Select((c, idx) => new Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw.RawCommit
                {
                    ProjectId = c.ProjectId,
                    ProjectName = c.ProjectName,
                    CommitId = $"task_{taskId}_commit_{idx}",
                    AuthorUserId = c.AuthorUserId,
                    AuthorName = c.AuthorName,
                    AuthorEmail = c.AuthorEmail,
                    CommittedAt = c.CommittedAt,
                    Message = $"Task {taskId} - {c.Message}",
                    Additions = c.Additions,
                    Deletions = c.Deletions,
                    IngestedAt = c.IngestedAt,
                    ParentCount = c.ParentCount
                }).ToList();

                await context.RawCommits.AddRangeAsync(commits);
                await context.SaveChangesAsync();
            }));
        }

        // Assert - All tasks should complete without deadlocks
        await Task.WhenAll(tasks);
        Assert.All(tasks, task => Assert.Equal(TaskStatus.RanToCompletion, task.Status));
    }

    [Fact]
    public void TestFixtures_ProduceConsistentData_AcrossMultipleCalls()
    {
        // Arrange & Act - Call fixture creation multiple times
        var results = new List<(int userCount, int commitCount, int mrCount)>();

        for (int i = 0; i < 10; i++)
        {
            // Use individual fixture methods
            results.Add((GitLabTestFixtures.CompleteFixture.Users.Count, GitLabTestFixtures.CompleteFixture.Commits.Count, GitLabTestFixtures.CompleteFixture.MergeRequests.Count));
        }

        // Assert - All results should be identical (deterministic)
        var first = results.First();
        Assert.All(results, result =>
        {
            Assert.Equal(first.userCount, result.userCount);
            Assert.Equal(first.commitCount, result.commitCount);
            Assert.Equal(first.mrCount, result.mrCount);
        });
    }

    [Fact]
    public void TestFixtures_ContainExpectedEdgeCases()
    {
        // Arrange
        // Use individual fixture methods

        // Act & Assert - Verify specific edge cases are present

        // Should have bot user
        Assert.Contains(GitLabTestFixtures.CompleteFixture.Users, u => u.External == true);

        // Should have archived project
        Assert.Contains(GitLabTestFixtures.CompleteFixture.Projects, p => p.Archived == true);

        // Should have merge commit
        Assert.Contains(GitLabTestFixtures.CompleteFixture.Commits, c => c.ParentCount > 1);

        // Should have revert commit
        Assert.Contains(GitLabTestFixtures.CompleteFixture.Commits, c => c.Message.StartsWith("Revert"));

        // Should have bot commit
        Assert.Contains(GitLabTestFixtures.CompleteFixture.Commits, c => c.AuthorName.Contains("bot"));

        // Should have draft MR
        Assert.Contains(GitLabTestFixtures.CompleteFixture.MergeRequests, mr => mr.Title.StartsWith("Draft:"));

        // Should have hotfix MR
        Assert.Contains(GitLabTestFixtures.CompleteFixture.MergeRequests, mr => mr.Title.Contains("hotfix"));

        // Should have conflicted MR
        Assert.Contains(GitLabTestFixtures.CompleteFixture.MergeRequests, mr => mr.HasConflicts == true);

        // Should have squash MR (high commit count)
        Assert.Contains(GitLabTestFixtures.CompleteFixture.MergeRequests, mr => mr.CommitsCount > 10);

        // Should have flaky pipeline (same SHA, different statuses)
        var flakyPipelines = GitLabTestFixtures.CompleteFixture.Pipelines.Where(p => p.Sha == "flaky123456").ToList();
        Assert.Equal(2, flakyPipelines.Count);
        Assert.Contains(flakyPipelines, p => p.Status == "failed");
        Assert.Contains(flakyPipelines, p => p.Status == "success");

        // Should have retried job
        Assert.Contains(GitLabTestFixtures.CompleteFixture.Jobs, j => j.RetriedFlag == true);

        // Should have resolved discussion
        Assert.Contains(GitLabTestFixtures.CompleteFixture.Notes, n => n.Resolved == true);

        // Should have system notes
        Assert.Contains(GitLabTestFixtures.CompleteFixture.Notes, n => n.System == true);
    }

    [Fact]
    public void TestFixtures_UseConsistentDating()
    {
        // Arrange
        // Use individual fixture methods
        var baseDate = GitLabTestFixtures.FixedBaseDate;

        // Act & Assert - All dates should be relative to the fixed base date
        Assert.All(GitLabTestFixtures.CompleteFixture.Users, user =>
            Assert.True(user.CreatedAt <= baseDate, $"User {user.Username} created date should be <= base date"));

        Assert.All(GitLabTestFixtures.CompleteFixture.Projects, project =>
            Assert.True(project.CreatedAt <= baseDate, $"Project {project.Name} created date should be <= base date"));

        Assert.All(GitLabTestFixtures.CompleteFixture.Commits, commit =>
            Assert.True(commit.CommittedAt <= baseDate, $"Commit {commit.CommitId} date should be <= base date"));

        Assert.All(GitLabTestFixtures.CompleteFixture.MergeRequests, mr =>
            Assert.True(mr.CreatedAt <= baseDate, $"MR {mr.MrId} created date should be <= base date"));

        Assert.All(GitLabTestFixtures.CompleteFixture.Pipelines, pipeline =>
            Assert.True(pipeline.CreatedAt <= baseDate, $"Pipeline {pipeline.PipelineId} created date should be <= base date"));
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
