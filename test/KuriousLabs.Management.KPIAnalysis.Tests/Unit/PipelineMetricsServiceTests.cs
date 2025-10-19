using Microsoft.Extensions.Logging;

using Moq;

using KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure;
using KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;
using KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

namespace KuriousLabs.Management.KPIAnalysis.Tests.Unit;

/// <summary>
/// Unit tests for PipelineMetricsService.
/// </summary>
public sealed class PipelineMetricsServiceTests
{
    [Fact]
    public async Task CalculatePipelineMetricsAsync_WithInvalidProjectId_ThrowsException()
    {
        // Arrange
        const long projectId = 999;
        const int windowDays = 30;

        var mockGitLabClient = new Mock<IGitLabHttpClient>();

        mockGitLabClient
            .Setup(x => x.GetProjectByIdAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GitLabProject?)null);

        var logger = Mock.Of<ILogger<PipelineMetricsService>>();
        var service = new PipelineMetricsService(mockGitLabClient.Object, logger);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await service.CalculatePipelineMetricsAsync(projectId, windowDays, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task CalculatePipelineMetricsAsync_WithNoPipelines_ReturnsEmptyResult()
    {
        // Arrange
        const long projectId = 100;
        const int windowDays = 30;

        var project = new GitLabProject
        {
            Id = projectId,
            Name = "TestProject",
            DefaultBranch = "main"
        };

        var mockGitLabClient = new Mock<IGitLabHttpClient>();

        mockGitLabClient
            .Setup(x => x.GetProjectByIdAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

        mockGitLabClient
            .Setup(x => x.GetPipelinesAsync(projectId, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitLabPipeline>());

        var logger = Mock.Of<ILogger<PipelineMetricsService>>();
        var service = new PipelineMetricsService(mockGitLabClient.Object, logger);

        // Act
        var result = await service.CalculatePipelineMetricsAsync(projectId, windowDays, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(projectId, result.ProjectId);
        Assert.Equal("TestProject", result.ProjectName);
        Assert.Empty(result.FailedJobs);
        Assert.Null(result.PipelineRetryRate);
        Assert.Equal(0, result.RetriedPipelineCount);
        Assert.Equal(0, result.TotalPipelineCount);
        Assert.Null(result.PipelineWaitTimeP50Min);
        Assert.Null(result.PipelineWaitTimeP95Min);
        Assert.Equal(0, result.DeploymentFrequency);
        Assert.Empty(result.JobDurationTrends);
        Assert.Null(result.AverageCoveragePercent);
        Assert.Null(result.CoverageTrend);
    }

    [Fact]
    public async Task CalculatePipelineMetricsAsync_WithPipelines_CalculatesMetrics()
    {
        // Arrange
        const long projectId = 100;
        const int windowDays = 30;
        var now = DateTime.UtcNow;

        var project = new GitLabProject
        {
            Id = projectId,
            Name = "TestProject",
            DefaultBranch = "main"
        };

        // Create test pipelines
        var pipelines = new List<GitLabPipeline>
        {
            new()
            {
                Id = 1,
                ProjectId = projectId,
                Sha = "abc123",
                Ref = "main",
                Status = "success",
                CreatedAt = now.AddDays(-5),
                UpdatedAt = now.AddDays(-5).AddMinutes(10),
                StartedAt = now.AddDays(-5).AddMinutes(2),
                Coverage = "85.5"
            },
            new()
            {
                Id = 2,
                ProjectId = projectId,
                Sha = "def456",
                Ref = "feature/test",
                Status = "failed",
                CreatedAt = now.AddDays(-4),
                UpdatedAt = now.AddDays(-4).AddMinutes(8),
                StartedAt = now.AddDays(-4).AddMinutes(1),
                Coverage = "80.0"
            },
            new()
            {
                Id = 3,
                ProjectId = projectId,
                Sha = "abc123", // Retry of pipeline 1
                Ref = "main",
                Status = "success",
                CreatedAt = now.AddDays(-5).AddMinutes(15),
                UpdatedAt = now.AddDays(-5).AddMinutes(25),
                StartedAt = now.AddDays(-5).AddMinutes(17),
                Coverage = "86.0"
            }
        };

        // Create test jobs
        var jobsPipeline1 = new List<GitLabPipelineJob>
        {
            new()
            {
                Id = 1,
                Name = "build",
                Status = "success",
                Stage = "build",
                CreatedAt = now.AddDays(-5),
                StartedAt = now.AddDays(-5).AddMinutes(2),
                FinishedAt = now.AddDays(-5).AddMinutes(5),
                Duration = 180.0,
                QueuedDuration = 120.0,
                User = null,
                PipelineId = 1,
                ProjectId = projectId,
                Sha = "abc123",
                Ref = "main",
                AllowFailure = false,
                Tag = false
            },
            new()
            {
                Id = 2,
                Name = "test",
                Status = "success",
                Stage = "test",
                CreatedAt = now.AddDays(-5).AddMinutes(5),
                StartedAt = now.AddDays(-5).AddMinutes(6),
                FinishedAt = now.AddDays(-5).AddMinutes(10),
                Duration = 240.0,
                QueuedDuration = 60.0,
                User = null,
                PipelineId = 1,
                ProjectId = projectId,
                Sha = "abc123",
                Ref = "main",
                AllowFailure = false,
                Tag = false
            }
        };

        var jobsPipeline2 = new List<GitLabPipelineJob>
        {
            new()
            {
                Id = 3,
                Name = "build",
                Status = "success",
                Stage = "build",
                CreatedAt = now.AddDays(-4),
                StartedAt = now.AddDays(-4).AddMinutes(1),
                FinishedAt = now.AddDays(-4).AddMinutes(3),
                Duration = 120.0,
                QueuedDuration = 60.0,
                User = null,
                PipelineId = 2,
                ProjectId = projectId,
                Sha = "def456",
                Ref = "feature/test",
                AllowFailure = false,
                Tag = false
            },
            new()
            {
                Id = 4,
                Name = "test",
                Status = "failed",
                Stage = "test",
                CreatedAt = now.AddDays(-4).AddMinutes(3),
                StartedAt = now.AddDays(-4).AddMinutes(4),
                FinishedAt = now.AddDays(-4).AddMinutes(8),
                Duration = 240.0,
                QueuedDuration = 60.0,
                User = null,
                PipelineId = 2,
                ProjectId = projectId,
                Sha = "def456",
                Ref = "feature/test",
                AllowFailure = false,
                Tag = false
            }
        };

        var jobsPipeline3 = new List<GitLabPipelineJob>
        {
            new()
            {
                Id = 5,
                Name = "build",
                Status = "success",
                Stage = "build",
                CreatedAt = now.AddDays(-5).AddMinutes(15),
                StartedAt = now.AddDays(-5).AddMinutes(17),
                FinishedAt = now.AddDays(-5).AddMinutes(20),
                Duration = 180.0,
                QueuedDuration = 120.0,
                User = null,
                PipelineId = 3,
                ProjectId = projectId,
                Sha = "abc123",
                Ref = "main",
                AllowFailure = false,
                Tag = false
            }
        };

        var mockGitLabClient = new Mock<IGitLabHttpClient>();

        mockGitLabClient
            .Setup(x => x.GetProjectByIdAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

        mockGitLabClient
            .Setup(x => x.GetPipelinesAsync(projectId, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pipelines);

        mockGitLabClient
            .Setup(x => x.GetPipelineJobsAsync(projectId, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(jobsPipeline1);

        mockGitLabClient
            .Setup(x => x.GetPipelineJobsAsync(projectId, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(jobsPipeline2);

        mockGitLabClient
            .Setup(x => x.GetPipelineJobsAsync(projectId, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(jobsPipeline3);

        var logger = Mock.Of<ILogger<PipelineMetricsService>>();
        var service = new PipelineMetricsService(mockGitLabClient.Object, logger);

        // Act
        var result = await service.CalculatePipelineMetricsAsync(projectId, windowDays, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(projectId, result.ProjectId);
        Assert.Equal("TestProject", result.ProjectName);
        Assert.Equal(3, result.TotalPipelineCount);

        // Failed Job Rate - should have 1 failed job (test job in pipeline 2)
        Assert.Single(result.FailedJobs);
        Assert.Equal("test", result.FailedJobs[0].JobName);
        Assert.Equal(1, result.FailedJobs[0].FailureCount);
        Assert.Equal(2, result.FailedJobs[0].TotalRuns);

        // Pipeline Retry Rate - 1 retry out of 2 unique SHAs = 0.5
        Assert.NotNull(result.PipelineRetryRate);
        Assert.Equal(0.5m, result.PipelineRetryRate.Value);
        Assert.Equal(1, result.RetriedPipelineCount);

        // Pipeline Wait Time - should calculate P50 and P95
        Assert.NotNull(result.PipelineWaitTimeP50Min);
        Assert.NotNull(result.PipelineWaitTimeP95Min);
        Assert.Equal(3, result.PipelinesWithWaitTimeCount);

        // Deployment Frequency - 2 pipelines on main branch
        Assert.Equal(2, result.DeploymentFrequency);

        // Job Duration Trends - should have trends for jobs with multiple runs
        Assert.NotEmpty(result.JobDurationTrends);

        // Branch Type Metrics
        Assert.NotNull(result.BranchTypeMetrics.MainBranchSuccessRate);
        Assert.Equal(1.0m, result.BranchTypeMetrics.MainBranchSuccessRate.Value); // 2/2 success on main
        Assert.Equal(2, result.BranchTypeMetrics.MainBranchTotalCount);
        Assert.NotNull(result.BranchTypeMetrics.FeatureBranchSuccessRate);
        Assert.Equal(0.0m, result.BranchTypeMetrics.FeatureBranchSuccessRate.Value); // 0/1 success on feature
        Assert.Equal(1, result.BranchTypeMetrics.FeatureBranchTotalCount);

        // Coverage Trend
        Assert.NotNull(result.AverageCoveragePercent);
        Assert.Equal(3, result.PipelinesWithCoverageCount);
        // Trend is null since we need at least 4 pipelines to determine trend
        // The test has only 3 pipelines
        Assert.Null(result.CoverageTrend);
    }

    [Fact]
    public async Task CalculatePipelineMetricsAsync_WithInvalidWindowDays_ThrowsException()
    {
        // Arrange
        const long projectId = 100;
        const int windowDays = 0;

        var mockGitLabClient = new Mock<IGitLabHttpClient>();
        var logger = Mock.Of<ILogger<PipelineMetricsService>>();
        var service = new PipelineMetricsService(mockGitLabClient.Object, logger);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await service.CalculatePipelineMetricsAsync(projectId, windowDays, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task CalculatePipelineMetricsAsync_WithJobFetchFailure_HandlesGracefully()
    {
        // Arrange
        const long projectId = 100;
        const int windowDays = 30;
        var now = DateTime.UtcNow;

        var project = new GitLabProject
        {
            Id = projectId,
            Name = "TestProject",
            DefaultBranch = "main"
        };

        var pipelines = new List<GitLabPipeline>
        {
            new()
            {
                Id = 1,
                ProjectId = projectId,
                Sha = "abc123",
                Ref = "main",
                Status = "success",
                CreatedAt = now.AddDays(-5),
                UpdatedAt = now.AddDays(-5).AddMinutes(10)
            }
        };

        var mockGitLabClient = new Mock<IGitLabHttpClient>();

        mockGitLabClient
            .Setup(x => x.GetProjectByIdAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

        mockGitLabClient
            .Setup(x => x.GetPipelinesAsync(projectId, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pipelines);

        mockGitLabClient
            .Setup(x => x.GetPipelineJobsAsync(projectId, 1, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Failed to fetch jobs"));

        var logger = Mock.Of<ILogger<PipelineMetricsService>>();
        var service = new PipelineMetricsService(mockGitLabClient.Object, logger);

        // Act
        var result = await service.CalculatePipelineMetricsAsync(projectId, windowDays, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(projectId, result.ProjectId);
        Assert.Equal(1, result.TotalPipelineCount);
        // Jobs should be empty since fetch failed
        Assert.Empty(result.FailedJobs);
        Assert.Empty(result.JobDurationTrends);
    }

    [Fact]
    public async Task CalculatePipelineMetricsAsync_WithNoCoverage_ReturnsNullCoverage()
    {
        // Arrange
        const long projectId = 100;
        const int windowDays = 30;
        var now = DateTime.UtcNow;

        var project = new GitLabProject
        {
            Id = projectId,
            Name = "TestProject",
            DefaultBranch = "main"
        };

        var pipelines = new List<GitLabPipeline>
        {
            new()
            {
                Id = 1,
                ProjectId = projectId,
                Sha = "abc123",
                Ref = "main",
                Status = "success",
                CreatedAt = now.AddDays(-5),
                UpdatedAt = now.AddDays(-5).AddMinutes(10),
                Coverage = null // No coverage
            }
        };

        var mockGitLabClient = new Mock<IGitLabHttpClient>();

        mockGitLabClient
            .Setup(x => x.GetProjectByIdAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

        mockGitLabClient
            .Setup(x => x.GetPipelinesAsync(projectId, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pipelines);

        mockGitLabClient
            .Setup(x => x.GetPipelineJobsAsync(projectId, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitLabPipelineJob>());

        var logger = Mock.Of<ILogger<PipelineMetricsService>>();
        var service = new PipelineMetricsService(mockGitLabClient.Object, logger);

        // Act
        var result = await service.CalculatePipelineMetricsAsync(projectId, windowDays, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.AverageCoveragePercent);
        Assert.Null(result.CoverageTrend);
        Assert.Equal(0, result.PipelinesWithCoverageCount);
    }
}
