using Microsoft.Extensions.Logging;

using Moq;

using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

namespace Toman.Management.KPIAnalysis.Tests.Unit;

/// <summary>
/// Unit tests for PerDeveloperMetricsService
/// </summary>
public sealed class PerDeveloperMetricsServiceTests
{
    [Fact]
    public async Task CalculateDeploymentFrequencyAsync_WithValidUser_ReturnsAnalysis()
    {
        // Arrange
        var userId = 1L;
        var windowDays = 30;

        var mockHttpClient = new Mock<IGitLabHttpClient>();
        var mockLogger = new Mock<ILogger<PerDeveloperMetricsService>>();

        var user = new GitLabUser
        {
            Id = userId,
            Username = "testuser",
            Email = "test@example.com",
            Name = "Test User"
        };

        var contributedProjects = new List<GitLabContributedProject>
        {
            new()
            {
                Id = 100,
                Name = "Test Project"
            }
        };

        var commits = new List<GitLabCommit>
        {
            new()
            {
                Id = "abc123",
                AuthorEmail = "test@example.com",
                Title = "Test commit"
            }
        };

        var pipelines = new List<GitLabPipeline>
        {
            new()
            {
                Id = 1,
                ProjectId = 100,
                Sha = "abc123",
                Ref = "main",
                Status = "success",
                CreatedAt = DateTime.UtcNow.AddDays(-5),
                UpdatedAt = DateTime.UtcNow.AddDays(-5)
            },
            new()
            {
                Id = 2,
                ProjectId = 100,
                Sha = "abc123",
                Ref = "main",
                Status = "success",
                CreatedAt = DateTime.UtcNow.AddDays(-10),
                UpdatedAt = DateTime.UtcNow.AddDays(-10)
            }
        };

        mockHttpClient.Setup(x => x.GetUserByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        mockHttpClient.Setup(x => x.GetUserContributedProjectsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contributedProjects);

        mockHttpClient.Setup(x => x.GetCommitsAsync(100, It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(commits);

        mockHttpClient.Setup(x => x.GetPipelinesAsync(100, It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pipelines);

        var service = new PerDeveloperMetricsService(mockHttpClient.Object, mockLogger.Object);

        // Act
        var result = await service.CalculateDeploymentFrequencyAsync(userId, windowDays, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(userId, result.UserId);
        Assert.Equal("testuser", result.Username);
        Assert.Equal(windowDays, result.WindowDays);
        Assert.Equal(2, result.TotalDeployments);
        // With 2 deployments in 30 days: (2 * 7 / 30) = 0.467 = 0 when cast to int
        Assert.Equal(0, result.DeploymentFrequencyWk);
        Assert.Single(result.Projects);
        Assert.Equal("Test Project", result.Projects[0].ProjectName);
        Assert.Equal(2, result.Projects[0].DeploymentCount);
    }

    [Fact]
    public async Task CalculateDeploymentFrequencyAsync_WithNoContributedProjects_ReturnsEmptyAnalysis()
    {
        // Arrange
        var userId = 1L;
        var windowDays = 30;

        var mockHttpClient = new Mock<IGitLabHttpClient>();
        var mockLogger = new Mock<ILogger<PerDeveloperMetricsService>>();

        var user = new GitLabUser
        {
            Id = userId,
            Username = "testuser",
            Email = "test@example.com",
            Name = "Test User"
        };

        mockHttpClient.Setup(x => x.GetUserByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        mockHttpClient.Setup(x => x.GetUserContributedProjectsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitLabContributedProject>());

        var service = new PerDeveloperMetricsService(mockHttpClient.Object, mockLogger.Object);

        // Act
        var result = await service.CalculateDeploymentFrequencyAsync(userId, windowDays, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(0, result.TotalDeployments);
        Assert.Equal(0, result.DeploymentFrequencyWk);
        Assert.Empty(result.Projects);
    }

    [Fact]
    public async Task CalculateDeploymentFrequencyAsync_WithInvalidUserId_ThrowsException()
    {
        // Arrange
        var userId = 999L;
        var windowDays = 30;

        var mockHttpClient = new Mock<IGitLabHttpClient>();
        var mockLogger = new Mock<ILogger<PerDeveloperMetricsService>>();

        mockHttpClient.Setup(x => x.GetUserByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GitLabUser?)null);

        var service = new PerDeveloperMetricsService(mockHttpClient.Object, mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await service.CalculateDeploymentFrequencyAsync(userId, windowDays, CancellationToken.None)
        );
    }

    [Fact]
    public async Task CalculateDeploymentFrequencyAsync_WithInvalidWindowDays_ThrowsException()
    {
        // Arrange
        var userId = 1L;
        var windowDays = 0;

        var mockHttpClient = new Mock<IGitLabHttpClient>();
        var mockLogger = new Mock<ILogger<PerDeveloperMetricsService>>();

        var service = new PerDeveloperMetricsService(mockHttpClient.Object, mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await service.CalculateDeploymentFrequencyAsync(userId, windowDays, CancellationToken.None)
        );
    }

    [Fact]
    public async Task CalculateDeploymentFrequencyAsync_OnlyCountsSuccessfulMainBranchPipelines()
    {
        // Arrange
        var userId = 1L;
        var windowDays = 30;

        var mockHttpClient = new Mock<IGitLabHttpClient>();
        var mockLogger = new Mock<ILogger<PerDeveloperMetricsService>>();

        var user = new GitLabUser
        {
            Id = userId,
            Username = "testuser",
            Email = "test@example.com",
            Name = "Test User"
        };

        var contributedProjects = new List<GitLabContributedProject>
        {
            new()
            {
                Id = 100,
                Name = "Test Project"
            }
        };

        var commits = new List<GitLabCommit>
        {
            new()
            {
                Id = "abc123",
                AuthorEmail = "test@example.com",
                Title = "Test commit"
            }
        };

        var pipelines = new List<GitLabPipeline>
        {
            new()
            {
                Id = 1,
                ProjectId = 100,
                Sha = "abc123",
                Ref = "main",
                Status = "success",
                CreatedAt = DateTime.UtcNow.AddDays(-5),
                UpdatedAt = DateTime.UtcNow.AddDays(-5)
            },
            new()
            {
                Id = 2,
                ProjectId = 100,
                Sha = "abc123",
                Ref = "feature-branch",
                Status = "success",
                CreatedAt = DateTime.UtcNow.AddDays(-6),
                UpdatedAt = DateTime.UtcNow.AddDays(-6)
            },
            new()
            {
                Id = 3,
                ProjectId = 100,
                Sha = "abc123",
                Ref = "main",
                Status = "failed",
                CreatedAt = DateTime.UtcNow.AddDays(-7),
                UpdatedAt = DateTime.UtcNow.AddDays(-7)
            }
        };

        mockHttpClient.Setup(x => x.GetUserByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        mockHttpClient.Setup(x => x.GetUserContributedProjectsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contributedProjects);

        mockHttpClient.Setup(x => x.GetCommitsAsync(100, It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(commits);

        mockHttpClient.Setup(x => x.GetPipelinesAsync(100, It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pipelines);

        var service = new PerDeveloperMetricsService(mockHttpClient.Object, mockLogger.Object);

        // Act
        var result = await service.CalculateDeploymentFrequencyAsync(userId, windowDays, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.TotalDeployments); // Only the successful main branch pipeline
    }
}
