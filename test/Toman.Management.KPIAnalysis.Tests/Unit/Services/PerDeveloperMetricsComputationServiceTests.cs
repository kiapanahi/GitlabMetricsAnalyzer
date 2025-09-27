using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Toman.Management.KPIAnalysis.ApiService.Configuration;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Data;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

using Xunit;

namespace Toman.Management.KPIAnalysis.Tests.Unit.Services;

public sealed class PerDeveloperMetricsComputationServiceTests : IDisposable
{
    private readonly GitLabMetricsDbContext _dbContext;
    private readonly PerDeveloperMetricsComputationService _service;

    public PerDeveloperMetricsComputationServiceTests()
    {
        var options = new DbContextOptionsBuilder<GitLabMetricsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new GitLabMetricsDbContext(options);

        var metricsConfig = Options.Create(new MetricsConfiguration
        {
            Identity = new IdentityConfiguration { BotRegexPatterns = [] },
            Excludes = new ExclusionConfiguration
            {
                CommitPatterns = ["^Merge.*", ".*automatic.*"],
                BranchPatterns = ["^temp/.*", "^experimental/.*"],
                FilePatterns = []
            }
        });

        var identityService = new IdentityMappingService(metricsConfig);
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<PerDeveloperMetricsComputationService>();

        _service = new PerDeveloperMetricsComputationService(_dbContext, identityService, logger, metricsConfig);
    }

    [Fact]
    public void GetSupportedWindowDays_ReturnsExpectedWindows()
    {
        // Act
        var windows = _service.GetSupportedWindowDays();

        // Assert
        Assert.Equal([14, 28, 90], windows);
    }

    [Fact]
    public async Task ComputeMetricsAsync_WithValidDeveloper_ReturnsMetrics()
    {
        // Arrange
        const long developerId = 123;
        var endDate = DateTimeOffset.UtcNow;
        var windowStart = endDate.AddDays(-14);

        await SeedTestDataAsync(developerId, windowStart, endDate);

        var options = new MetricsComputationOptions
        {
            WindowDays = 14,
            EndDate = endDate,
            ApplyWinsorization = true,
            ApplyFileExclusions = true
        };

        // Act
        var result = await _service.ComputeMetricsAsync(developerId, options);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(developerId, result.DeveloperId);
        Assert.Equal(14, result.WindowDays);
        Assert.Equal(windowStart.Date, result.WindowStart.Date);
        Assert.Equal(endDate.Date, result.WindowEnd.Date);
        Assert.NotNull(result.Metrics);
        Assert.NotNull(result.Audit);
    }

    [Fact]
    public async Task ComputeMetricsAsync_WithInvalidWindowDays_ThrowsArgumentException()
    {
        // Arrange
        var options = new MetricsComputationOptions
        {
            WindowDays = 30, // Unsupported window
            EndDate = DateTimeOffset.UtcNow
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _service.ComputeMetricsAsync(123, options));
    }

    [Fact]
    public async Task ComputeMetricsAsync_WithNonExistentDeveloper_ThrowsArgumentException()
    {
        // Arrange
        var options = new MetricsComputationOptions
        {
            WindowDays = 14,
            EndDate = DateTimeOffset.UtcNow
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _service.ComputeMetricsAsync(999, options));
    }

    [Fact]
    public async Task ComputeMetricsAsync_WithMergedMRs_ComputesCycleTime()
    {
        // Arrange
        const long developerId = 456;
        var endDate = DateTimeOffset.UtcNow;
        var windowStart = endDate.AddDays(-14);

        // Add commits and merge requests with specific cycle times
        var commit = new RawCommit
        {
            ProjectId = 1,
            ProjectName = "Test Project",
            CommitId = "abc123",
            AuthorUserId = developerId,
            AuthorName = "Test Developer",
            AuthorEmail = "test@example.com",
            CommittedAt = windowStart.AddDays(1),
            Message = "Test commit",
            Additions = 10,
            Deletions = 5,
            IngestedAt = DateTimeOffset.UtcNow
        };

        var mergeRequest1 = new RawMergeRequest
        {
            ProjectId = 1,
            ProjectName = "Test Project",
            MrId = 1,
            AuthorUserId = developerId,
            AuthorName = "Test Developer",
            Title = "Test MR 1",
            CreatedAt = windowStart.AddDays(2),
            MergedAt = windowStart.AddDays(4), // 2-day cycle time
            State = "merged",
            SourceBranch = "feature/test1",
            TargetBranch = "main",
            IngestedAt = DateTimeOffset.UtcNow
        };

        var mergeRequest2 = new RawMergeRequest
        {
            ProjectId = 1,
            ProjectName = "Test Project", 
            MrId = 2,
            AuthorUserId = developerId,
            AuthorName = "Test Developer",
            Title = "Test MR 2",
            CreatedAt = windowStart.AddDays(3),
            MergedAt = windowStart.AddDays(7), // 4-day cycle time
            State = "merged",
            SourceBranch = "feature/test2",
            TargetBranch = "main",
            IngestedAt = DateTimeOffset.UtcNow
        };

        _dbContext.RawCommits.Add(commit);
        _dbContext.RawMergeRequests.AddRange(mergeRequest1, mergeRequest2);
        await _dbContext.SaveChangesAsync();

        var options = new MetricsComputationOptions
        {
            WindowDays = 14,
            EndDate = endDate
        };

        // Act
        var result = await _service.ComputeMetricsAsync(developerId, options);

        // Assert
        Assert.NotNull(result.Metrics.MrCycleTimeP50H);
        Assert.Equal(72m, result.Metrics.MrCycleTimeP50H); // Median of 48h and 96h = 72h
        Assert.True(result.Audit.HasMergeRequestData);
        Assert.Equal(2, result.Audit.TotalMergeRequests);
    }

