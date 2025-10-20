using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

using KuriousLabs.Management.KPIAnalysis.ApiService.Configuration;
using KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure;
using KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;
using KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

namespace KuriousLabs.Management.KPIAnalysis.Tests.Unit;

/// <summary>
/// Unit tests for CollaborationMetricsService.
/// </summary>
public sealed class CollaborationMetricsServiceTests
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
    public async Task CalculateCollaborationMetricsAsync_WithNoProjects_ReturnsEmptyResult()
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

        var mockLogger = new Mock<ILogger<CollaborationMetricsService>>();
        var mockConfig = new Mock<IOptions<MetricsConfiguration>>();
        mockConfig.Setup(c => c.Value).Returns(CreateTestConfiguration());

        var service = new CollaborationMetricsService(mockHttpClient.Object, mockLogger.Object, mockConfig.Object);

        // Act
        var result = await service.CalculateCollaborationMetricsAsync(userId, windowDays);

        // Assert
        Assert.Equal(0, result.ReviewCommentsGiven);
        Assert.Equal(0, result.ReviewCommentsReceived);
        Assert.Equal(0, result.ApprovalsGiven);
        Assert.Equal(0, result.ResolvedDiscussionThreads);
        Assert.Equal(0, result.UnresolvedDiscussionThreads);
        Assert.Equal(0, result.SelfMergedMrsCount);
        Assert.Null(result.SelfMergedMrsRatio);
        Assert.Null(result.ReviewTurnaroundTimeMedianH);
        Assert.Null(result.ReviewDepthScoreAvgChars);
        Assert.Equal(0, result.TotalMrsCreated);
        Assert.Equal(0, result.TotalMrsMerged);
        Assert.Equal(0, result.MrsReviewed);
        Assert.Empty(result.Projects);
    }

    [Fact]
    public async Task CalculateCollaborationMetricsAsync_WithReviewCommentsGiven_CalculatesCorrectly()
    {
        // Arrange
        const long userId = 1;
        const long otherUserId = 2;
        const int windowDays = 30;

        var user = new GitLabUser
        {
            Id = userId,
            Username = "testuser",
            Name = "Test User",
            Email = "test@example.com"
        };

        var otherUser = new GitLabUser
        {
            Id = otherUserId,
            Username = "otheruser",
            Name = "Other User",
            Email = "other@example.com"
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

        // MR authored by other user
        var mr = new GitLabMergeRequest
        {
            Id = 1,
            Iid = 1,
            ProjectId = 100,
            Title = "MR 1",
            State = "merged",
            CreatedAt = now.AddDays(-10),
            MergedAt = now.AddDays(-8),
            Author = otherUser,
            TargetBranch = "main",
            SourceBranch = "feature/1"
        };

        // Comments by test user on other's MR
        var notes = new List<GitLabMergeRequestNote>
        {
            new GitLabMergeRequestNote
            {
                Id = 1,
                Body = "Good work!",
                Author = user,
                CreatedAt = now.AddDays(-9),
                System = false,
                Resolvable = false,
                Resolved = false
            },
            new GitLabMergeRequestNote
            {
                Id = 2,
                Body = "Consider refactoring this part.",
                Author = user,
                CreatedAt = now.AddDays(-9).AddHours(1),
                System = false,
                Resolvable = true,
                Resolved = true
            }
        };

        var mockHttpClient = new Mock<IGitLabHttpClient>();
        mockHttpClient.Setup(c => c.GetUserByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        mockHttpClient.Setup(c => c.GetUserContributedProjectsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { project });
        mockHttpClient.Setup(c => c.GetMergeRequestsAsync(100, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { mr });
        mockHttpClient.Setup(c => c.GetMergeRequestNotesAsync(100, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notes);
        mockHttpClient.Setup(c => c.GetMergeRequestDiscussionsAsync(100, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GitLabDiscussion>());
        mockHttpClient.Setup(c => c.GetMergeRequestApprovalsAsync(100, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GitLabMergeRequestApprovals?)null);

        var mockLogger = new Mock<ILogger<CollaborationMetricsService>>();
        var mockConfig = new Mock<IOptions<MetricsConfiguration>>();
        mockConfig.Setup(c => c.Value).Returns(CreateTestConfiguration());

        var service = new CollaborationMetricsService(mockHttpClient.Object, mockLogger.Object, mockConfig.Object);

        // Act
        var result = await service.CalculateCollaborationMetricsAsync(userId, windowDays);

        // Assert
        Assert.Equal(2, result.ReviewCommentsGiven);
        Assert.Equal(1, result.MrsReviewed);
        Assert.Equal(0, result.ReviewCommentsReceived);
        Assert.Equal(0, result.TotalMrsCreated);
        Assert.NotNull(result.ReviewTurnaroundTimeMedianH);
        Assert.True(result.ReviewTurnaroundTimeMedianH > 0);
        Assert.NotNull(result.ReviewDepthScoreAvgChars);
    }

    [Fact]
    public async Task CalculateCollaborationMetricsAsync_WithSelfMergedMRs_CalculatesCorrectly()
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

        // Self-merged MR (no approvals, no external comments)
        var selfMergedMr = new GitLabMergeRequest
        {
            Id = 1,
            Iid = 1,
            ProjectId = 100,
            Title = "Self-merged MR",
            State = "merged",
            CreatedAt = now.AddDays(-10),
            MergedAt = now.AddDays(-8),
            Author = user,
            TargetBranch = "main",
            SourceBranch = "feature/1"
        };

        // Reviewed MR (with external comments)
        var reviewedMr = new GitLabMergeRequest
        {
            Id = 2,
            Iid = 2,
            ProjectId = 100,
            Title = "Reviewed MR",
            State = "merged",
            CreatedAt = now.AddDays(-6),
            MergedAt = now.AddDays(-5),
            Author = user,
            TargetBranch = "main",
            SourceBranch = "feature/2"
        };

        var otherUser = new GitLabUser
        {
            Id = 2,
            Username = "reviewer",
            Name = "Reviewer",
            Email = "reviewer@example.com"
        };

        var selfMergedNotes = new List<GitLabMergeRequestNote>
        {
            new GitLabMergeRequestNote
            {
                Id = 1,
                Body = "Self comment",
                Author = user,
                CreatedAt = now.AddDays(-9),
                System = false,
                Resolvable = false,
                Resolved = false
            }
        };

        var reviewedNotes = new List<GitLabMergeRequestNote>
        {
            new GitLabMergeRequestNote
            {
                Id = 2,
                Body = "Looks good!",
                Author = otherUser,
                CreatedAt = now.AddDays(-5).AddHours(2),
                System = false,
                Resolvable = false,
                Resolved = false
            }
        };

        var mockHttpClient = new Mock<IGitLabHttpClient>();
        mockHttpClient.Setup(c => c.GetUserByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        mockHttpClient.Setup(c => c.GetUserContributedProjectsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { project });
        mockHttpClient.Setup(c => c.GetMergeRequestsAsync(100, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { selfMergedMr, reviewedMr });
        mockHttpClient.Setup(c => c.GetMergeRequestNotesAsync(100, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(selfMergedNotes);
        mockHttpClient.Setup(c => c.GetMergeRequestNotesAsync(100, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reviewedNotes);
        mockHttpClient.Setup(c => c.GetMergeRequestDiscussionsAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GitLabDiscussion>());
        mockHttpClient.Setup(c => c.GetMergeRequestApprovalsAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GitLabMergeRequestApprovals?)null);

        var mockLogger = new Mock<ILogger<CollaborationMetricsService>>();
        var mockConfig = new Mock<IOptions<MetricsConfiguration>>();
        mockConfig.Setup(c => c.Value).Returns(CreateTestConfiguration());

        var service = new CollaborationMetricsService(mockHttpClient.Object, mockLogger.Object, mockConfig.Object);

        // Act
        var result = await service.CalculateCollaborationMetricsAsync(userId, windowDays);

        // Assert
        Assert.Equal(2, result.TotalMrsCreated);
        Assert.Equal(2, result.TotalMrsMerged);
        Assert.Equal(1, result.SelfMergedMrsCount);
        Assert.Equal(0.5m, result.SelfMergedMrsRatio);
        Assert.Equal(1, result.ReviewCommentsReceived);
    }

    [Fact]
    public async Task CalculateCollaborationMetricsAsync_WithDiscussionThreads_CalculatesCorrectly()
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

        var mr = new GitLabMergeRequest
        {
            Id = 1,
            Iid = 1,
            ProjectId = 100,
            Title = "MR with discussions",
            State = "merged",
            CreatedAt = now.AddDays(-10),
            MergedAt = now.AddDays(-8),
            Author = user,
            TargetBranch = "main",
            SourceBranch = "feature/1"
        };

        var otherUser = new GitLabUser
        {
            Id = 2,
            Username = "reviewer",
            Name = "Reviewer",
            Email = "reviewer@example.com"
        };

        var discussions = new List<GitLabDiscussion>
        {
            // Resolved discussion thread
            new GitLabDiscussion
            {
                Id = "discussion1",
                IndividualNote = false,
                Notes = new List<GitLabMergeRequestNote>
                {
                    new GitLabMergeRequestNote
                    {
                        Id = 1,
                        Body = "Comment 1",
                        Author = otherUser,
                        CreatedAt = now.AddDays(-9),
                        System = false,
                        Resolvable = true,
                        Resolved = true
                    }
                }
            },
            // Unresolved discussion thread
            new GitLabDiscussion
            {
                Id = "discussion2",
                IndividualNote = false,
                Notes = new List<GitLabMergeRequestNote>
                {
                    new GitLabMergeRequestNote
                    {
                        Id = 2,
                        Body = "Comment 2",
                        Author = otherUser,
                        CreatedAt = now.AddDays(-8),
                        System = false,
                        Resolvable = true,
                        Resolved = false
                    }
                }
            },
            // Individual note (not a thread)
            new GitLabDiscussion
            {
                Id = "discussion3",
                IndividualNote = true,
                Notes = new List<GitLabMergeRequestNote>
                {
                    new GitLabMergeRequestNote
                    {
                        Id = 3,
                        Body = "Individual comment",
                        Author = otherUser,
                        CreatedAt = now.AddDays(-8),
                        System = false,
                        Resolvable = false,
                        Resolved = false
                    }
                }
            }
        };

        var mockHttpClient = new Mock<IGitLabHttpClient>();
        mockHttpClient.Setup(c => c.GetUserByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        mockHttpClient.Setup(c => c.GetUserContributedProjectsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { project });
        mockHttpClient.Setup(c => c.GetMergeRequestsAsync(100, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { mr });
        mockHttpClient.Setup(c => c.GetMergeRequestNotesAsync(100, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(discussions.SelectMany(d => d.Notes ?? new List<GitLabMergeRequestNote>()).ToList());
        mockHttpClient.Setup(c => c.GetMergeRequestDiscussionsAsync(100, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(discussions);
        mockHttpClient.Setup(c => c.GetMergeRequestApprovalsAsync(100, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GitLabMergeRequestApprovals?)null);

        var mockLogger = new Mock<ILogger<CollaborationMetricsService>>();
        var mockConfig = new Mock<IOptions<MetricsConfiguration>>();
        mockConfig.Setup(c => c.Value).Returns(CreateTestConfiguration());

        var service = new CollaborationMetricsService(mockHttpClient.Object, mockLogger.Object, mockConfig.Object);

        // Act
        var result = await service.CalculateCollaborationMetricsAsync(userId, windowDays);

        // Assert
        Assert.Equal(1, result.ResolvedDiscussionThreads);
        Assert.Equal(1, result.UnresolvedDiscussionThreads);
    }

    [Fact]
    public async Task CalculateCollaborationMetricsAsync_WithApprovals_CalculatesCorrectly()
    {
        // Arrange
        const long userId = 1;
        const long otherUserId = 2;
        const int windowDays = 30;

        var user = new GitLabUser
        {
            Id = userId,
            Username = "testuser",
            Name = "Test User",
            Email = "test@example.com"
        };

        var otherUser = new GitLabUser
        {
            Id = otherUserId,
            Username = "otheruser",
            Name = "Other User",
            Email = "other@example.com"
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

        // MR authored by other user, approved by test user
        var mr = new GitLabMergeRequest
        {
            Id = 1,
            Iid = 1,
            ProjectId = 100,
            Title = "MR 1",
            State = "merged",
            CreatedAt = now.AddDays(-10),
            MergedAt = now.AddDays(-8),
            Author = otherUser,
            TargetBranch = "main",
            SourceBranch = "feature/1"
        };

        var approvals = new GitLabMergeRequestApprovals
        {
            Id = 1,
            Iid = 1,
            ProjectId = 100,
            ApprovalsRequired = 1,
            ApprovalsLeft = 0,
            Approved = true,
            ApprovedBy = new List<GitLabApprover>
            {
                new GitLabApprover
                {
                    User = user
                }
            }
        };

        var mockHttpClient = new Mock<IGitLabHttpClient>();
        mockHttpClient.Setup(c => c.GetUserByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        mockHttpClient.Setup(c => c.GetUserContributedProjectsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { project });
        mockHttpClient.Setup(c => c.GetMergeRequestsAsync(100, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { mr });
        mockHttpClient.Setup(c => c.GetMergeRequestNotesAsync(100, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GitLabMergeRequestNote>());
        mockHttpClient.Setup(c => c.GetMergeRequestDiscussionsAsync(100, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GitLabDiscussion>());
        mockHttpClient.Setup(c => c.GetMergeRequestApprovalsAsync(100, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(approvals);

        var mockLogger = new Mock<ILogger<CollaborationMetricsService>>();
        var mockConfig = new Mock<IOptions<MetricsConfiguration>>();
        mockConfig.Setup(c => c.Value).Returns(CreateTestConfiguration());

        var service = new CollaborationMetricsService(mockHttpClient.Object, mockLogger.Object, mockConfig.Object);

        // Act
        var result = await service.CalculateCollaborationMetricsAsync(userId, windowDays);

        // Assert
        Assert.Equal(1, result.ApprovalsGiven);
    }

    [Fact]
    public async Task CalculateCollaborationMetricsAsync_WithBotComments_FiltersCorrectly()
    {
        // Arrange
        const long userId = 1;
        const long otherUserId = 2;
        const long botUserId = 999;
        const int windowDays = 30;

        var user = new GitLabUser
        {
            Id = userId,
            Username = "testuser",
            Name = "Test User",
            Email = "test@example.com"
        };

        var otherUser = new GitLabUser
        {
            Id = otherUserId,
            Username = "otheruser",
            Name = "Other User",
            Email = "other@example.com"
        };

        var botUser = new GitLabUser
        {
            Id = botUserId,
            Username = "gitlab-bot",
            Name = "GitLab Bot",
            Email = "bot@gitlab.com"
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

        var mr = new GitLabMergeRequest
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

        var notes = new List<GitLabMergeRequestNote>
        {
            new GitLabMergeRequestNote
            {
                Id = 1,
                Body = "Real comment",
                Author = otherUser,
                CreatedAt = now.AddDays(-9),
                System = false,
                Resolvable = false,
                Resolved = false
            },
            new GitLabMergeRequestNote
            {
                Id = 2,
                Body = "Bot comment",
                Author = botUser,
                CreatedAt = now.AddDays(-9),
                System = false,
                Resolvable = false,
                Resolved = false
            }
        };

        var mockHttpClient = new Mock<IGitLabHttpClient>();
        mockHttpClient.Setup(c => c.GetUserByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        mockHttpClient.Setup(c => c.GetUserContributedProjectsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { project });
        mockHttpClient.Setup(c => c.GetMergeRequestsAsync(100, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { mr });
        mockHttpClient.Setup(c => c.GetMergeRequestNotesAsync(100, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notes);
        mockHttpClient.Setup(c => c.GetMergeRequestDiscussionsAsync(100, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GitLabDiscussion>());
        mockHttpClient.Setup(c => c.GetMergeRequestApprovalsAsync(100, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GitLabMergeRequestApprovals?)null);

        var mockLogger = new Mock<ILogger<CollaborationMetricsService>>();
        var mockConfig = new Mock<IOptions<MetricsConfiguration>>();
        mockConfig.Setup(c => c.Value).Returns(CreateTestConfiguration());

        var service = new CollaborationMetricsService(mockHttpClient.Object, mockLogger.Object, mockConfig.Object);

        // Act
        var result = await service.CalculateCollaborationMetricsAsync(userId, windowDays);

        // Assert
        // Should only count the real comment, not the bot comment
        Assert.Equal(1, result.ReviewCommentsReceived);
    }

    [Fact]
    public async Task CalculateCollaborationMetricsAsync_WithInvalidWindowDays_ThrowsArgumentException()
    {
        // Arrange
        const long userId = 1;
        const int invalidWindowDays = 0;

        var mockHttpClient = new Mock<IGitLabHttpClient>();
        var mockLogger = new Mock<ILogger<CollaborationMetricsService>>();
        var mockConfig = new Mock<IOptions<MetricsConfiguration>>();
        mockConfig.Setup(c => c.Value).Returns(CreateTestConfiguration());

        var service = new CollaborationMetricsService(mockHttpClient.Object, mockLogger.Object, mockConfig.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.CalculateCollaborationMetricsAsync(userId, invalidWindowDays));
    }

    [Fact]
    public async Task CalculateCollaborationMetricsAsync_WithNonExistentUser_ThrowsInvalidOperationException()
    {
        // Arrange
        const long userId = 999;
        const int windowDays = 30;

        var mockHttpClient = new Mock<IGitLabHttpClient>();
        mockHttpClient.Setup(c => c.GetUserByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GitLabUser?)null);

        var mockLogger = new Mock<ILogger<CollaborationMetricsService>>();
        var mockConfig = new Mock<IOptions<MetricsConfiguration>>();
        mockConfig.Setup(c => c.Value).Returns(CreateTestConfiguration());

        var service = new CollaborationMetricsService(mockHttpClient.Object, mockLogger.Object, mockConfig.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CalculateCollaborationMetricsAsync(userId, windowDays));
    }
}
