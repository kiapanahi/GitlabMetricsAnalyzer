using Microsoft.Extensions.Logging;

using Moq;

using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

namespace Toman.Management.KPIAnalysis.Tests.Unit;

/// <summary>
/// Unit tests for QualityMetricsService.
/// </summary>
public sealed class QualityMetricsServiceTests
{
    [Fact]
    public async Task CalculateQualityMetricsAsync_WithInvalidUserId_ThrowsException()
    {
        // Arrange
        const long userId = 999;
        const int windowDays = 30;

        var mockGitLabClient = new Mock<IGitLabHttpClient>();

        mockGitLabClient
            .Setup(x => x.GetUserByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GitLabUser?)null);

        var logger = Mock.Of<ILogger<QualityMetricsService>>();
        var service = new QualityMetricsService(mockGitLabClient.Object, logger);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await service.CalculateQualityMetricsAsync(userId, windowDays, 30, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task CalculateQualityMetricsAsync_WithNoProjects_ReturnsEmptyResult()
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

        var logger = Mock.Of<ILogger<QualityMetricsService>>();
        var service = new QualityMetricsService(mockGitLabClient.Object, logger);

        // Act
        var result = await service.CalculateQualityMetricsAsync(userId, windowDays, 30, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(userId, result.UserId);
        Assert.Equal("testuser", result.Username);
        Assert.Equal(0, result.MergedMrCount);
        Assert.Equal(0, result.ReworkRatio);
        Assert.Equal(0, result.RevertRate);
        Assert.Null(result.CiSuccessRate);
        Assert.Null(result.PipelineDurationP50Min);
        Assert.Null(result.PipelineDurationP95Min);
        Assert.Null(result.TestCoveragePercent);
        Assert.Equal(0, result.HotfixRate);
        Assert.Equal(0, result.ConflictRate);
        Assert.Empty(result.Projects);
    }

    [Fact]
    public async Task CalculateQualityMetricsAsync_WithMergedMRs_CalculatesMetrics()
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
            Name = "test-project"
        };

        var windowStart = DateTime.UtcNow.AddDays(-windowDays);
        var windowEnd = DateTime.UtcNow;

        var mergeRequests = new List<GitLabMergeRequest>
        {
            new()
            {
                Id = 1,
                Iid = 1,
                ProjectId = projectId,
                Title = "Feature: Add new feature",
                State = "merged",
                Author = user,
                CreatedAt = windowStart.AddDays(5),
                MergedAt = windowStart.AddDays(7),
                HasConflicts = false,
                SourceBranch = "feature/new-feature",
                TargetBranch = "main",
                Labels = new List<string> { "feature" }
            },
            new()
            {
                Id = 2,
                Iid = 2,
                ProjectId = projectId,
                Title = "Hotfix: Fix critical bug",
                State = "merged",
                Author = user,
                CreatedAt = windowStart.AddDays(10),
                MergedAt = windowStart.AddDays(10.5),
                HasConflicts = false,
                SourceBranch = "hotfix/critical-bug",
                TargetBranch = "main",
                Labels = new List<string> { "hotfix" }
            },
            new()
            {
                Id = 3,
                Iid = 3,
                ProjectId = projectId,
                Title = "Feature with conflicts",
                State = "merged",
                Author = user,
                CreatedAt = windowStart.AddDays(15),
                MergedAt = windowStart.AddDays(17),
                HasConflicts = true,
                SourceBranch = "feature/conflicting",
                TargetBranch = "main",
                Labels = new List<string> { "feature" }
            },
            new()
            {
                Id = 4,
                Iid = 4,
                ProjectId = projectId,
                Title = "Revert: Feature: Add new feature",
                State = "merged",
                Author = user,
                CreatedAt = windowStart.AddDays(20),
                MergedAt = windowStart.AddDays(20.5),
                HasConflicts = false,
                SourceBranch = "revert/feature",
                TargetBranch = "main",
                Labels = new List<string>()
            }
        };