    [Fact]
    public async Task ComputeMetricsAsync_WithPipelines_ComputesSuccessRate()
    {
        // Arrange
        const long developerId = 789;
        var endDate = DateTimeOffset.UtcNow;
        var windowStart = endDate.AddDays(-14);

        await SeedBasicDeveloperData(developerId, windowStart, endDate);

        // Add pipelines with different statuses
        var pipeline1 = new RawPipeline
        {
            ProjectId = 1,
            ProjectName = "Test Project",
            PipelineId = 1,
            Sha = "abc123",
            Ref = "main",
            Status = "success",
            AuthorUserId = developerId,
            AuthorName = "Test Developer",
            TriggerSource = "push",
            CreatedAt = windowStart.AddDays(1),
            UpdatedAt = windowStart.AddDays(1),
            DurationSec = 300,
            IngestedAt = DateTimeOffset.UtcNow
        };

        var pipeline2 = new RawPipeline
        {
            ProjectId = 1,
            ProjectName = "Test Project",
            PipelineId = 2,
            Sha = "def456",
            Ref = "main",
            Status = "failed",
            AuthorUserId = developerId,
            AuthorName = "Test Developer",
            TriggerSource = "push",
            CreatedAt = windowStart.AddDays(2),
            UpdatedAt = windowStart.AddDays(2),
            DurationSec = 150,
            IngestedAt = DateTimeOffset.UtcNow
        };

        var pipeline3 = new RawPipeline
        {
            ProjectId = 1,
            ProjectName = "Test Project",
            PipelineId = 3,
            Sha = "ghi789",
            Ref = "main",
            Status = "success",
            AuthorUserId = developerId,
            AuthorName = "Test Developer",
            TriggerSource = "push",
            CreatedAt = windowStart.AddDays(3),
            UpdatedAt = windowStart.AddDays(3),
            DurationSec = 200,
            IngestedAt = DateTimeOffset.UtcNow
        };

        _dbContext.RawPipelines.AddRange(pipeline1, pipeline2, pipeline3);
        await _dbContext.SaveChangesAsync();

        var options = new MetricsComputationOptions
        {
            WindowDays = 14,
            EndDate = endDate
        };

        // Act
        var result = await _service.ComputeMetricsAsync(developerId, options);

        // Assert
        Assert.NotNull(result.Metrics.PipelineSuccessRate);
        Assert.Equal(66.67m, Math.Round(result.Metrics.PipelineSuccessRate.Value, 2)); // 2/3 = 66.67%
        Assert.True(result.Audit.HasPipelineData);
        Assert.Equal(3, result.Audit.TotalPipelines);
    }

    [Fact]
    public async Task ComputeMetricsAsync_WithLowSampleSize_SetsLowFlags()
    {
        // Arrange
        const long developerId = 101;
        var endDate = DateTimeOffset.UtcNow;
        var windowStart = endDate.AddDays(-14);

        // Add minimal data (below MinSampleSize = 5)
        var commit = new RawCommit
        {
            ProjectId = 1,
            ProjectName = "Test Project",
            CommitId = "single123",
            AuthorUserId = developerId,
            AuthorName = "Low Activity Developer",
            AuthorEmail = "low@example.com",
            CommittedAt = windowStart.AddDays(1),
            Message = "Single commit",
            Additions = 5,
            Deletions = 2,
            IngestedAt = DateTimeOffset.UtcNow
        };

        _dbContext.RawCommits.Add(commit);
        await _dbContext.SaveChangesAsync();

        var options = new MetricsComputationOptions
        {
            WindowDays = 14,
            EndDate = endDate
        };

        // Act
        var result = await _service.ComputeMetricsAsync(developerId, options);

        // Assert
        Assert.True(result.Audit.LowCommitCount);
        Assert.True(result.Audit.LowMergeRequestCount);
        Assert.True(result.Audit.LowPipelineCount);
        Assert.False(result.Audit.HasSufficientData);
        Assert.Equal("Poor", result.Audit.DataQuality);
    }

    private async Task SeedTestDataAsync(long developerId, DateTimeOffset windowStart, DateTimeOffset windowEnd)
    {
        await SeedBasicDeveloperData(developerId, windowStart, windowEnd);

        // Add some merge requests
        var mr = new RawMergeRequest
        {
            ProjectId = 1,
            ProjectName = "Test Project",
            MrId = 1,
            AuthorUserId = developerId,
            AuthorName = "Test Developer",
            Title = "Test MR",
            CreatedAt = windowStart.AddDays(1),
            MergedAt = windowStart.AddDays(2),
            State = "merged",
            SourceBranch = "feature/test",
            TargetBranch = "main",
            IngestedAt = DateTimeOffset.UtcNow
        };

        _dbContext.RawMergeRequests.Add(mr);
        await _dbContext.SaveChangesAsync();
    }

    private async Task SeedBasicDeveloperData(long developerId, DateTimeOffset windowStart, DateTimeOffset windowEnd)
    {
        var commit = new RawCommit
        {
            ProjectId = 1,
            ProjectName = "Test Project",
            CommitId = "test123",
            AuthorUserId = developerId,
            AuthorName = "Test Developer",
            AuthorEmail = "test@example.com",
            CommittedAt = windowStart.AddDays(1),
            Message = "Test commit",
            Additions = 10,
            Deletions = 5,
            IngestedAt = DateTimeOffset.UtcNow
        };

        _dbContext.RawCommits.Add(commit);
        await _dbContext.SaveChangesAsync();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}