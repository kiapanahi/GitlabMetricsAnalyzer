using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

using Toman.Management.KPIAnalysis.ApiService.Configuration;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

namespace Toman.Management.KPIAnalysis.Tests.Unit;

/// <summary>
/// Unit tests for CodeCharacteristicsService.
/// </summary>
public sealed class CodeCharacteristicsServiceTests
{
    [Fact]
    public async Task CalculateCodeCharacteristicsAsync_WithInvalidUserId_ThrowsException()
    {
        // Arrange
        const long userId = 999;
        const int windowDays = 30;

        var mockGitLabClient = new Mock<IGitLabHttpClient>();

        mockGitLabClient
            .Setup(x => x.GetUserByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GitLabUser?)null);

        var logger = Mock.Of<ILogger<CodeCharacteristicsService>>();
        var config = CreateDefaultMetricsConfig();
        var service = new CodeCharacteristicsService(mockGitLabClient.Object, logger, config);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await service.CalculateCodeCharacteristicsAsync(userId, windowDays, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task CalculateCodeCharacteristicsAsync_WithNoProjects_ReturnsEmptyResult()
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

        var mockGitLabClient = new Mock<IGitLabHttpClient>();

        mockGitLabClient
            .Setup(x => x.GetUserByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        mockGitLabClient
            .Setup(x => x.GetUserContributedProjectsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitLabContributedProject>());

        var logger = Mock.Of<ILogger<CodeCharacteristicsService>>();
        var config = CreateDefaultMetricsConfig();
        var service = new CodeCharacteristicsService(mockGitLabClient.Object, logger, config);

        // Act
        var result = await service.CalculateCodeCharacteristicsAsync(userId, windowDays, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(userId, result.UserId);
        Assert.Equal("testuser", result.Username);
        Assert.Equal(0, result.TotalCommits);
        Assert.Equal(0, result.TotalMergedMrs);
        Assert.Equal(0, result.CommitsPerDay);
        Assert.Equal(0, result.CommitsPerWeek);
        Assert.Null(result.CommitSizeMedian);
        Assert.Empty(result.Projects);
    }

    [Fact]
    public async Task CalculateCodeCharacteristicsAsync_WithCommits_CalculatesCommitFrequency()
    {
        // Arrange
        const long userId = 1;
        const long projectId = 100;
        const int windowDays = 7;

        var user = new GitLabUser
        {
            Id = userId,
            Username = "testuser",
            Name = "Test User",
            Email = "test@example.com"
        };

        var project = new GitLabContributedProject
        {
            Id = projectId,
            Name = "Test Project"
        };

        // Create 3 commits on different days
        var now = DateTime.UtcNow;
        var commits = new List<GitLabCommit>
        {
            new GitLabCommit
            {
                Id = "commit1",
                AuthorName = "Test User",
                AuthorEmail = "test@example.com",
                CommittedDate = now.AddDays(-1),
                Title = "feat: add feature",
                Stats = new GitLabCommitStats { Additions = 10, Deletions = 5, Total = 15 }
            },
            new GitLabCommit
            {
                Id = "commit2",
                AuthorName = "Test User",
                AuthorEmail = "test@example.com",
                CommittedDate = now.AddDays(-2),
                Title = "fix: bug fix",
                Stats = new GitLabCommitStats { Additions = 20, Deletions = 10, Total = 30 }
            },
            new GitLabCommit
            {
                Id = "commit3",
                AuthorName = "Test User",
                AuthorEmail = "test@example.com",
                CommittedDate = now.AddDays(-3),
                Title = "refactor: improve code",
                Stats = new GitLabCommitStats { Additions = 15, Deletions = 8, Total = 23 }
            }
        };

        var mockGitLabClient = new Mock<IGitLabHttpClient>();

        mockGitLabClient
            .Setup(x => x.GetUserByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        mockGitLabClient
            .Setup(x => x.GetUserContributedProjectsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitLabContributedProject> { project });

        mockGitLabClient
            .Setup(x => x.GetCommitsAsync(projectId, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(commits);

        mockGitLabClient
            .Setup(x => x.GetMergeRequestsAsync(projectId, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitLabMergeRequest>());

        var logger = Mock.Of<ILogger<CodeCharacteristicsService>>();
        var config = CreateDefaultMetricsConfig();
        var service = new CodeCharacteristicsService(mockGitLabClient.Object, logger, config);

        // Act
        var result = await service.CalculateCodeCharacteristicsAsync(userId, windowDays, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.TotalCommits);
        Assert.Equal(3, result.CommitDaysCount);
        Assert.Equal(3m / windowDays, result.CommitsPerDay);
        Assert.Equal(3m / windowDays * 7, result.CommitsPerWeek);
        Assert.Equal(23m, result.CommitSizeMedian); // Median of [15, 23, 30]
    }

    [Fact]
    public async Task CalculateCodeCharacteristicsAsync_WithMRs_CalculatesMRSizeDistribution()
    {
        // Arrange
        const long userId = 1;
        const long projectId = 100;
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
            Id = projectId,
            Name = "Test Project"
        };

        var now = DateTime.UtcNow;
        var mergeRequests = new List<GitLabMergeRequest>
        {
            // Small MR: 50 lines
            new GitLabMergeRequest
            {
                Id = 1,
                Iid = 1,
                ProjectId = projectId,
                Title = "Small MR",
                Author = user,
                MergedAt = now.AddDays(-1),
                State = "merged",
                SourceBranch = "feature/small",
                TargetBranch = "main",
                Squash = false
            },
            // Medium MR: 300 lines
            new GitLabMergeRequest
            {
                Id = 2,
                Iid = 2,
                ProjectId = projectId,
                Title = "Medium MR",
                Author = user,
                MergedAt = now.AddDays(-2),
                State = "merged",
                SourceBranch = "feature/medium",
                TargetBranch = "main",
                Squash = true
            },
            // Large MR: 800 lines
            new GitLabMergeRequest
            {
                Id = 3,
                Iid = 3,
                ProjectId = projectId,
                Title = "Large MR",
                Author = user,
                MergedAt = now.AddDays(-3),
                State = "merged",
                SourceBranch = "feature/large",
                TargetBranch = "main",
                Squash = false
            },
            // XL MR: 1500 lines
            new GitLabMergeRequest
            {
                Id = 4,
                Iid = 4,
                ProjectId = projectId,
                Title = "XL MR",
                Author = user,
                MergedAt = now.AddDays(-4),
                State = "merged",
                SourceBranch = "feat/xl",
                TargetBranch = "main",
                Squash = true
            }
        };

        var mockGitLabClient = new Mock<IGitLabHttpClient>();

        mockGitLabClient
            .Setup(x => x.GetUserByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        mockGitLabClient
            .Setup(x => x.GetUserContributedProjectsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitLabContributedProject> { project });

        mockGitLabClient
            .Setup(x => x.GetCommitsAsync(projectId, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitLabCommit>());

        mockGitLabClient
            .Setup(x => x.GetMergeRequestsAsync(projectId, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mergeRequests);

        // Setup MR changes
        mockGitLabClient
            .Setup(x => x.GetMergeRequestChangesAsync(projectId, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GitLabMergeRequestChanges
            {
                Additions = 30,
                Deletions = 20,
                Total = 50,
                Changes = new List<GitLabMergeRequestChange>()
            });

        mockGitLabClient
            .Setup(x => x.GetMergeRequestChangesAsync(projectId, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GitLabMergeRequestChanges
            {
                Additions = 200,
                Deletions = 100,
                Total = 300,
                Changes = new List<GitLabMergeRequestChange>()
            });

        mockGitLabClient
            .Setup(x => x.GetMergeRequestChangesAsync(projectId, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GitLabMergeRequestChanges
            {
                Additions = 500,
                Deletions = 300,
                Total = 800,
                Changes = new List<GitLabMergeRequestChange>()
            });

        mockGitLabClient
            .Setup(x => x.GetMergeRequestChangesAsync(projectId, 4, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GitLabMergeRequestChanges
            {
                Additions = 1000,
                Deletions = 500,
                Total = 1500,
                Changes = new List<GitLabMergeRequestChange>()
            });

        var logger = Mock.Of<ILogger<CodeCharacteristicsService>>();
        var config = CreateDefaultMetricsConfig();
        var service = new CodeCharacteristicsService(mockGitLabClient.Object, logger, config);

        // Act
        var result = await service.CalculateCodeCharacteristicsAsync(userId, windowDays, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(4, result.TotalMergedMrs);
        Assert.Equal(1, result.MrSizeDistribution.SmallCount);
        Assert.Equal(1, result.MrSizeDistribution.MediumCount);
        Assert.Equal(1, result.MrSizeDistribution.LargeCount);
        Assert.Equal(1, result.MrSizeDistribution.ExtraLargeCount);
        Assert.Equal(25m, result.MrSizeDistribution.SmallPercentage);
        Assert.Equal(25m, result.MrSizeDistribution.MediumPercentage);
        Assert.Equal(25m, result.MrSizeDistribution.LargePercentage);
        Assert.Equal(25m, result.MrSizeDistribution.ExtraLargePercentage);
    }

    [Fact]
    public async Task CalculateCodeCharacteristicsAsync_WithMRs_CalculatesSquashRate()
    {
        // Arrange
        const long userId = 1;
        const long projectId = 100;
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
            Id = projectId,
            Name = "Test Project"
        };

        var now = DateTime.UtcNow;
        var mergeRequests = new List<GitLabMergeRequest>
        {
            new GitLabMergeRequest
            {
                Id = 1,
                Iid = 1,
                ProjectId = projectId,
                Author = user,
                MergedAt = now.AddDays(-1),
                State = "merged",
                SourceBranch = "feature/1",
                TargetBranch = "main",
                Squash = true
            },
            new GitLabMergeRequest
            {
                Id = 2,
                Iid = 2,
                ProjectId = projectId,
                Author = user,
                MergedAt = now.AddDays(-2),
                State = "merged",
                SourceBranch = "feature/2",
                TargetBranch = "main",
                Squash = true
            },
            new GitLabMergeRequest
            {
                Id = 3,
                Iid = 3,
                ProjectId = projectId,
                Author = user,
                MergedAt = now.AddDays(-3),
                State = "merged",
                SourceBranch = "feature/3",
                TargetBranch = "main",
                Squash = false
            }
        };

        var mockGitLabClient = new Mock<IGitLabHttpClient>();

        mockGitLabClient
            .Setup(x => x.GetUserByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        mockGitLabClient
            .Setup(x => x.GetUserContributedProjectsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitLabContributedProject> { project });

        mockGitLabClient
            .Setup(x => x.GetCommitsAsync(projectId, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitLabCommit>());

        mockGitLabClient
            .Setup(x => x.GetMergeRequestsAsync(projectId, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mergeRequests);

        var logger = Mock.Of<ILogger<CodeCharacteristicsService>>();
        var config = CreateDefaultMetricsConfig();
        var service = new CodeCharacteristicsService(mockGitLabClient.Object, logger, config);

        // Act
        var result = await service.CalculateCodeCharacteristicsAsync(userId, windowDays, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.TotalMergedMrs);
        Assert.Equal(2, result.SquashedMrsCount);
        Assert.Equal(2m / 3m, result.SquashMergeRate);
    }

    [Fact]
    public async Task CalculateCodeCharacteristicsAsync_WithCommits_CalculatesCommitMessageQuality()
    {
        // Arrange
        const long userId = 1;
        const long projectId = 100;
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
            Id = projectId,
            Name = "Test Project"
        };

        var now = DateTime.UtcNow;
        var commits = new List<GitLabCommit>
        {
            // Good conventional commit
            new GitLabCommit
            {
                Id = "commit1",
                AuthorName = "Test User",
                AuthorEmail = "test@example.com",
                CommittedDate = now.AddDays(-1),
                Title = "feat: add new feature",
                Stats = new GitLabCommitStats { Additions = 10, Deletions = 5, Total = 15 }
            },
            // Good conventional commit with scope
            new GitLabCommit
            {
                Id = "commit2",
                AuthorName = "Test User",
                AuthorEmail = "test@example.com",
                CommittedDate = now.AddDays(-2),
                Title = "fix(api): resolve bug",
                Stats = new GitLabCommitStats { Additions = 20, Deletions = 10, Total = 30 }
            },
            // Non-conventional commit
            new GitLabCommit
            {
                Id = "commit3",
                AuthorName = "Test User",
                AuthorEmail = "test@example.com",
                CommittedDate = now.AddDays(-3),
                Title = "update some stuff",
                Stats = new GitLabCommitStats { Additions = 15, Deletions = 8, Total = 23 }
            },
            // Excluded pattern (too short)
            new GitLabCommit
            {
                Id = "commit4",
                AuthorName = "Test User",
                AuthorEmail = "test@example.com",
                CommittedDate = now.AddDays(-4),
                Title = "wip",
                Stats = new GitLabCommitStats { Additions = 5, Deletions = 2, Total = 7 }
            }
        };

        var mockGitLabClient = new Mock<IGitLabHttpClient>();

        mockGitLabClient
            .Setup(x => x.GetUserByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        mockGitLabClient
            .Setup(x => x.GetUserContributedProjectsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitLabContributedProject> { project });

        mockGitLabClient
            .Setup(x => x.GetCommitsAsync(projectId, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(commits);

        mockGitLabClient
            .Setup(x => x.GetMergeRequestsAsync(projectId, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitLabMergeRequest>());

        var logger = Mock.Of<ILogger<CodeCharacteristicsService>>();
        var config = CreateDefaultMetricsConfig();
        var service = new CodeCharacteristicsService(mockGitLabClient.Object, logger, config);

        // Act
        var result = await service.CalculateCodeCharacteristicsAsync(userId, windowDays, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(4, result.TotalCommits);
        Assert.Equal(2, result.ConventionalCommitsCount); // 2 out of 4 are conventional
        Assert.Equal(2m / 4m, result.ConventionalCommitRate);
        Assert.True(result.AverageCommitMessageLength > 0);
    }

    [Fact]
    public async Task CalculateCodeCharacteristicsAsync_WithMRs_CalculatesBranchNamingCompliance()
    {
        // Arrange
        const long userId = 1;
        const long projectId = 100;
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
            Id = projectId,
            Name = "Test Project"
        };

        var now = DateTime.UtcNow;
        var mergeRequests = new List<GitLabMergeRequest>
        {
            // Compliant branch names
            new GitLabMergeRequest
            {
                Id = 1,
                Iid = 1,
                ProjectId = projectId,
                Author = user,
                MergedAt = now.AddDays(-1),
                State = "merged",
                SourceBranch = "feature/add-login",
                TargetBranch = "main",
                Squash = false
            },
            new GitLabMergeRequest
            {
                Id = 2,
                Iid = 2,
                ProjectId = projectId,
                Author = user,
                MergedAt = now.AddDays(-2),
                State = "merged",
                SourceBranch = "bugfix/fix-crash",
                TargetBranch = "main",
                Squash = false
            },
            // Non-compliant branch name
            new GitLabMergeRequest
            {
                Id = 3,
                Iid = 3,
                ProjectId = projectId,
                Author = user,
                MergedAt = now.AddDays(-3),
                State = "merged",
                SourceBranch = "my-changes",
                TargetBranch = "main",
                Squash = false
            }
        };

        var mockGitLabClient = new Mock<IGitLabHttpClient>();

        mockGitLabClient
            .Setup(x => x.GetUserByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        mockGitLabClient
            .Setup(x => x.GetUserContributedProjectsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitLabContributedProject> { project });

        mockGitLabClient
            .Setup(x => x.GetCommitsAsync(projectId, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitLabCommit>());

        mockGitLabClient
            .Setup(x => x.GetMergeRequestsAsync(projectId, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mergeRequests);

        var logger = Mock.Of<ILogger<CodeCharacteristicsService>>();
        var config = CreateDefaultMetricsConfig();
        var service = new CodeCharacteristicsService(mockGitLabClient.Object, logger, config);

        // Act
        var result = await service.CalculateCodeCharacteristicsAsync(userId, windowDays, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.TotalMergedMrs);
        Assert.Equal(2, result.CompliantBranchesCount);
        Assert.Equal(2m / 3m, result.BranchNamingComplianceRate);
    }

    [Fact]
    public async Task CalculateCodeCharacteristicsAsync_WithInvalidWindowDays_ThrowsException()
    {
        // Arrange
        const long userId = 1;
        const int invalidWindowDays = 0;

        var mockGitLabClient = new Mock<IGitLabHttpClient>();
        var logger = Mock.Of<ILogger<CodeCharacteristicsService>>();
        var config = CreateDefaultMetricsConfig();
        var service = new CodeCharacteristicsService(mockGitLabClient.Object, logger, config);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await service.CalculateCodeCharacteristicsAsync(userId, invalidWindowDays, TestContext.Current.CancellationToken)
        );
    }

    private static IOptions<MetricsConfiguration> CreateDefaultMetricsConfig()
    {
        var config = new MetricsConfiguration
        {
            Identity = new IdentityConfiguration(),
            Excludes = new ExclusionConfiguration(),
            CodeCharacteristics = new CodeCharacteristicsConfiguration()
        };

        return Options.Create(config);
    }
}
