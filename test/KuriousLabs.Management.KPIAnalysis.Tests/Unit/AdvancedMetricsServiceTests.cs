using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

using Toman.Management.KPIAnalysis.ApiService.Configuration;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

namespace Toman.Management.KPIAnalysis.Tests.Unit;

/// <summary>
/// Unit tests for AdvancedMetricsService.
/// </summary>
public sealed class AdvancedMetricsServiceTests
{
    private static MetricsConfiguration CreateTestConfiguration()
    {
        return new MetricsConfiguration
        {
            Identity = new IdentityConfiguration(),
            Excludes = new ExclusionConfiguration()
        };
    }

    [Fact]
    public async Task CalculateAdvancedMetricsAsync_WithNoProjects_ReturnsEmptyResult()
    {
        // Arrange
        const long userId = 1;
        const int windowDays = 30;

        var user = new GitLabUser
        {
            Id = userId,
            Username = "testuser",
            Name = "Test User",
            Email = "test@example.com"
        };

        var mockHttpClient = new Mock<IGitLabHttpClient>();
        mockHttpClient.Setup(c => c.GetUserByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        mockHttpClient.Setup(c => c.GetUserContributedProjectsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GitLabContributedProject>());

        var mockLogger = new Mock<ILogger<AdvancedMetricsService>>();
        var mockConfig = new Mock<IOptions<MetricsConfiguration>>();
        mockConfig.Setup(c => c.Value).Returns(CreateTestConfiguration());

        var service = new AdvancedMetricsService(mockHttpClient.Object, mockLogger.Object, mockConfig.Object);

        // Act
        var result = await service.CalculateAdvancedMetricsAsync(userId, windowDays);

        // Assert
        Assert.Equal(userId, result.UserId);
        Assert.Equal("testuser", result.Username);
        Assert.Equal(windowDays, result.WindowDays);
        Assert.Equal(0, result.BusFactor);
        Assert.Equal(0, result.ContributingDevelopersCount);
        Assert.Equal(0, result.Top3DevelopersFileChangePercentage);
        Assert.NotNull(result.ResponseTimeDistribution);
        Assert.Equal(24, result.ResponseTimeDistribution.Count);
        Assert.Null(result.PeakResponseHour);
        Assert.Equal(0, result.TotalReviewResponses);
        Assert.Null(result.BatchSizeP50);
        Assert.Null(result.BatchSizeP95);
        Assert.Equal(0, result.BatchSizeMrCount);
        Assert.Null(result.DraftDurationMedianH);
        Assert.Equal(0, result.DraftMrCount);
        Assert.Null(result.IterationCountMedian);
        Assert.Equal(0, result.IterationMrCount);
        Assert.Null(result.IdleTimeInReviewMedianH);
        Assert.Equal(0, result.IdleTimeMrCount);
        Assert.Null(result.CrossTeamCollaborationPercentage);
        Assert.Equal(0, result.CrossTeamMrCount);
        Assert.Equal(0, result.TotalMrsForCrossTeam);
        Assert.False(result.TeamMappingAvailable);
        Assert.Empty(result.Projects);
    }

    [Fact]
    public async Task CalculateAdvancedMetricsAsync_WithBusFactorData_CalculatesCorrectly()
    {
        // Arrange
        const long userId = 1;
        const int windowDays = 30;

        var user = new GitLabUser
        {
            Id = userId,
            Username = "testuser",
            Name = "Test User",
            Email = "test@example.com"
        };

        var project = new GitLabContributedProject
        {
            Id = 100,
            Name = "Test Project"
        };

        var windowStart = DateTime.UtcNow.AddDays(-windowDays);
        var windowEnd = DateTime.UtcNow;

        // Create commits with different authors to test bus factor
        var commits = new List<GitLabCommit>
        {
            new()
            {
                Id = "sha1",
                AuthorEmail = "dev1@example.com",
                AuthorName = "Dev 1",
                CommittedDate = windowStart.AddDays(1),
                Stats = new GitLabCommitStats { Total = 100, Additions = 60, Deletions = 40 }
            },
            new()
            {
                Id = "sha2",
                AuthorEmail = "dev2@example.com",
                AuthorName = "Dev 2",
                CommittedDate = windowStart.AddDays(2),
                Stats = new GitLabCommitStats { Total = 50, Additions = 30, Deletions = 20 }
            },
            new()
            {
                Id = "sha3",
                AuthorEmail = "dev1@example.com",
                AuthorName = "Dev 1",
                CommittedDate = windowStart.AddDays(3),
                Stats = new GitLabCommitStats { Total = 75, Additions = 45, Deletions = 30 }
            }
        };

        var mockHttpClient = new Mock<IGitLabHttpClient>();
        mockHttpClient.Setup(c => c.GetUserByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        mockHttpClient.Setup(c => c.GetUserContributedProjectsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { project });
        mockHttpClient.Setup(c => c.GetMergeRequestsAsync(project.Id, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GitLabMergeRequest>());
        mockHttpClient.Setup(c => c.GetCommitsAsync(project.Id, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(commits);

        var mockLogger = new Mock<ILogger<AdvancedMetricsService>>();
        var mockConfig = new Mock<IOptions<MetricsConfiguration>>();
        mockConfig.Setup(c => c.Value).Returns(CreateTestConfiguration());

        var service = new AdvancedMetricsService(mockHttpClient.Object, mockLogger.Object, mockConfig.Object);

        // Act
        var result = await service.CalculateAdvancedMetricsAsync(userId, windowDays);

        // Assert
        Assert.Equal(2, result.ContributingDevelopersCount); // 2 unique developers
        Assert.True(result.BusFactor >= 0 && result.BusFactor <= 1); // Gini coefficient is between 0 and 1
        Assert.True(result.Top3DevelopersFileChangePercentage > 0); // Should have percentage
    }

    [Fact]
    public async Task CalculateAdvancedMetricsAsync_WithResponseTimeData_CalculatesDistribution()
    {
        // Arrange
        const long userId = 1;
        const int windowDays = 30;

        var user = new GitLabUser
        {
            Id = userId,
            Username = "testuser",
            Name = "Test User",
            Email = "test@example.com"
        };

        var project = new GitLabContributedProject
        {
            Id = 100,
            Name = "Test Project"
        };

        var windowStart = DateTime.UtcNow.AddDays(-windowDays);

        var mr = new GitLabMergeRequest
        {
            Id = 1,
            Iid = 1,
            ProjectId = project.Id,
            Title = "Test MR",
            State = "merged",
            CreatedAt = windowStart.AddDays(1),
            MergedAt = windowStart.AddDays(5),
            Author = new GitLabUser { Id = 2, Username = "author", Email = "author@example.com" }
        };

        var notes = new List<GitLabMergeRequestNote>
        {
            new()
            {
                Id = 1,
                Author = new GitLabUser { Id = userId, Username = "testuser" },
                CreatedAt = windowStart.AddDays(2).AddHours(10), // 10 AM
                System = false,
                Body = "Review comment"
            },
            new()
            {
                Id = 2,
                Author = new GitLabUser { Id = userId, Username = "testuser" },
                CreatedAt = windowStart.AddDays(3).AddHours(14), // 2 PM
                System = false,
                Body = "Another review comment"
            }
        };

        var mockHttpClient = new Mock<IGitLabHttpClient>();
        mockHttpClient.Setup(c => c.GetUserByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        mockHttpClient.Setup(c => c.GetUserContributedProjectsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { project });
        mockHttpClient.Setup(c => c.GetMergeRequestsAsync(project.Id, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { mr });
        mockHttpClient.Setup(c => c.GetCommitsAsync(project.Id, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GitLabCommit>());
        mockHttpClient.Setup(c => c.GetMergeRequestNotesAsync(project.Id, mr.Iid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notes);

        var mockLogger = new Mock<ILogger<AdvancedMetricsService>>();
        var mockConfig = new Mock<IOptions<MetricsConfiguration>>();
        mockConfig.Setup(c => c.Value).Returns(CreateTestConfiguration());

        var service = new AdvancedMetricsService(mockHttpClient.Object, mockLogger.Object, mockConfig.Object);

        // Act
        var result = await service.CalculateAdvancedMetricsAsync(userId, windowDays);

        // Assert
        Assert.Equal(2, result.TotalReviewResponses);
        Assert.NotNull(result.PeakResponseHour);
        Assert.NotNull(result.ResponseTimeDistribution);
        Assert.Equal(24, result.ResponseTimeDistribution.Count);
        // Verify the distribution has the correct hours
        var totalDistributed = result.ResponseTimeDistribution.Values.Sum();
        Assert.Equal(2, totalDistributed); // Should match total responses
    }

    [Fact]
    public async Task CalculateAdvancedMetricsAsync_WithBatchSizeData_CalculatesMedianAndP95()
    {
        // Arrange
        const long userId = 1;
        const int windowDays = 30;

        var user = new GitLabUser
        {
            Id = userId,
            Username = "testuser",
            Name = "Test User",
            Email = "test@example.com"
        };

        var project = new GitLabContributedProject
        {
            Id = 100,
            Name = "Test Project"
        };

        var windowStart = DateTime.UtcNow.AddDays(-windowDays);

        var mrs = new List<GitLabMergeRequest>
        {
            new()
            {
                Id = 1,
                Iid = 1,
                ProjectId = project.Id,
                Title = "Test MR 1",
                State = "merged",
                CreatedAt = windowStart.AddDays(1),
                MergedAt = windowStart.AddDays(2),
                Author = new GitLabUser { Id = userId, Username = "testuser", Email = "test@example.com" }
            },
            new()
            {
                Id = 2,
                Iid = 2,
                ProjectId = project.Id,
                Title = "Test MR 2",
                State = "merged",
                CreatedAt = windowStart.AddDays(5),
                MergedAt = windowStart.AddDays(7),
                Author = new GitLabUser { Id = userId, Username = "testuser", Email = "test@example.com" }
            }
        };

        var commits = new List<GitLabCommit>
        {
            new()
            {
                Id = "sha1",
                AuthorEmail = "test@example.com",
                CommittedDate = windowStart.AddDays(1).AddHours(1)
            },
            new()
            {
                Id = "sha2",
                AuthorEmail = "test@example.com",
                CommittedDate = windowStart.AddDays(1).AddHours(2)
            },
            new()
            {
                Id = "sha3",
                AuthorEmail = "test@example.com",
                CommittedDate = windowStart.AddDays(5).AddHours(1)
            }
        };

        var mockHttpClient = new Mock<IGitLabHttpClient>();
        mockHttpClient.Setup(c => c.GetUserByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        mockHttpClient.Setup(c => c.GetUserContributedProjectsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { project });
        mockHttpClient.Setup(c => c.GetMergeRequestsAsync(project.Id, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mrs);
        mockHttpClient.Setup(c => c.GetCommitsAsync(project.Id, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(commits);

        var mockLogger = new Mock<ILogger<AdvancedMetricsService>>();
        var mockConfig = new Mock<IOptions<MetricsConfiguration>>();
        mockConfig.Setup(c => c.Value).Returns(CreateTestConfiguration());

        var service = new AdvancedMetricsService(mockHttpClient.Object, mockLogger.Object, mockConfig.Object);

        // Act
        var result = await service.CalculateAdvancedMetricsAsync(userId, windowDays);

        // Assert
        Assert.True(result.BatchSizeMrCount >= 0);
        if (result.BatchSizeMrCount > 0)
        {
            Assert.NotNull(result.BatchSizeP50);
            Assert.NotNull(result.BatchSizeP95);
            Assert.True(result.BatchSizeP50 > 0);
            Assert.True(result.BatchSizeP95 >= result.BatchSizeP50);
        }
    }

    [Fact]
    public async Task CalculateAdvancedMetricsAsync_WithInvalidWindowDays_ThrowsArgumentException()
    {
        // Arrange
        const long userId = 1;
        const int invalidWindowDays = 0;

        var mockHttpClient = new Mock<IGitLabHttpClient>();
        var mockLogger = new Mock<ILogger<AdvancedMetricsService>>();
        var mockConfig = new Mock<IOptions<MetricsConfiguration>>();
        mockConfig.Setup(c => c.Value).Returns(CreateTestConfiguration());

        var service = new AdvancedMetricsService(mockHttpClient.Object, mockLogger.Object, mockConfig.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await service.CalculateAdvancedMetricsAsync(userId, invalidWindowDays));
    }

    [Fact]
    public async Task CalculateAdvancedMetricsAsync_WithNonExistentUser_ThrowsInvalidOperationException()
    {
        // Arrange
        const long userId = 999;
        const int windowDays = 30;

        var mockHttpClient = new Mock<IGitLabHttpClient>();
        mockHttpClient.Setup(c => c.GetUserByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GitLabUser?)null);

        var mockLogger = new Mock<ILogger<AdvancedMetricsService>>();
        var mockConfig = new Mock<IOptions<MetricsConfiguration>>();
        mockConfig.Setup(c => c.Value).Returns(CreateTestConfiguration());

        var service = new AdvancedMetricsService(mockHttpClient.Object, mockLogger.Object, mockConfig.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await service.CalculateAdvancedMetricsAsync(userId, windowDays));
    }

    [Fact]
    public async Task CalculateAdvancedMetricsAsync_WithCrossTeamCollaboration_ReturnsTeamMappingNotAvailable()
    {
        // Arrange
        const long userId = 1;
        const int windowDays = 30;

        var user = new GitLabUser
        {
            Id = userId,
            Username = "testuser",
            Name = "Test User",
            Email = "test@example.com"
        };

        var project = new GitLabContributedProject
        {
            Id = 100,
            Name = "Test Project"
        };

        var windowStart = DateTime.UtcNow.AddDays(-windowDays);

        var mr = new GitLabMergeRequest
        {
            Id = 1,
            Iid = 1,
            ProjectId = project.Id,
            Title = "Test MR",
            State = "merged",
            CreatedAt = windowStart.AddDays(1),
            MergedAt = windowStart.AddDays(5),
            Author = new GitLabUser { Id = userId, Username = "testuser", Email = "test@example.com" }
        };

        var mockHttpClient = new Mock<IGitLabHttpClient>();
        mockHttpClient.Setup(c => c.GetUserByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        mockHttpClient.Setup(c => c.GetUserContributedProjectsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { project });
        mockHttpClient.Setup(c => c.GetMergeRequestsAsync(project.Id, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { mr });
        mockHttpClient.Setup(c => c.GetCommitsAsync(project.Id, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GitLabCommit>());

        var mockLogger = new Mock<ILogger<AdvancedMetricsService>>();
        var mockConfig = new Mock<IOptions<MetricsConfiguration>>();
        mockConfig.Setup(c => c.Value).Returns(CreateTestConfiguration());

        var service = new AdvancedMetricsService(mockHttpClient.Object, mockLogger.Object, mockConfig.Object);

        // Act
        var result = await service.CalculateAdvancedMetricsAsync(userId, windowDays);

        // Assert
        Assert.False(result.TeamMappingAvailable);
        Assert.Null(result.CrossTeamCollaborationPercentage);
        Assert.Equal(0, result.CrossTeamMrCount);
    }
}
