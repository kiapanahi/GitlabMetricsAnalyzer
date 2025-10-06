using Microsoft.Extensions.Logging;

using Moq;

using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

namespace Toman.Management.KPIAnalysis.Tests.Integration;

/// <summary>
/// Unit tests for PerDeveloperMetricsService to validate MR throughput calculation logic
/// </summary>
public sealed class PerDeveloperMetricsServiceTests
{
    [Fact]
    public async Task CalculateMrThroughputAsync_WithMergedMrs_ReturnsCorrectThroughput()
    {
        // Arrange
        var mockGitLabClient = new Mock<IGitLabHttpClient>();
        var logger = Mock.Of<ILogger<PerDeveloperMetricsService>>();

        var developerId = 1L;
        var windowDays = 7;

        // Setup user
        mockGitLabClient
            .Setup(x => x.GetUserByIdAsync(developerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GitLabUser
            {
                Id = developerId,
                Username = "testuser",
                Name = "Test User",
                Email = "test@example.com",
                State = "active"
            });

        // Setup contributed projects
        mockGitLabClient
            .Setup(x => x.GetUserContributedProjectsAsync(developerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitLabContributedProject>
            {
                new()
                {
                    Id = 1,
                    Name = "Test Project",
                    NameWithNamespace = "group/test-project"
                }
            });

        // Setup merge requests - 2 merged MRs in the window
        var now = DateTime.UtcNow;
        mockGitLabClient
            .Setup(x => x.GetMergeRequestsAsync(1L, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitLabMergeRequest>
            {
                new()
                {
                    Id = 1,
                    Iid = 1,
                    Title = "MR 1",
                    Author = new GitLabUser { Id = developerId, Username = "testuser" },
                    MergedAt = now.AddDays(-2),
                    CreatedAt = now.AddDays(-3),
                    UpdatedAt = now.AddDays(-2),
                    State = "merged"
                },
                new()
                {
                    Id = 2,
                    Iid = 2,
                    Title = "MR 2",
                    Author = new GitLabUser { Id = developerId, Username = "testuser" },
                    MergedAt = now.AddDays(-4),
                    CreatedAt = now.AddDays(-5),
                    UpdatedAt = now.AddDays(-4),
                    State = "merged"
                }
            });

        var service = new PerDeveloperMetricsService(mockGitLabClient.Object, logger);

        // Act
        var result = await service.CalculateMrThroughputAsync(developerId, windowDays, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(developerId, result.UserId);
        Assert.Equal("testuser", result.Username);
        Assert.Equal(windowDays, result.WindowDays);
        Assert.Equal(2, result.TotalMergedMrs);
        
        // Throughput = (2 MRs * 7 days) / 7 days = 2 MRs/week
        Assert.Equal(2, result.MrThroughputWk);
        
        Assert.Single(result.Projects);
        Assert.Equal(2, result.Projects[0].MergedMrCount);
    }

    [Fact]
    public async Task CalculateMrThroughputAsync_WithNoMergedMrs_ReturnsZeroThroughput()
    {
        // Arrange
        var mockGitLabClient = new Mock<IGitLabHttpClient>();
        var logger = Mock.Of<ILogger<PerDeveloperMetricsService>>();

        var developerId = 1L;
        var windowDays = 7;

        // Setup user
        mockGitLabClient
            .Setup(x => x.GetUserByIdAsync(developerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GitLabUser
            {
                Id = developerId,
                Username = "testuser",
                Name = "Test User",
                Email = "test@example.com",
                State = "active"
            });

        // Setup contributed projects
        mockGitLabClient
            .Setup(x => x.GetUserContributedProjectsAsync(developerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitLabContributedProject>
            {
                new()
                {
                    Id = 1,
                    Name = "Test Project",
                    NameWithNamespace = "group/test-project"
                }
            });

        // Setup merge requests - no merged MRs
        mockGitLabClient
            .Setup(x => x.GetMergeRequestsAsync(1L, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitLabMergeRequest>());

        var service = new PerDeveloperMetricsService(mockGitLabClient.Object, logger);

        // Act
        var result = await service.CalculateMrThroughputAsync(developerId, windowDays, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.TotalMergedMrs);
        Assert.Equal(0, result.MrThroughputWk);
        Assert.Empty(result.Projects);
    }

    [Fact]
    public async Task CalculateMrThroughputAsync_WithMultipleProjects_AggregatesCorrectly()
    {
        // Arrange
        var mockGitLabClient = new Mock<IGitLabHttpClient>();
        var logger = Mock.Of<ILogger<PerDeveloperMetricsService>>();

        var developerId = 1L;
        var windowDays = 14;

        // Setup user
        mockGitLabClient
            .Setup(x => x.GetUserByIdAsync(developerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GitLabUser
            {
                Id = developerId,
                Username = "testuser",
                Name = "Test User",
                Email = "test@example.com",
                State = "active"
            });

        // Setup contributed projects - 2 projects
        mockGitLabClient
            .Setup(x => x.GetUserContributedProjectsAsync(developerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitLabContributedProject>
            {
                new() { Id = 1, Name = "Project 1", NameWithNamespace = "group/project1" },
                new() { Id = 2, Name = "Project 2", NameWithNamespace = "group/project2" }
            });

        var now = DateTime.UtcNow;
        
        // Setup merge requests for project 1 - 3 merged MRs
        mockGitLabClient
            .Setup(x => x.GetMergeRequestsAsync(1L, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitLabMergeRequest>
            {
                new()
                {
                    Id = 1,
                    Iid = 1,
                    Title = "MR 1",
                    Author = new GitLabUser { Id = developerId, Username = "testuser" },
                    MergedAt = now.AddDays(-2),
                    CreatedAt = now.AddDays(-3),
                    UpdatedAt = now.AddDays(-2),
                    State = "merged"
                },
                new()
                {
                    Id = 2,
                    Iid = 2,
                    Title = "MR 2",
                    Author = new GitLabUser { Id = developerId, Username = "testuser" },
                    MergedAt = now.AddDays(-5),
                    CreatedAt = now.AddDays(-6),
                    UpdatedAt = now.AddDays(-5),
                    State = "merged"
                },
                new()
                {
                    Id = 3,
                    Iid = 3,
                    Title = "MR 3",
                    Author = new GitLabUser { Id = developerId, Username = "testuser" },
                    MergedAt = now.AddDays(-8),
                    CreatedAt = now.AddDays(-9),
                    UpdatedAt = now.AddDays(-8),
                    State = "merged"
                }
            });

        // Setup merge requests for project 2 - 2 merged MRs
        mockGitLabClient
            .Setup(x => x.GetMergeRequestsAsync(2L, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitLabMergeRequest>
            {
                new()
                {
                    Id = 4,
                    Iid = 1,
                    Title = "MR 4",
                    Author = new GitLabUser { Id = developerId, Username = "testuser" },
                    MergedAt = now.AddDays(-3),
                    CreatedAt = now.AddDays(-4),
                    UpdatedAt = now.AddDays(-3),
                    State = "merged"
                },
                new()
                {
                    Id = 5,
                    Iid = 2,
                    Title = "MR 5",
                    Author = new GitLabUser { Id = developerId, Username = "testuser" },
                    MergedAt = now.AddDays(-10),
                    CreatedAt = now.AddDays(-11),
                    UpdatedAt = now.AddDays(-10),
                    State = "merged"
                }
            });

        var service = new PerDeveloperMetricsService(mockGitLabClient.Object, logger);

        // Act
        var result = await service.CalculateMrThroughputAsync(developerId, windowDays, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(5, result.TotalMergedMrs);
        
        // Throughput = (5 MRs * 7 days) / 14 days = 2.5 rounded to 2 MRs/week
        Assert.Equal(2, result.MrThroughputWk);
        
        Assert.Equal(2, result.Projects.Count);
        Assert.Contains(result.Projects, p => p.ProjectId == 1 && p.MergedMrCount == 3);
        Assert.Contains(result.Projects, p => p.ProjectId == 2 && p.MergedMrCount == 2);
    }

    [Fact]
    public async Task CalculateMrThroughputAsync_FiltersOutNonMergedMrs()
    {
        // Arrange
        var mockGitLabClient = new Mock<IGitLabHttpClient>();
        var logger = Mock.Of<ILogger<PerDeveloperMetricsService>>();

        var developerId = 1L;
        var windowDays = 7;

        // Setup user
        mockGitLabClient
            .Setup(x => x.GetUserByIdAsync(developerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GitLabUser
            {
                Id = developerId,
                Username = "testuser",
                Name = "Test User",
                Email = "test@example.com",
                State = "active"
            });

        // Setup contributed projects
        mockGitLabClient
            .Setup(x => x.GetUserContributedProjectsAsync(developerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitLabContributedProject>
            {
                new() { Id = 1, Name = "Test Project", NameWithNamespace = "group/test-project" }
            });

        var now = DateTime.UtcNow;
        
        // Setup merge requests - mix of merged and open MRs
        mockGitLabClient
            .Setup(x => x.GetMergeRequestsAsync(1L, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitLabMergeRequest>
            {
                new()
                {
                    Id = 1,
                    Iid = 1,
                    Title = "Merged MR",
                    Author = new GitLabUser { Id = developerId, Username = "testuser" },
                    MergedAt = now.AddDays(-2),
                    CreatedAt = now.AddDays(-3),
                    UpdatedAt = now.AddDays(-2),
                    State = "merged"
                },
                new()
                {
                    Id = 2,
                    Iid = 2,
                    Title = "Open MR",
                    Author = new GitLabUser { Id = developerId, Username = "testuser" },
                    MergedAt = null,
                    CreatedAt = now.AddDays(-1),
                    UpdatedAt = now,
                    State = "opened"
                },
                new()
                {
                    Id = 3,
                    Iid = 3,
                    Title = "Closed MR",
                    Author = new GitLabUser { Id = developerId, Username = "testuser" },
                    MergedAt = null,
                    CreatedAt = now.AddDays(-5),
                    UpdatedAt = now.AddDays(-4),
                    State = "closed"
                }
            });

        var service = new PerDeveloperMetricsService(mockGitLabClient.Object, logger);

        // Act
        var result = await service.CalculateMrThroughputAsync(developerId, windowDays, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.TotalMergedMrs); // Only the merged MR counts
        Assert.Equal(1, result.MrThroughputWk);
    }

    [Fact]
    public async Task CalculateMrThroughputAsync_FiltersOutMrsFromOtherAuthors()
    {
        // Arrange
        var mockGitLabClient = new Mock<IGitLabHttpClient>();
        var logger = Mock.Of<ILogger<PerDeveloperMetricsService>>();

        var developerId = 1L;
        var otherDeveloperId = 2L;
        var windowDays = 7;

        // Setup user
        mockGitLabClient
            .Setup(x => x.GetUserByIdAsync(developerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GitLabUser
            {
                Id = developerId,
                Username = "testuser",
                Name = "Test User",
                Email = "test@example.com",
                State = "active"
            });

        // Setup contributed projects
        mockGitLabClient
            .Setup(x => x.GetUserContributedProjectsAsync(developerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitLabContributedProject>
            {
                new() { Id = 1, Name = "Test Project", NameWithNamespace = "group/test-project" }
            });

        var now = DateTime.UtcNow;
        
        // Setup merge requests - mix of MRs from different authors
        mockGitLabClient
            .Setup(x => x.GetMergeRequestsAsync(1L, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitLabMergeRequest>
            {
                new()
                {
                    Id = 1,
                    Iid = 1,
                    Title = "My MR",
                    Author = new GitLabUser { Id = developerId, Username = "testuser" },
                    MergedAt = now.AddDays(-2),
                    CreatedAt = now.AddDays(-3),
                    UpdatedAt = now.AddDays(-2),
                    State = "merged"
                },
                new()
                {
                    Id = 2,
                    Iid = 2,
                    Title = "Other's MR",
                    Author = new GitLabUser { Id = otherDeveloperId, Username = "otheruser" },
                    MergedAt = now.AddDays(-1),
                    CreatedAt = now.AddDays(-2),
                    UpdatedAt = now.AddDays(-1),
                    State = "merged"
                }
            });

        var service = new PerDeveloperMetricsService(mockGitLabClient.Object, logger);

        // Act
        var result = await service.CalculateMrThroughputAsync(developerId, windowDays, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.TotalMergedMrs); // Only MRs from the target developer
        Assert.Equal(1, result.MrThroughputWk);
    }

    [Fact]
    public async Task CalculateMrThroughputAsync_WithInvalidWindowDays_ThrowsArgumentException()
    {
        // Arrange
        var mockGitLabClient = new Mock<IGitLabHttpClient>();
        var logger = Mock.Of<ILogger<PerDeveloperMetricsService>>();
        var service = new PerDeveloperMetricsService(mockGitLabClient.Object, logger);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CalculateMrThroughputAsync(1L, 0, TestContext.Current.CancellationToken));
        
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CalculateMrThroughputAsync(1L, -5, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CalculateMrThroughputAsync_WithNonExistentUser_ThrowsInvalidOperationException()
    {
        // Arrange
        var mockGitLabClient = new Mock<IGitLabHttpClient>();
        var logger = Mock.Of<ILogger<PerDeveloperMetricsService>>();

        mockGitLabClient
            .Setup(x => x.GetUserByIdAsync(999L, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GitLabUser?)null);

        var service = new PerDeveloperMetricsService(mockGitLabClient.Object, logger);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CalculateMrThroughputAsync(999L, 7, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CalculateMrThroughputAsync_WithNoContributedProjects_ReturnsZeroThroughput()
    {
        // Arrange
        var mockGitLabClient = new Mock<IGitLabHttpClient>();
        var logger = Mock.Of<ILogger<PerDeveloperMetricsService>>();

        var developerId = 1L;
        var windowDays = 7;

        // Setup user
        mockGitLabClient
            .Setup(x => x.GetUserByIdAsync(developerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GitLabUser
            {
                Id = developerId,
                Username = "testuser",
                Name = "Test User",
                Email = "test@example.com",
                State = "active"
            });

        // Setup no contributed projects
        mockGitLabClient
            .Setup(x => x.GetUserContributedProjectsAsync(developerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitLabContributedProject>());

        var service = new PerDeveloperMetricsService(mockGitLabClient.Object, logger);

        // Act
        var result = await service.CalculateMrThroughputAsync(developerId, windowDays, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.TotalMergedMrs);
        Assert.Equal(0, result.MrThroughputWk);
        Assert.Empty(result.Projects);
    }
}
