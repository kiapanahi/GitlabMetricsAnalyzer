using Microsoft.Extensions.Logging;

using Moq;

using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

using GitLabBranch = Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure.DTOs.GitLabBranch;
using GitLabBranchCommit = Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure.DTOs.GitLabBranchCommit;
using GitLabMilestone = Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure.DTOs.GitLabMilestone;
using RawGitLabProject = Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw.GitLabProject;
using RawGitLabCommit = Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw.GitLabCommit;
using RawGitLabMergeRequest = Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw.GitLabMergeRequest;
using RawGitLabUser = Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw.GitLabUser;
using RawGitLabContributedProject = Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw.GitLabContributedProject;
using RawGitLabMergeRequestApprovals = Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw.GitLabMergeRequestApprovals;

namespace Toman.Management.KPIAnalysis.Tests.Unit;

/// <summary>
/// Unit tests for ProjectMetricsService.
/// </summary>
public sealed class ProjectMetricsServiceTests
{
    [Fact]
    public async Task CalculateProjectMetricsAsync_WithValidProject_ReturnsMetrics()
    {
        // Arrange
        const long projectId = 100;
        const int windowDays = 30;

        var mockHttpClient = new Mock<IGitLabHttpClient>();
        var mockLogger = new Mock<ILogger<ProjectMetricsService>>();

        var project = new RawGitLabProject
        {
            Id = projectId,
            Name = "test-project",
            PathWithNamespace = "company/test-project"
        };

        mockHttpClient
            .Setup(x => x.GetProjectByIdAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

        var now = DateTime.UtcNow;
        var commits = new List<RawGitLabCommit>
        {
            new RawGitLabCommit
            {
                Id = "commit1",
                ShortId = "abc123",
                Title = "Test commit",
                AuthorName = "Test User",
                AuthorEmail = "test@example.com",
                CommittedDate = now.AddDays(-5),
                ProjectId = projectId
            }
        };

        mockHttpClient
            .Setup(x => x.GetCommitsAsync(It.IsAny<long>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(commits);

        var mergeRequests = new List<RawGitLabMergeRequest>
        {
            new RawGitLabMergeRequest
            {
                Id = 1,
                Iid = 1,
                ProjectId = projectId,
                Title = "Test MR",
                State = "merged",
                CreatedAt = now.AddDays(-5),
                MergedAt = now.AddDays(-4),
                Author = new RawGitLabUser { Id = 1, Username = "user1" }
            }
        };

        mockHttpClient
            .Setup(x => x.GetMergeRequestsAsync(It.IsAny<long>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mergeRequests);

        mockHttpClient
            .Setup(x => x.GetUserContributedProjectsAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RawGitLabContributedProject>
            {
                new RawGitLabContributedProject
                {
                    Id = projectId,
                    Name = "test-project",
                    PathWithNamespace = "company/test-project"
                }
            });

        var branches = new List<GitLabBranch>
        {
            new GitLabBranch(
                Name: "main",
                Merged: false,
                Protected: true,
                DevelopersCanPush: false,
                DevelopersCanMerge: false,
                Commit: new GitLabBranchCommit(
                    Id: "commit1",
                    ShortId: "abc123",
                    Title: "Recent commit",
                    AuthorName: "Test User",
                    CreatedAt: now.AddDays(-1),
                    CommittedDate: now.AddDays(-1)
                )
            ),
            new GitLabBranch(
                Name: "old-feature",
                Merged: false,
                Protected: false,
                DevelopersCanPush: true,
                DevelopersCanMerge: true,
                Commit: new GitLabBranchCommit(
                    Id: "commit2",
                    ShortId: "def456",
                    Title: "Old commit",
                    AuthorName: "Test User",
                    CreatedAt: now.AddDays(-40),
                    CommittedDate: now.AddDays(-40)
                )
            )
        };

        mockHttpClient
            .Setup(x => x.GetBranchesAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(branches);

        var milestones = new List<GitLabMilestone>
        {
            new GitLabMilestone(
                Id: 1,
                Iid: 1,
                ProjectId: (int)projectId,
                Title: "Sprint 1",
                Description: "First sprint",
                State: "closed",
                CreatedAt: now.AddDays(-60),
                UpdatedAt: now.AddDays(-30),
                DueDate: DateOnly.FromDateTime(now.AddDays(-31)),
                StartDate: DateOnly.FromDateTime(now.AddDays(-60)),
                Expired: false,
                WebUrl: "https://gitlab.example.com/milestone/1"
            )
        };

        mockHttpClient
            .Setup(x => x.GetMilestonesAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(milestones);

        mockHttpClient
            .Setup(x => x.GetMergeRequestApprovalsAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RawGitLabMergeRequestApprovals?)null);

        var service = new ProjectMetricsService(mockHttpClient.Object, mockLogger.Object);

        // Act
        var result = await service.CalculateProjectMetricsAsync(projectId, windowDays);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(projectId, result.ProjectId);
        Assert.Equal("company/test-project", result.ProjectName);
        Assert.Equal(windowDays, result.WindowDays);
        Assert.Equal(1, result.TotalCommits);
        Assert.Equal(1, result.TotalMergedMrs);
        Assert.Equal(1, result.LongLivedBranchCount); // old-feature branch is > 30 days
    }

    [Fact]
    public async Task CalculateProjectMetricsAsync_WithNonExistentProject_ThrowsException()
    {
        // Arrange
        const long projectId = 999;

        var mockHttpClient = new Mock<IGitLabHttpClient>();
        var mockLogger = new Mock<ILogger<ProjectMetricsService>>();

        mockHttpClient
            .Setup(x => x.GetProjectByIdAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RawGitLabProject?)null);

        var service = new ProjectMetricsService(mockHttpClient.Object, mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await service.CalculateProjectMetricsAsync(projectId, 30));
    }

    [Fact]
    public async Task CalculateProjectMetricsAsync_WithNoActivity_ReturnsZeroMetrics()
    {
        // Arrange
        const long projectId = 100;
        const int windowDays = 30;

        var mockHttpClient = new Mock<IGitLabHttpClient>();
        var mockLogger = new Mock<ILogger<ProjectMetricsService>>();

        var project = new RawGitLabProject
        {
            Id = projectId,
            Name = "empty-project",
            PathWithNamespace = "company/empty-project"
        };

        mockHttpClient
            .Setup(x => x.GetProjectByIdAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

        mockHttpClient
            .Setup(x => x.GetCommitsAsync(It.IsAny<long>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RawGitLabCommit>());

        mockHttpClient
            .Setup(x => x.GetMergeRequestsAsync(It.IsAny<long>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RawGitLabMergeRequest>());

        mockHttpClient
            .Setup(x => x.GetBranchesAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitLabBranch>());

        mockHttpClient
            .Setup(x => x.GetMilestonesAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitLabMilestone>());

        var service = new ProjectMetricsService(mockHttpClient.Object, mockLogger.Object);

        // Act
        var result = await service.CalculateProjectMetricsAsync(projectId, windowDays);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(projectId, result.ProjectId);
        Assert.Equal(0, result.TotalCommits);
        Assert.Equal(0, result.TotalMergedMrs);
        Assert.Equal(0, result.LongLivedBranchCount);
        Assert.Empty(result.LabelUsageDistribution);
        Assert.Empty(result.LongLivedBranches);
    }
}
