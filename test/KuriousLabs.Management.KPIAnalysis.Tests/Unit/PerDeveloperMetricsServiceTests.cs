using Microsoft.Extensions.Logging;

using Moq;

using KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure;
using KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;
using KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

namespace KuriousLabs.Management.KPIAnalysis.Tests.Unit;

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

    [Fact]
    public async Task CalculateFlowMetricsAsync_WithMergedMRs_CalculatesAllMetricsCorrectly()
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

        var project1 = new GitLabContributedProject
        {
            Id = 100,
            Name = "project-1",
            Path = "project-1",
            PathWithNamespace = "company/project-1",
            NameWithNamespace = "Company / Project 1",
            DefaultBranch = "main",
            WebUrl = "https://gitlab.example.com/company/project-1",
            ForksCount = 0
        };

        var project2 = new GitLabContributedProject
        {
            Id = 200,
            Name = "project-2",
            Path = "project-2",
            PathWithNamespace = "company/project-2",
            NameWithNamespace = "Company / Project 2",
            DefaultBranch = "main",
            WebUrl = "https://gitlab.example.com/company/project-2",
            ForksCount = 0
        };

        var now = DateTime.UtcNow;
        
        // Create merged MRs
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

        var mr2 = new GitLabMergeRequest
        {
            Id = 2,
            Iid = 2,
            ProjectId = 200,
            Title = "MR 2",
            State = "merged",
            CreatedAt = now.AddDays(-14),
            MergedAt = now.AddDays(-10),
            Author = user,
            TargetBranch = "main",
            SourceBranch = "feature/2"
        };

        // Create open MRs
        var mr3 = new GitLabMergeRequest
        {
            Id = 3,
            Iid = 3,
            ProjectId = 100,
            Title = "MR 3 (open)",
            State = "opened",
            CreatedAt = now.AddDays(-2),
            MergedAt = null,
            Author = user,
            TargetBranch = "main",
            SourceBranch = "feature/3"
        };

        // Create commits with stats for line counting
        // MR1: First commit at -11 days, MR created at -10 days
        // Coding time calculation: (-10) - (-11) = 1 day = 24 hours
        var mr1Commits = new List<GitLabCommit>
        {
            new GitLabCommit
            {
                Id = "commit1",
                ShortId = "abc123",
                Title = "First commit for MR 1",
                AuthorName = "Test User",
                AuthorEmail = "test@example.com",
                CommittedDate = now.AddDays(-11),
                ProjectId = 100,
                Stats = new GitLabCommitStats { Additions = 50, Deletions = 10, Total = 60 }
            }
        };

        // MR2: First commit at -16 days, MR created at -14 days
        // Coding time calculation: (-14) - (-16) = 2 days = 48 hours
        var mr2Commits = new List<GitLabCommit>
        {
            new GitLabCommit
            {
                Id = "commit2",
                ShortId = "def456",
                Title = "First commit for MR 2",
                AuthorName = "Test User",
                AuthorEmail = "test@example.com",
                CommittedDate = now.AddDays(-16),
                ProjectId = 200,
                Stats = new GitLabCommitStats { Additions = 100, Deletions = 20, Total = 120 }
            }
        };

        // Create notes for review metrics
        var reviewer = new GitLabUser
        {
            Id = 2,
            Username = "reviewer",
            Name = "Reviewer User"
        };

        // MR1: MR created at -10 days, first review at -9.5 days
        // Time to first review calculation: (-9.5) - (-10) = 0.5 days = 12 hours
        var mr1Notes = new List<GitLabMergeRequestNote>
        {
            new GitLabMergeRequestNote
            {
                Id = 1,
                Author = reviewer,
                Body = "Looks good!",
                CreatedAt = now.AddDays(-9.5),
                System = false
            }
        };

        // MR2: MR created at -14 days, first review at -13 days
        // Time to first review calculation: (-13) - (-14) = 1 day = 24 hours
        var mr2Notes = new List<GitLabMergeRequestNote>
        {
            new GitLabMergeRequestNote
            {
                Id = 2,
                Author = reviewer,
                Body = "Some comments",
                CreatedAt = now.AddDays(-13),
                System = false
            }
        };

        // Setup mocks
        var mockGitLabClient = new Mock<IGitLabHttpClient>();
        
        mockGitLabClient
            .Setup(x => x.GetUserByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        mockGitLabClient
            .Setup(x => x.GetUserContributedProjectsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitLabContributedProject> { project1, project2 });

        mockGitLabClient
            .Setup(x => x.GetMergeRequestsAsync(100, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitLabMergeRequest> { mr1, mr3 });

        mockGitLabClient
            .Setup(x => x.GetMergeRequestsAsync(200, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitLabMergeRequest> { mr2 });

        mockGitLabClient
            .Setup(x => x.GetMergeRequestCommitsAsync(100, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mr1Commits);

        mockGitLabClient
            .Setup(x => x.GetMergeRequestCommitsAsync(200, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mr2Commits);

        mockGitLabClient
            .Setup(x => x.GetMergeRequestNotesAsync(100, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mr1Notes);

        mockGitLabClient
            .Setup(x => x.GetMergeRequestNotesAsync(200, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mr2Notes);

        var logger = Mock.Of<ILogger<PerDeveloperMetricsService>>();
        var service = new PerDeveloperMetricsService(mockGitLabClient.Object, logger);

        // Act
        var result = await service.CalculateFlowMetricsAsync(userId, windowDays, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(userId, result.UserId);
        Assert.Equal("testuser", result.Username);
        Assert.Equal(windowDays, result.WindowDays);
        
        // Metric 1: Merged MRs Count
        Assert.Equal(2, result.MergedMrsCount);
        
        // Metric 2: Lines Changed (50+10 + 100+20 = 180)
        Assert.Equal(180, result.LinesChanged);
        
        // Metric 3: Coding Time Median (median of 24h and 48h = 36h)
        Assert.NotNull(result.CodingTimeMedianH);
        Assert.Equal(36m, result.CodingTimeMedianH.Value);
        
        // Metric 4: Time to First Review Median (median of 12h and 24h = 18h)
        Assert.NotNull(result.TimeToFirstReviewMedianH);
        Assert.Equal(18m, result.TimeToFirstReviewMedianH.Value);
        
        // Metric 5: Review Time Median - Not available without approval API
        Assert.Null(result.ReviewTimeMedianH);
        
        // Metric 6: Merge Time Median - Using MR created → merged
        // MR1: created at -10 days, merged at -8 days
        //   Calculation: (-8) - (-10) = 2 days = 48 hours
        // MR2: created at -14 days, merged at -10 days
        //   Calculation: (-10) - (-14) = 4 days = 96 hours
        // Median of [48h, 96h] = 72h
        Assert.NotNull(result.MergeTimeMedianH);
        Assert.Equal(72m, result.MergeTimeMedianH.Value);
        
        // Metric 7: WIP/Open MRs Count
        Assert.Equal(1, result.WipOpenMrsCount);
        
        // Metric 8: Context Switching Index (2 projects with merged MRs)
        Assert.Equal(2, result.ContextSwitchingIndex);
        
        // Projects
        Assert.Equal(2, result.Projects.Count);
    }

    [Fact]
    public async Task CalculateFlowMetricsAsync_WithNoMRs_ReturnsEmptyResult()
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
        var result = await service.CalculateFlowMetricsAsync(userId, windowDays, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(0, result.MergedMrsCount);
        Assert.Equal(0, result.LinesChanged);
        Assert.Null(result.CodingTimeMedianH);
        Assert.Null(result.TimeToFirstReviewMedianH);
        Assert.Null(result.ReviewTimeMedianH);
        Assert.Null(result.MergeTimeMedianH);
        Assert.Equal(0, result.WipOpenMrsCount);
        Assert.Equal(0, result.ContextSwitchingIndex);
        Assert.Empty(result.Projects);
    }

    [Fact]
    public async Task CalculateFlowMetricsAsync_WithInvalidUserId_ThrowsException()
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
            async () => await service.CalculateFlowMetricsAsync(userId, windowDays, TestContext.Current.CancellationToken)
        );
    }
}
