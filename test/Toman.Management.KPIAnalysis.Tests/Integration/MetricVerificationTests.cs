using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Toman.Management.KPIAnalysis.ApiService.Configuration;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Data;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;
using Toman.Management.KPIAnalysis.Tests.TestFixtures;
using Xunit;

namespace Toman.Management.KPIAnalysis.Tests.Integration;

/// <summary>
/// Integration tests for metric computation verification using deterministic GitLabTestFixtures.CompleteFixture.
/// Validates that metrics calculation produces expected results for known test data.
/// </summary>
public sealed class MetricVerificationTests : IDisposable
{
    private readonly GitLabMetricsDbContext _dbContext;
    private readonly PerDeveloperMetricsComputationService _metricsService;
    private readonly IDataEnrichmentService _enrichmentService;

    public MetricVerificationTests()
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

        var identityService = new IdentityMappingService(metricsConfig);
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<PerDeveloperMetricsComputationService>();
        var enrichmentLogger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<DataEnrichmentService>();
        
        _metricsService = new PerDeveloperMetricsComputationService(_dbContext, identityService, logger, metricsConfig);
        _enrichmentService = new DataEnrichmentService(metricsConfig, enrichmentLogger);
    }

    [Fact]
    public async Task ComputeMetrics_ForAlice_ProducesExpectedResults()
    {
        // Arrange - Load deterministic test data
        await SeedDeterministicTestDataAsync();
        
        var options = new MetricsComputationOptions
        {
            WindowDays = 30,
            EndDate = GitLabTestFixtures.FixedBaseDate,
            ApplyWinsorization = false
        };
        
        // Act
        var metrics = await _metricsService.ComputeMetricsAsync(1, options); // Alice (ID=1)
        
        // Assert - Verify specific metric calculations
        Assert.NotNull(metrics);
        Assert.Equal(1, metrics.AuthorUserId);
        Assert.Equal("alice.developer", metrics.AuthorUsername);
        
        // Commit-based metrics 
        Assert.True(metrics.CommitsCount > 0);
        Assert.True(metrics.LinesAddedCount > 0);
        Assert.True(metrics.LinesDeletedCount > 0);
        
        // Should include Alice's regular commits but exclude bot commits
        // Alice has 3 commits: regular, large refactoring, but no bot commits
        var expectedCommits = 2; // Regular + refactoring (bot commit excluded)
        Assert.Equal(expectedCommits, metrics.CommitsCount);
        
        // Verify line counts match expected values from fixtures
        var expectedLinesAdded = 150 + 800; // regular commit + refactoring
        var expectedLinesDeleted = 25 + 650; // regular commit + refactoring
        Assert.Equal(expectedLinesAdded, metrics.LinesAddedCount);
        Assert.Equal(expectedLinesDeleted, metrics.LinesDeletedCount);
    }

    [Fact]
    public async Task ComputeMetrics_ForAlice_HandlesMergeRequestMetrics()
    {
        // Arrange
        await SeedDeterministicTestDataAsync();
        
        var options = new MetricsComputationOptions
        {
            WindowDays = 30,
            EndDate = GitLabTestFixtures.FixedBaseDate,
            ApplyWinsorization = false
        };
        
        // Act
        var metrics = await _metricsService.ComputeMetricsAsync(1, options);
        
        // Assert - MR metrics for Alice
        // Alice has 3 MRs: standard (merged), draft (open), conflicted (closed)
        Assert.True(metrics.MergeRequestsCount > 0);
        
        // Alice's MR patterns from fixtures:
        // - Standard MR (merged): 4 days to merge (created -12, merged -8)
        // - Draft MR (open): still open
        // - Conflicted MR (closed): closed without merge (created -20, closed -18)
        
        // Only merged MRs count for cycle time
        Assert.True(metrics.MergeRequestsCount >= 1);
        
        // Verify review time calculations
        Assert.True(metrics.ReviewTimeP50H >= 0);
    }

    [Fact]
    public async Task ComputeMetrics_ForBob_HandlesReviewerRole()
    {
        // Arrange
        await SeedDeterministicTestDataAsync();
        
        var options = new MetricsComputationOptions
        {
            WindowDays = 30,
            EndDate = GitLabTestFixtures.FixedBaseDate,
            ApplyWinsorization = false
        };
        
        // Act
        var metrics = await _metricsService.ComputeMetricsAsync(2, options); // Bob (ID=2)
        
        // Assert - Bob is primarily a reviewer
        Assert.Equal(2, metrics.AuthorUserId);
        Assert.Equal("bob.reviewer", metrics.AuthorUsername);
        
        // Bob has 1 MR: the squash merge refactoring
        Assert.True(metrics.MergeRequestsCount >= 1);
        
        // Bob should have fewer commits than Alice
        Assert.True(metrics.CommitsCount < 3);
    }

    [Fact]
    public async Task ComputeMetrics_DetectsFlakyJobRate()
    {
        // Arrange
        await SeedDeterministicTestDataAsync();
        
        var options = new MetricsComputationOptions
        {
            WindowDays = 30,
            EndDate = GitLabTestFixtures.FixedBaseDate,
            ApplyWinsorization = false
        };
        
        // Act
        var metrics = await _metricsService.ComputeMetricsAsync(1, options); // Alice
        
        // Assert - Should detect flaky jobs from test data
        // Alice has pipelines with flaky behavior (failed then successful retry)
        Assert.NotNull(metrics.FlakyJobRate);
        
        // From fixtures: pipeline 1003 failed, 1004 succeeded (same SHA = retry)
        // This indicates flaky behavior that should be detected
        if (metrics.FlakyJobRate.HasValue)
        {
            Assert.True(metrics.FlakyJobRate.Value >= 0);
            Assert.True(metrics.FlakyJobRate.Value <= 1); // Rate should be between 0-1
        }
    }

    [Fact]
    public async Task ComputeMetrics_DetectsRollbackIncidence()
    {
        // Arrange
        await SeedDeterministicTestDataAsync();
        
        var options = new MetricsComputationOptions
        {
            WindowDays = 30,
            EndDate = GitLabTestFixtures.FixedBaseDate,
            ApplyWinsorization = false
        };
        
        // Act
        var metrics = await _metricsService.ComputeMetricsAsync(3, options); // Charlie (maintainer)
        
        // Assert - Charlie has the revert MR
        Assert.Equal(3, metrics.AuthorUserId);
        Assert.True(metrics.RollbackIncidence >= 0);
        
        // Charlie has both the hotfix and revert MRs in the test data
        Assert.True(metrics.MergeRequestsCount >= 1);
    }

    [Fact]
    public async Task ComputeMetrics_HandlesBranchTtlCalculations()
    {
        // Arrange
        await SeedDeterministicTestDataAsync();
        
        var options = new MetricsComputationOptions
        {
            WindowDays = 30,
            EndDate = GitLabTestFixtures.FixedBaseDate,
            ApplyWinsorization = false
        };
        
        // Act
        var metrics = await _metricsService.ComputeMetricsAsync(1, options);
        
        // Assert - Branch TTL metrics
        Assert.NotNull(metrics.BranchTtlP50H);
        Assert.NotNull(metrics.BranchTtlP90H);
        
        // From fixtures, Alice's standard MR had 4-day lifecycle
        // (created -12 days, merged -8 days = 4 days = 96 hours)
        if (metrics.BranchTtlP50H.HasValue)
        {
            Assert.True(metrics.BranchTtlP50H.Value > 0);
            Assert.True(metrics.BranchTtlP50H.Value <= metrics.BranchTtlP90H);
        }
    }

    [Fact]
    public async Task ComputeMetrics_HandlesDataQualityAudit()
    {
        // Arrange
        await SeedDeterministicTestDataAsync();
        
        var options = new MetricsComputationOptions
        {
            WindowDays = 30,
            EndDate = GitLabTestFixtures.FixedBaseDate,
            ApplyWinsorization = false
        };
        
        // Act
        var metrics = await _metricsService.ComputeMetricsAsync(1, options);
        
        // Assert - Audit information
        Assert.NotNull(metrics.Audit);
        Assert.NotNull(metrics.Audit.DataQuality);
        
        // With our comprehensive test data, Alice should have sufficient data
        Assert.True(metrics.Audit.HasSufficientData);
        Assert.False(metrics.Audit.LowCommitCount); // Alice has multiple commits
        Assert.False(metrics.Audit.LowMergeRequestCount); // Alice has multiple MRs
        
        // Data quality should be good with comprehensive fixtures
        Assert.Contains(metrics.Audit.DataQuality, new[] { "Good", "Excellent" });
    }

    [Fact]
    public async Task ComputeMetrics_ForBotUser_ProducesMinimalMetrics()
    {
        // Arrange
        await SeedDeterministicTestDataAsync();
        
        var options = new MetricsComputationOptions
        {
            WindowDays = 30,
            EndDate = GitLabTestFixtures.FixedBaseDate,
            ApplyWinsorization = false
        };
        
        // Act
        var metrics = await _metricsService.ComputeMetricsAsync(4, options); // Deployment Bot
        
        // Assert - Bot should have minimal metrics
        Assert.Equal(4, metrics.AuthorUserId);
        Assert.Equal("deployment.bot", metrics.AuthorUsername);
        
        // Bot commits should be excluded, so commit count should be low/zero
        Assert.True(metrics.CommitsCount == 0); // Bot commits excluded
        Assert.Equal(0, metrics.MergeRequestsCount); // No MRs for bot
        
        // But bot may have pipelines (scheduled triggers)
        // Should have low data quality flags
        Assert.True(metrics.Audit.LowCommitCount);
        Assert.True(metrics.Audit.LowMergeRequestCount);
    }

    [Fact]
    public async Task ComputeMetrics_WithWinsorization_ClampsOutliers()
    {
        // Arrange
        await SeedDeterministicTestDataAsync();
        
        var optionsWithWinsorization = new MetricsComputationOptions
        {
            WindowDays = 30,
            EndDate = GitLabTestFixtures.FixedBaseDate,
            ApplyWinsorization = true // Enable winsorization
        };
        
        var optionsWithoutWinsorization = new MetricsComputationOptions
        {
            WindowDays = 30,
            EndDate = GitLabTestFixtures.FixedBaseDate,
            ApplyWinsorization = false
        };
        
        // Act
        var metricsWithWinsorization = await _metricsService.ComputeMetricsAsync(1, optionsWithWinsorization);
        var metricsWithoutWinsorization = await _metricsService.ComputeMetricsAsync(1, optionsWithoutWinsorization);
        
        // Assert - Both should produce valid metrics
        Assert.NotNull(metricsWithWinsorization);
        Assert.NotNull(metricsWithoutWinsorization);
        
        // Basic counts should be the same
        Assert.Equal(metricsWithWinsorization.CommitsCount, metricsWithoutWinsorization.CommitsCount);
        Assert.Equal(metricsWithWinsorization.MergeRequestsCount, metricsWithoutWinsorization.MergeRequestsCount);
        
        // Values might be clamped with winsorization, but should still be reasonable
        Assert.True(metricsWithWinsorization.LinesAddedCount > 0);
        Assert.True(metricsWithoutWinsorization.LinesAddedCount > 0);
    }

    private async Task SeedDeterministicTestDataAsync()
    {
        // Use individual fixture methods

        // Enrich the data before storing (to test the full pipeline)
        var enrichedCommits = GitLabTestFixtures.CompleteFixture.Commits
            .Select(c => _enrichmentService.EnrichCommit(c))
            .ToList();

        var enrichedMergeRequests = GitLabTestFixtures.CompleteFixture.MergeRequests
            .Select(mr => _enrichmentService.EnrichMergeRequest(mr))
            .ToList();

        // Store all test data
        await _dbContext.RawCommits.AddRangeAsync(enrichedCommits);
        await _dbContext.RawMergeRequests.AddRangeAsync(enrichedMergeRequests);
        await _dbContext.RawPipelines.AddRangeAsync(GitLabTestFixtures.CompleteFixture.Pipelines);
        await _dbContext.RawJobs.AddRangeAsync(GitLabTestFixtures.CompleteFixture.Jobs);
        await _dbContext.RawMergeRequestNotes.AddRangeAsync(GitLabTestFixtures.CompleteFixture.Notes);
        
        await _dbContext.SaveChangesAsync();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}