using Microsoft.Extensions.Logging;

using Moq;

using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

namespace Toman.Management.KPIAnalysis.Tests.Unit;

/// <summary>
/// Unit tests for PerDeveloperMetricsService
/// </summary>
public sealed class PerDeveloperMetricsServiceTests
{
    [Fact]
    public async Task GetPipelineSuccessRateAsync_WithNoPipelines_ReturnsNullRate()
    {
        // Arrange
        var mockGitLabService = new Mock<IGitLabService>();
        var logger = Mock.Of<ILogger<PerDeveloperMetricsService>>();
        var service = new PerDeveloperMetricsService(mockGitLabService.Object, logger);

        var userId = 1L;
        var user = new GitLabUser
        {
            Id = userId,
            Username = "testuser",
            Name = "Test User",
            Email = "test@example.com",
            State = "active"
        };

        mockGitLabService.Setup(x => x.GetUserByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        mockGitLabService.Setup(x => x.GetUserContributedProjectsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitLabProject>().AsReadOnly());

        // Act
        var result = await service.GetPipelineSuccessRateAsync(userId, 30, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(userId, result.UserId);
        Assert.Equal("testuser", result.Username);
        Assert.Equal(0, result.TotalPipelines);
        Assert.Null(result.PipelineSuccessRate);
    }

    [Fact]
    public async Task GetPipelineSuccessRateAsync_WithAllSuccessfulPipelines_Returns100Percent()
    {
        // Arrange
        var mockGitLabService = new Mock<IGitLabService>();
        var logger = Mock.Of<ILogger<PerDeveloperMetricsService>>();
        var service = new PerDeveloperMetricsService(mockGitLabService.Object, logger);

        var userId = 1L;
        var projectId = 100L;
        var user = new GitLabUser
        {
            Id = userId,
            Username = "testuser",
            Name = "Test User",
            Email = "test@example.com",
            State = "active"
        };

        var project = new GitLabProject
        {
            Id = projectId,
            Name = "Test Project",
            NameWithNamespace = "group/test-project",
            DefaultBranch = "main",
            Visibility = "private"
        };

        var pipelines = new List<RawPipeline>
        {
            new RawPipeline
            {
                ProjectId = projectId,
                ProjectName = "Test Project",
                PipelineId = 1,
                Sha = "abc123",
                Ref = "main",
                Status = "success",
                AuthorUserId = userId,
                AuthorName = "Test User",
                TriggerSource = "push",
                CreatedAt = DateTime.UtcNow.AddDays(-5),
                UpdatedAt = DateTime.UtcNow.AddDays(-5),
                IngestedAt = DateTime.UtcNow
            },
            new RawPipeline
            {
                ProjectId = projectId,
                ProjectName = "Test Project",
                PipelineId = 2,
                Sha = "def456",
                Ref = "main",
                Status = "success",
                AuthorUserId = userId,
                AuthorName = "Test User",
                TriggerSource = "push",
                CreatedAt = DateTime.UtcNow.AddDays(-3),
                UpdatedAt = DateTime.UtcNow.AddDays(-3),
                IngestedAt = DateTime.UtcNow
            }
        };

        mockGitLabService.Setup(x => x.GetUserByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        mockGitLabService.Setup(x => x.GetUserContributedProjectsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitLabProject> { project }.AsReadOnly());

        mockGitLabService.Setup(x => x.GetPipelinesAsync(projectId, It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pipelines.AsReadOnly());

        // Act
        var result = await service.GetPipelineSuccessRateAsync(userId, 30, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(2, result.TotalPipelines);
        Assert.Equal(2, result.SuccessfulPipelines);
        Assert.Equal(0, result.FailedPipelines);
        Assert.NotNull(result.PipelineSuccessRate);
        Assert.Equal(1.0m, result.PipelineSuccessRate.Value);
        Assert.Single(result.Projects);
        Assert.Equal(projectId, result.Projects[0].ProjectId);
    }

    [Fact]
    public async Task GetPipelineSuccessRateAsync_WithMixedStatuses_CalculatesCorrectRate()
    {
        // Arrange
        var mockGitLabService = new Mock<IGitLabService>();
        var logger = Mock.Of<ILogger<PerDeveloperMetricsService>>();
        var service = new PerDeveloperMetricsService(mockGitLabService.Object, logger);

        var userId = 1L;
        var projectId = 100L;
        var user = new GitLabUser
        {
            Id = userId,
            Username = "testuser",
            Name = "Test User",
            Email = "test@example.com",
            State = "active"
        };

        var project = new GitLabProject
        {
            Id = projectId,
            Name = "Test Project",
            NameWithNamespace = "group/test-project",
            DefaultBranch = "main",
            Visibility = "private"
        };

        var pipelines = new List<RawPipeline>
        {
            new RawPipeline
            {
                ProjectId = projectId,
                ProjectName = "Test Project",
                PipelineId = 1,
                Sha = "abc123",
                Ref = "main",
                Status = "success",
                AuthorUserId = userId,
                AuthorName = "Test User",
                TriggerSource = "push",
                CreatedAt = DateTime.UtcNow.AddDays(-5),
                UpdatedAt = DateTime.UtcNow.AddDays(-5),
                IngestedAt = DateTime.UtcNow
            },
            new RawPipeline
            {
                ProjectId = projectId,
                ProjectName = "Test Project",
                PipelineId = 2,
                Sha = "def456",
                Ref = "main",
                Status = "failed",
                AuthorUserId = userId,
                AuthorName = "Test User",
                TriggerSource = "push",
                CreatedAt = DateTime.UtcNow.AddDays(-3),
                UpdatedAt = DateTime.UtcNow.AddDays(-3),
                IngestedAt = DateTime.UtcNow
            },
            new RawPipeline
            {
                ProjectId = projectId,
                ProjectName = "Test Project",
                PipelineId = 3,
                Sha = "ghi789",
                Ref = "main",
                Status = "success",
                AuthorUserId = userId,
                AuthorName = "Test User",
                TriggerSource = "push",
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                UpdatedAt = DateTime.UtcNow.AddDays(-1),
                IngestedAt = DateTime.UtcNow
            }
        };

        mockGitLabService.Setup(x => x.GetUserByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        mockGitLabService.Setup(x => x.GetUserContributedProjectsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitLabProject> { project }.AsReadOnly());

        mockGitLabService.Setup(x => x.GetPipelinesAsync(projectId, It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pipelines.AsReadOnly());

        // Act
        var result = await service.GetPipelineSuccessRateAsync(userId, 30, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.TotalPipelines);
        Assert.Equal(2, result.SuccessfulPipelines);
        Assert.Equal(1, result.FailedPipelines);
        Assert.Equal(0, result.OtherStatusPipelines);
        Assert.NotNull(result.PipelineSuccessRate);
        Assert.Equal(0.67m, Math.Round(result.PipelineSuccessRate.Value, 2));
    }

    [Fact]
    public async Task GetPipelineSuccessRateAsync_WithUserNotFound_ThrowsException()
    {
        // Arrange
        var mockGitLabService = new Mock<IGitLabService>();
        var logger = Mock.Of<ILogger<PerDeveloperMetricsService>>();
        var service = new PerDeveloperMetricsService(mockGitLabService.Object, logger);

        var userId = 999L;

        mockGitLabService.Setup(x => x.GetUserByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GitLabUser?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetPipelineSuccessRateAsync(userId, 30, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetPipelineSuccessRateAsync_WithInvalidLookbackDays_ThrowsException()
    {
        // Arrange
        var mockGitLabService = new Mock<IGitLabService>();
        var logger = Mock.Of<ILogger<PerDeveloperMetricsService>>();
        var service = new PerDeveloperMetricsService(mockGitLabService.Object, logger);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => service.GetPipelineSuccessRateAsync(1L, 0, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetPipelineSuccessRateAsync_FiltersOutOtherUsersPipelines()
    {
        // Arrange
        var mockGitLabService = new Mock<IGitLabService>();
        var logger = Mock.Of<ILogger<PerDeveloperMetricsService>>();
        var service = new PerDeveloperMetricsService(mockGitLabService.Object, logger);

        var userId = 1L;
        var otherUserId = 2L;
        var projectId = 100L;
        var user = new GitLabUser
        {
            Id = userId,
            Username = "testuser",
            Name = "Test User",
            Email = "test@example.com",
            State = "active"
        };

        var project = new GitLabProject
        {
            Id = projectId,
            Name = "Test Project",
            NameWithNamespace = "group/test-project",
            DefaultBranch = "main",
            Visibility = "private"
        };

        var pipelines = new List<RawPipeline>
        {
            new RawPipeline
            {
                ProjectId = projectId,
                ProjectName = "Test Project",
                PipelineId = 1,
                Sha = "abc123",
                Ref = "main",
                Status = "success",
                AuthorUserId = userId,
                AuthorName = "Test User",
                TriggerSource = "push",
                CreatedAt = DateTime.UtcNow.AddDays(-5),
                UpdatedAt = DateTime.UtcNow.AddDays(-5),
                IngestedAt = DateTime.UtcNow
            },
            new RawPipeline
            {
                ProjectId = projectId,
                ProjectName = "Test Project",
                PipelineId = 2,
                Sha = "def456",
                Ref = "main",
                Status = "success",
                AuthorUserId = otherUserId,
                AuthorName = "Other User",
                TriggerSource = "push",
                CreatedAt = DateTime.UtcNow.AddDays(-3),
                UpdatedAt = DateTime.UtcNow.AddDays(-3),
                IngestedAt = DateTime.UtcNow
            }
        };

        mockGitLabService.Setup(x => x.GetUserByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        mockGitLabService.Setup(x => x.GetUserContributedProjectsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitLabProject> { project }.AsReadOnly());

        mockGitLabService.Setup(x => x.GetPipelinesAsync(projectId, It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pipelines.AsReadOnly());

        // Act
        var result = await service.GetPipelineSuccessRateAsync(userId, 30, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.TotalPipelines); // Only the pipeline from the target user
        Assert.Equal(1, result.SuccessfulPipelines);
    }
}
