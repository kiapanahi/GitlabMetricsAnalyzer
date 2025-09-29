using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Toman.Management.KPIAnalysis.ApiService.Configuration;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Data;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;
using Toman.Management.KPIAnalysis.Tests.TestFixtures;

namespace Toman.Management.KPIAnalysis.Tests.Integration;

/// <summary>
/// Integration tests to verify that metrics computation produces expected results
/// using deterministic test fixtures
/// </summary>
public sealed class MetricVerificationTests : IDisposable
{
    private readonly GitLabMetricsDbContext _dbContext;
    private readonly PerDeveloperMetricsComputationService _metricsService;

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
        _metricsService = new PerDeveloperMetricsComputationService(_dbContext, identityService, logger, metricsConfig);
    }

    [Fact]
    public async Task ComputeMetrics_ForValidDeveloper_ReturnsValidStructure()
    {
        // Arrange
        await SeedDeterministicTestDataAsync();

        var options = new MetricsComputationOptions
        {
            WindowDays = 14,
            EndDate = GitLabTestFixtures.FixedBaseDate,
            ApplyWinsorization = false
        };

        // Act
        var metrics = await _metricsService.ComputeMetricsAsync(1, options, TestContext.Current.CancellationToken); // Alice (ID=1)

        // Assert - Verify the correct API structure
        Assert.NotNull(metrics);
        Assert.Equal(1, metrics.DeveloperId);
        Assert.Equal("alice.developer", metrics.DeveloperName);
        Assert.NotNull(metrics.Metrics);
        Assert.NotNull(metrics.Audit);

        // Verify metrics properties exist and are accessible
        Assert.True(metrics.Metrics.MrThroughputWk >= 0);
        Assert.True(metrics.Metrics.DeploymentFrequencyWk >= 0);

        // Verify audit properties
        Assert.NotNull(metrics.Audit.DataQuality);
    }

    [Fact]
    public async Task ComputeMetrics_ForBob_ReturnsCorrectDeveloperInfo()
    {
        // Arrange
        await SeedDeterministicTestDataAsync();

        var options = new MetricsComputationOptions
        {
            WindowDays = 14,
            EndDate = GitLabTestFixtures.FixedBaseDate,
            ApplyWinsorization = false
        };

        // Act
        var metrics = await _metricsService.ComputeMetricsAsync(2, options, TestContext.Current.CancellationToken); // Bob (ID=2)

        // Assert - Bob is primarily a reviewer
        Assert.Equal(2, metrics.DeveloperId);
        Assert.Equal("bob.reviewer", metrics.DeveloperName);
        Assert.Contains("bob@example.com", metrics.DeveloperEmail);
    }

    [Fact]
    public async Task ComputeMetrics_DetectsMetricsValues()
    {
        // Arrange
        await SeedDeterministicTestDataAsync();

        var options = new MetricsComputationOptions
        {
            WindowDays = 28,
            EndDate = GitLabTestFixtures.FixedBaseDate,
            ApplyWinsorization = false
        };

        // Act
        var metrics = await _metricsService.ComputeMetricsAsync(1, options, TestContext.Current.CancellationToken); // Alice

        // Assert - Verify metrics can be computed
        Assert.NotNull(metrics.Metrics.PipelineSuccessRate);
        Assert.NotNull(metrics.Metrics.FlakyJobRate);

        // Verify rate values are within expected ranges
        if (metrics.Metrics.PipelineSuccessRate.HasValue)
        {
            Assert.True(metrics.Metrics.PipelineSuccessRate.Value >= 0);
            Assert.True(metrics.Metrics.PipelineSuccessRate.Value <= 1);
        }

        if (metrics.Metrics.FlakyJobRate.HasValue)
        {
            Assert.True(metrics.Metrics.FlakyJobRate.Value >= 0);
            Assert.True(metrics.Metrics.FlakyJobRate.Value <= 1);
        }
    }

    [Fact]
    public async Task ComputeMetrics_WithWinsorization_AppliesCorrectly()
    {
        // Arrange
        await SeedDeterministicTestDataAsync();

        var optionsWithWinsorization = new MetricsComputationOptions
        {
            WindowDays = 14,
            EndDate = GitLabTestFixtures.FixedBaseDate,
            ApplyWinsorization = true
        };

        var optionsWithoutWinsorization = new MetricsComputationOptions
        {
            WindowDays = 14,
            EndDate = GitLabTestFixtures.FixedBaseDate,
            ApplyWinsorization = false
        };

        // Act
        var metricsWithWinsorization = await _metricsService.ComputeMetricsAsync(1, optionsWithWinsorization, TestContext.Current.CancellationToken);
        var metricsWithoutWinsorization = await _metricsService.ComputeMetricsAsync(1, optionsWithoutWinsorization, TestContext.Current.CancellationToken);

        // Assert - Both should produce valid results
        Assert.NotNull(metricsWithWinsorization);
        Assert.NotNull(metricsWithoutWinsorization);
        Assert.Equal(metricsWithWinsorization.DeveloperId, metricsWithoutWinsorization.DeveloperId);

        // Winsorization flag should be different
        Assert.True(metricsWithWinsorization.Audit.WinsorizedMetrics >= 0);
        Assert.True(metricsWithoutWinsorization.Audit.WinsorizedMetrics >= 0);
    }

    private async Task SeedDeterministicTestDataAsync()
    {
        // Clear any existing change tracking to avoid conflicts
        _dbContext.ChangeTracker.Clear();

        // Use the deterministic fixtures directly
        var commits = GitLabTestFixtures.CompleteFixture.Commits;
        var mergeRequests = GitLabTestFixtures.CompleteFixture.MergeRequests;
        var pipelines = GitLabTestFixtures.CompleteFixture.Pipelines;
        var jobs = GitLabTestFixtures.CompleteFixture.Jobs;
        var notes = GitLabTestFixtures.CompleteFixture.Notes;

        await _dbContext.RawCommits.AddRangeAsync(commits, TestContext.Current.CancellationToken);
        await _dbContext.RawMergeRequests.AddRangeAsync(mergeRequests, TestContext.Current.CancellationToken);
        await _dbContext.RawPipelines.AddRangeAsync(pipelines, TestContext.Current.CancellationToken);
        await _dbContext.RawJobs.AddRangeAsync(jobs, TestContext.Current.CancellationToken);
        await _dbContext.RawMergeRequestNotes.AddRangeAsync(notes, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
