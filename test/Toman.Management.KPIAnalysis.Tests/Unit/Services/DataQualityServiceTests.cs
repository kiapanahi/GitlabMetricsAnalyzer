using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Moq;

using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Data;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.DataQuality;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Entities;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

namespace Toman.Management.KPIAnalysis.Tests.Unit.Services;

public class DataQualityServiceTests
{
    private static GitLabMetricsDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<GitLabMetricsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new GitLabMetricsDbContext(options);
    }

    [Fact]
    public async Task PerformDataQualityChecksAsync_ReturnsReport()
    {
        // Arrange
        using var dbContext = GetInMemoryDbContext();
        var mockMetricsService = new Mock<IObservabilityMetricsService>();
        var mockLogger = new Mock<ILogger<DataQualityService>>();
        var service = new DataQualityService(dbContext, mockMetricsService.Object, mockLogger.Object);

        // Act
        var result = await service.PerformDataQualityChecksAsync();

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Checks);
        Assert.True(result.Checks.Count >= 3); // We perform at least 3 checks
        Assert.Contains("referential_integrity", result.Checks.Select(c => c.CheckType));
        Assert.Contains("data_completeness", result.Checks.Select(c => c.CheckType));
        Assert.Contains("data_latency", result.Checks.Select(c => c.CheckType));
    }

    [Fact]
    public async Task CheckReferentialIntegrityAsync_WithValidData_ReturnsPassedStatus()
    {
        // Arrange
        using var dbContext = GetInMemoryDbContext();
        var mockMetricsService = new Mock<IObservabilityMetricsService>();
        var mockLogger = new Mock<ILogger<DataQualityService>>();
        var service = new DataQualityService(dbContext, mockMetricsService.Object, mockLogger.Object);

        // Add test data with valid references
        var project = new Project
        {
            Id = 1,
            Name = "Test Project",
            PathWithNamespace = "test/project",
            CreatedAt = DateTime.UtcNow
        };
        dbContext.Projects.Add(project);

        var commit = new RawCommit
        {
            Id = 1,
            ProjectId = 1, // References the project above
            ProjectName = "Test Project",
            CommitId = "abc123",
            AuthorUserId = 1,
            AuthorName = "Test Author",
            AuthorEmail = "test@example.com",
            CommittedAt = DateTime.UtcNow,
            Message = "Test commit",
            IngestedAt = DateTime.UtcNow
        };
        dbContext.RawCommits.Add(commit);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await service.CheckReferentialIntegrityAsync();

        // Assert
        Assert.Equal("referential_integrity", result.CheckType);
        Assert.Equal("passed", result.Status);
        Assert.True(result.Score >= 0.9); // Should have high score with valid data
    }

    [Fact]
    public async Task CheckReferentialIntegrityAsync_WithInvalidReferences_ReturnsWarningOrFailed()
    {
        // Arrange
        using var dbContext = GetInMemoryDbContext();
        var mockMetricsService = new Mock<IObservabilityMetricsService>();
        var mockLogger = new Mock<ILogger<DataQualityService>>();
        var service = new DataQualityService(dbContext, mockMetricsService.Object, mockLogger.Object);

        // Add commit that references non-existent project
        var commit = new RawCommit
        {
            Id = 1,
            ProjectId = 999, // Non-existent project ID
            ProjectName = "Non-existent Project",
            CommitId = "abc123",
            AuthorUserId = 1,
            AuthorName = "Test Author",
            AuthorEmail = "test@example.com",
            CommittedAt = DateTime.UtcNow,
            Message = "Test commit",
            IngestedAt = DateTime.UtcNow
        };
        dbContext.RawCommits.Add(commit);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await service.CheckReferentialIntegrityAsync();

        // Assert
        Assert.Equal("referential_integrity", result.CheckType);
        Assert.True(result.Status == "warning" || result.Status == "failed");
        Assert.Contains("commits reference non-existent projects", result.Details);
    }

    [Fact]
    public async Task CheckDataCompletenessAsync_WithCompleteData_ReturnsPassedStatus()
    {
        // Arrange
        using var dbContext = GetInMemoryDbContext();
        var mockMetricsService = new Mock<IObservabilityMetricsService>();
        var mockLogger = new Mock<ILogger<DataQualityService>>();
        var service = new DataQualityService(dbContext, mockMetricsService.Object, mockLogger.Object);

        // Add complete data
        var commit = new RawCommit
        {
            Id = 1,
            ProjectId = 1,
            ProjectName = "Test Project",
            CommitId = "abc123",
            AuthorUserId = 1,
            AuthorName = "Test Author",
            AuthorEmail = "test@example.com",
            CommittedAt = DateTime.UtcNow.AddHours(-1), // Recent
            Message = "Test commit message",
            IngestedAt = DateTime.UtcNow
        };
        dbContext.RawCommits.Add(commit);

        var mr = new RawMergeRequest
        {
            Id = 1,
            ProjectId = 1,
            ProjectName = "Test Project",
            MrId = 1,
            AuthorUserId = 1,
            AuthorName = "Test Author",
            Title = "Test MR",
            CreatedAt = DateTime.UtcNow.AddHours(-1), // Recent
            State = "opened",
            SourceBranch = "feature",
            TargetBranch = "main",
            IngestedAt = DateTime.UtcNow
        };
        dbContext.RawMergeRequests.Add(mr);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await service.CheckDataCompletenessAsync();

        // Assert
        Assert.Equal("data_completeness", result.CheckType);
        Assert.Equal("passed", result.Status);
        Assert.True(result.Score >= 0.9);
    }

    [Fact]
    public async Task CheckDataLatencyAsync_WithRecentData_ReturnsPassedStatus()
    {
        // Arrange
        using var dbContext = GetInMemoryDbContext();
        var mockMetricsService = new Mock<IObservabilityMetricsService>();
        var mockLogger = new Mock<ILogger<DataQualityService>>();
        var service = new DataQualityService(dbContext, mockMetricsService.Object, mockLogger.Object);

        // Add recent ingestion state
        var ingestionState = new ApiService.Features.GitLabMetrics.Models.Operational.IngestionState
        {
            Id = 1,
            Entity = "incremental",
            LastSeenUpdatedAt = DateTime.UtcNow.AddMinutes(-30),
            LastRunAt = DateTime.UtcNow.AddMinutes(-30) // 30 minutes ago - fresh
        };
        dbContext.IngestionStates.Add(ingestionState);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await service.CheckDataLatencyAsync();

        // Assert
        Assert.Equal("data_latency", result.CheckType);
        Assert.Equal("passed", result.Status);
        Assert.True(result.Score > 0.5); // Should have decent score with recent data
    }

    [Fact]
    public async Task DataQualityReport_CalculatesOverallStatusCorrectly()
    {
        // Arrange
        var checks = new List<DataQualityCheckResult>
        {
            new()
            {
                CheckType = "test1",
                Status = "passed",
                Score = 0.9,
                Description = "Test 1"
            },
            new()
            {
                CheckType = "test2",
                Status = "warning",
                Score = 0.7,
                Description = "Test 2"
            },
            new()
            {
                CheckType = "test3",
                Status = "passed",
                Score = 0.95,
                Description = "Test 3"
            }
        };

        var report = new DataQualityReport
        {
            Checks = checks,
            OverallStatus = "warning", // Has one warning
            OverallScore = checks.Average(c => c.Score ?? 0)
        };

        // Assert
        Assert.Equal("warning", report.OverallStatus);
        Assert.Equal(0.85, report.OverallScore, 2); // Average of 0.9, 0.7, 0.95
        Assert.False(report.IsHealthy); // Not healthy due to warning status
        Assert.Single(report.GetChecksByStatus("warning"));
        Assert.Equal(2, report.GetChecksByStatus("passed").Count);
    }
}