        var pipelines = new List<GitLabPipeline>
        {
            new()
            {
                Id = 1,
                ProjectId = projectId,
                Sha = "abc123",
                Ref = "feature/new-feature",
                Status = "success",
                CreatedAt = windowStart.AddDays(5),
                UpdatedAt = windowStart.AddDays(5).AddMinutes(10),
                User = user
            },
            new()
            {
                Id = 2,
                ProjectId = projectId,
                Sha = "def456",
                Ref = "hotfix/critical-bug",
                Status = "failed",
                CreatedAt = windowStart.AddDays(10),
                UpdatedAt = windowStart.AddDays(10).AddMinutes(5),
                User = user
            },
            new()
            {
                Id = 3,
                ProjectId = projectId,
                Sha = "def456",
                Ref = "hotfix/critical-bug",
                Status = "success",
                CreatedAt = windowStart.AddDays(10).AddMinutes(10),
                UpdatedAt = windowStart.AddDays(10).AddMinutes(15),
                User = user
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
            .Setup(x => x.GetMergeRequestsAsync(projectId, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mergeRequests);

        mockGitLabClient
            .Setup(x => x.GetPipelinesAsync(projectId, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pipelines);

        // Mock GetMergeRequestCommitsAsync and GetMergeRequestNotesAsync for rework calculation
        mockGitLabClient
            .Setup(x => x.GetMergeRequestCommitsAsync(projectId, It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitLabCommit>());

        mockGitLabClient
            .Setup(x => x.GetMergeRequestNotesAsync(projectId, It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitLabMergeRequestNote>());

        var logger = Mock.Of<ILogger<QualityMetricsService>>();
        var service = new QualityMetricsService(mockGitLabClient.Object, logger);

        // Act
        var result = await service.CalculateQualityMetricsAsync(userId, windowDays, 30, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(userId, result.UserId);
        Assert.Equal("testuser", result.Username);
        Assert.Equal(4, result.MergedMrCount);

        // Revert rate: 1 revert out of 4 MRs = 0.25
        Assert.Equal(0.25m, result.RevertRate);
        Assert.Equal(1, result.RevertedMrCount);

        // CI Success Rate: 2 first runs by SHA - abc123 (success) and def456 (failed, ID 2 is first)
        // 1 successful out of 2 = 0.5
        Assert.Equal(0.5m, result.CiSuccessRate);
        Assert.Equal(1, result.SuccessfulPipelinesFirstRun);
        Assert.Equal(2, result.TotalFirstRunPipelines);

        // Pipeline duration should be calculated
        Assert.NotNull(result.PipelineDurationP50Min);
        Assert.NotNull(result.PipelineDurationP95Min);
        Assert.Equal(3, result.PipelinesWithDurationCount);

        // Hotfix rate: 1 hotfix out of 4 MRs = 0.25
        Assert.Equal(0.25m, result.HotfixRate);
        Assert.Equal(1, result.HotfixMrCount);

        // Conflict rate: 1 conflict out of 4 MRs = 0.25
        Assert.Equal(0.25m, result.ConflictRate);
        Assert.Equal(1, result.ConflictMrCount);

        Assert.Single(result.Projects);
        Assert.Equal(projectId, result.Projects[0].ProjectId);
        Assert.Equal("test-project", result.Projects[0].ProjectName);
        Assert.Equal(4, result.Projects[0].MergedMrCount);
        Assert.Equal(3, result.Projects[0].PipelineCount);
    }

    [Fact]
    public async Task CalculateQualityMetricsAsync_WithNoPipelines_HandlesGracefully()
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
            Name = "test-project"
        };

        var windowStart = DateTime.UtcNow.AddDays(-windowDays);

        var mergeRequests = new List<GitLabMergeRequest>
        {
            new()
            {
                Id = 1,
                Iid = 1,
                ProjectId = projectId,
                Title = "Feature: Add new feature",
                State = "merged",
                Author = user,
                CreatedAt = windowStart.AddDays(5),
                MergedAt = windowStart.AddDays(7),
                HasConflicts = false,
                SourceBranch = "feature/new-feature",
                TargetBranch = "main",
                Labels = new List<string> { "feature" }
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
            .Setup(x => x.GetMergeRequestsAsync(projectId, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mergeRequests);

        mockGitLabClient
            .Setup(x => x.GetPipelinesAsync(projectId, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitLabPipeline>());

        mockGitLabClient
            .Setup(x => x.GetMergeRequestCommitsAsync(projectId, It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitLabCommit>());

        mockGitLabClient
            .Setup(x => x.GetMergeRequestNotesAsync(projectId, It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitLabMergeRequestNote>());

        var logger = Mock.Of<ILogger<QualityMetricsService>>();
        var service = new QualityMetricsService(mockGitLabClient.Object, logger);

        // Act
        var result = await service.CalculateQualityMetricsAsync(userId, windowDays, 30, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.MergedMrCount);
        Assert.Null(result.CiSuccessRate); // No pipelines, should be null
        Assert.Equal(0, result.TotalFirstRunPipelines);
        Assert.Null(result.PipelineDurationP50Min);
        Assert.Null(result.PipelineDurationP95Min);
        Assert.Equal(0, result.PipelinesWithDurationCount);
    }

    [Fact]
    public async Task CalculateQualityMetricsAsync_WithZeroWindowDays_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        const long userId = 1;
        const int windowDays = 0;

        var mockGitLabClient = new Mock<IGitLabHttpClient>();
        var logger = Mock.Of<ILogger<QualityMetricsService>>();
        var service = new QualityMetricsService(mockGitLabClient.Object, logger);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await service.CalculateQualityMetricsAsync(userId, windowDays, 30, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task CalculateQualityMetricsAsync_WithNegativeRevertDetectionDays_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        const long userId = 1;
        const int windowDays = 30;
        const int revertDetectionDays = -1;

        var mockGitLabClient = new Mock<IGitLabHttpClient>();
        var logger = Mock.Of<ILogger<QualityMetricsService>>();
        var service = new QualityMetricsService(mockGitLabClient.Object, logger);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            async () => await service.CalculateQualityMetricsAsync(userId, windowDays, revertDetectionDays, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task CalculateQualityMetricsAsync_WithHotfixBranchPattern_DetectsHotfix()
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
            Name = "test-project"
        };

        var windowStart = DateTime.UtcNow.AddDays(-windowDays);

        var mergeRequests = new List<GitLabMergeRequest>
        {
            new()
            {
                Id = 1,
                Iid = 1,
                ProjectId = projectId,
                Title = "Normal feature",
                State = "merged",
                Author = user,
                CreatedAt = windowStart.AddDays(5),
                MergedAt = windowStart.AddDays(7),
                HasConflicts = false,
                SourceBranch = "hot-fix/urgent-issue",
                TargetBranch = "main",
                Labels = new List<string>()
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
            .Setup(x => x.GetMergeRequestsAsync(projectId, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mergeRequests);

        mockGitLabClient
            .Setup(x => x.GetPipelinesAsync(projectId, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitLabPipeline>());

        mockGitLabClient
            .Setup(x => x.GetMergeRequestCommitsAsync(projectId, It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitLabCommit>());

        mockGitLabClient
            .Setup(x => x.GetMergeRequestNotesAsync(projectId, It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitLabMergeRequestNote>());

        var logger = Mock.Of<ILogger<QualityMetricsService>>();
        var service = new QualityMetricsService(mockGitLabClient.Object, logger);

        // Act
        var result = await service.CalculateQualityMetricsAsync(userId, windowDays, 30, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1.0m, result.HotfixRate); // 1 out of 1 is hotfix
        Assert.Equal(1, result.HotfixMrCount);
    }
}
