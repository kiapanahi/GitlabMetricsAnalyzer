using Microsoft.Extensions.Logging;

using Moq;

using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

namespace Toman.Management.KPIAnalysis.Tests.Unit;

/// <summary>
/// Unit tests for PerDeveloperMetricsService.
/// </summary>
public sealed class PerDeveloperMetricsServiceTests
{
    [Fact]
    public async Task CalculateMrCycleTimeAsync_WithMergedMRs_CalculatesMedianCorrectly()
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
            Name = "test-project",
            Path = "test-project",
            PathWithNamespace = "company/test-project",
            NameWithNamespace = "Company / Test Project",
            DefaultBranch = "main",
            WebUrl = "https://gitlab.example.com/company/test-project",
            ForksCount = 0
        };

        var now = DateTime.UtcNow;
        
        // Create MRs with known cycle times
        // MR 1: 2 days cycle time (48 hours)
        var mr1 = new GitLabMergeRequest
        {
            Id = 1,
            Iid = 1,
            ProjectId = 100,
            Title = "MR 1",
            State = "merged",
            CreatedAt = now.AddDays(-10),
            MergedAt = now.AddDays(-8),
            Author = user,
            TargetBranch = "main",
            SourceBranch = "feature/1"
        };

        // MR 2: 4 days cycle time (96 hours)
        var mr2 = new GitLabMergeRequest
        {
            Id = 2,
            Iid = 2,
            ProjectId = 100,
            Title = "MR 2",
            State = "merged",
            CreatedAt = now.AddDays(-14),
            MergedAt = now.AddDays(-10),
            Author = user,
            TargetBranch = "main",
            SourceBranch = "feature/2"
        };

        // Create commits for MRs
        // MR 1 has first commit 2 days before merge
        var mr1Commits = new List<GitLabCommit>
        {
            new GitLabCommit
            {
                Id = "commit1",
                ShortId = "abc123",
                Title = "First commit for MR 1",
                AuthorName = "Test User",
                AuthorEmail = "test@example.com",
                CommittedDate = now.AddDays(-10), // Same as MR creation
                ProjectId = 100
            }
        };

        // MR 2 has first commit 4 days before merge
        var mr2Commits = new List<GitLabCommit>
        {
            new GitLabCommit
            {
                Id = "commit2",
                ShortId = "def456",
                Title = "First commit for MR 2",
                AuthorName = "Test User",
                AuthorEmail = "test@example.com",
                CommittedDate = now.AddDays(-14), // 4 days before merge
                ProjectId = 100
            },
            new GitLabCommit
            {
                Id = "commit3",
                ShortId = "ghi789",
                Title = "Second commit for MR 2",
                AuthorName = "Test User",
                AuthorEmail = "test@example.com",
                CommittedDate = now.AddDays(-12),
                ProjectId = 100
            }
        };

        // Setup mocks
        var mockGitLabClient = new Mock<IGitLabHttpClient>();
        
        mockGitLabClient
            .Setup(x => x.GetUserByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        mockGitLabClient
            .Setup(x => x.GetUserContributedProjectsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitLabContributedProject> { project });

        mockGitLabClient
            .Setup(x => x.GetMergeRequestsAsync(100, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitLabMergeRequest> { mr1, mr2 });

        mockGitLabClient
            .Setup(x => x.GetMergeRequestCommitsAsync(100, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mr1Commits);

        mockGitLabClient
            .Setup(x => x.GetMergeRequestCommitsAsync(100, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mr2Commits);

        var logger = Mock.Of<ILogger<PerDeveloperMetricsService>>();
        var service = new PerDeveloperMetricsService(mockGitLabClient.Object, logger);

        // Act
        var result = await service.CalculateMrCycleTimeAsync(userId, windowDays, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(userId, result.UserId);
        Assert.Equal("testuser", result.Username);
        Assert.Equal(windowDays, result.WindowDays);
        Assert.NotNull(result.MrCycleTimeP50H);
        
        // Median of 48h and 96h should be 72h
        Assert.Equal(72m, result.MrCycleTimeP50H.Value);
        Assert.Equal(2, result.MergedMrCount);
        Assert.Equal(0, result.ExcludedMrCount);
        Assert.Single(result.Projects);
        Assert.Equal(100, result.Projects[0].ProjectId);
        Assert.Equal("test-project", result.Projects[0].ProjectName);
        Assert.Equal(2, result.Projects[0].MergedMrCount);
    }

    [Fact]
    public async Task CalculateMrCycleTimeAsync_WithNoMRs_ReturnsEmptyResult()
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

        // Setup mocks
        var mockGitLabClient = new Mock<IGitLabHttpClient>();
        
        mockGitLabClient
            .Setup(x => x.GetUserByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        mockGitLabClient
            .Setup(x => x.GetUserContributedProjectsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitLabContributedProject>());

        var logger = Mock.Of<ILogger<PerDeveloperMetricsService>>();
        var service = new PerDeveloperMetricsService(mockGitLabClient.Object, logger);

        // Act
        var result = await service.CalculateMrCycleTimeAsync(userId, windowDays, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(userId, result.UserId);
        Assert.Null(result.MrCycleTimeP50H);
        Assert.Equal(0, result.MergedMrCount);
        Assert.Equal(0, result.ExcludedMrCount);
        Assert.Empty(result.Projects);
    }

    [Fact]
    public async Task CalculateMrCycleTimeAsync_WithUserNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        const long userId = 999;
        const int windowDays = 30;

        var mockGitLabClient = new Mock<IGitLabHttpClient>();
        
        mockGitLabClient
            .Setup(x => x.GetUserByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GitLabUser?)null);

        var logger = Mock.Of<ILogger<PerDeveloperMetricsService>>();
        var service = new PerDeveloperMetricsService(mockGitLabClient.Object, logger);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await service.CalculateMrCycleTimeAsync(userId, windowDays, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CalculateMrCycleTimeAsync_WithInvalidWindowDays_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        const long userId = 1;
        const int windowDays = -1;

        var mockGitLabClient = new Mock<IGitLabHttpClient>();
        var logger = Mock.Of<ILogger<PerDeveloperMetricsService>>();
        var service = new PerDeveloperMetricsService(mockGitLabClient.Object, logger);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await service.CalculateMrCycleTimeAsync(userId, windowDays, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CalculateMrCycleTimeAsync_WithOddNumberOfMRs_CalculatesMedianCorrectly()
    {
        // Arrange
        const long userId = 1;
        const int windowDays = 30;

        var user = new GitLabUser
        {
            Id = userId,
            Username = "testuser",
            Email = "test@example.com"
        };

        var project = new GitLabContributedProject
        {
            Id = 100,
            Name = "test-project",
            Path = "test-project",
            PathWithNamespace = "company/test-project",
            NameWithNamespace = "Company / Test Project",
            DefaultBranch = "main",
            WebUrl = "https://gitlab.example.com/company/test-project",
            ForksCount = 0
        };

        var now = DateTime.UtcNow;
        
        // Create 3 MRs with cycle times: 24h, 48h, 72h
        var mr1 = new GitLabMergeRequest
        {
            Id = 1,
            Iid = 1,
            ProjectId = 100,
            State = "merged",
            MergedAt = now.AddDays(-1),
            Author = user,
            TargetBranch = "main",
            SourceBranch = "feature/1"
        };

        var mr2 = new GitLabMergeRequest
        {
            Id = 2,
            Iid = 2,
            ProjectId = 100,
            State = "merged",
            MergedAt = now.AddDays(-5),
            Author = user,
            TargetBranch = "main",
            SourceBranch = "feature/2"
        };

        var mr3 = new GitLabMergeRequest
        {
            Id = 3,
            Iid = 3,
            ProjectId = 100,
            State = "merged",
            MergedAt = now.AddDays(-10),
            Author = user,
            TargetBranch = "main",
            SourceBranch = "feature/3"
        };

        var mr1Commits = new List<GitLabCommit>
        {
            new GitLabCommit
            {
                Id = "commit1",
                CommittedDate = now.AddDays(-2), // 24h before merge
                ProjectId = 100
            }
        };

        var mr2Commits = new List<GitLabCommit>
        {
            new GitLabCommit
            {
                Id = "commit2",
                CommittedDate = now.AddDays(-7), // 48h before merge
                ProjectId = 100
            }
        };

        var mr3Commits = new List<GitLabCommit>
        {
            new GitLabCommit
            {
                Id = "commit3",
                CommittedDate = now.AddDays(-13), // 72h before merge
                ProjectId = 100
            }
        };

        // Setup mocks
        var mockGitLabClient = new Mock<IGitLabHttpClient>();
        
        mockGitLabClient
            .Setup(x => x.GetUserByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        mockGitLabClient
            .Setup(x => x.GetUserContributedProjectsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitLabContributedProject> { project });

        mockGitLabClient
            .Setup(x => x.GetMergeRequestsAsync(100, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitLabMergeRequest> { mr1, mr2, mr3 });

        mockGitLabClient
            .Setup(x => x.GetMergeRequestCommitsAsync(100, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mr1Commits);

        mockGitLabClient
            .Setup(x => x.GetMergeRequestCommitsAsync(100, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mr2Commits);

        mockGitLabClient
            .Setup(x => x.GetMergeRequestCommitsAsync(100, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mr3Commits);

        var logger = Mock.Of<ILogger<PerDeveloperMetricsService>>();
        var service = new PerDeveloperMetricsService(mockGitLabClient.Object, logger);

        // Act
        var result = await service.CalculateMrCycleTimeAsync(userId, windowDays, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.MrCycleTimeP50H);
        
        // Median of 24h, 48h, 72h should be 48h (middle value)
        Assert.Equal(48m, result.MrCycleTimeP50H.Value);
        Assert.Equal(3, result.MergedMrCount);
    }
}
