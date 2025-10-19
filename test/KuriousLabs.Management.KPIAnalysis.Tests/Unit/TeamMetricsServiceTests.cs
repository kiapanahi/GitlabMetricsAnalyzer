using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

using KuriousLabs.Management.KPIAnalysis.ApiService.Configuration;
using KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure;
using KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;
using KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

using RawGitLabCommit = KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw.GitLabCommit;
using RawGitLabMergeRequestApprovals = KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw.GitLabMergeRequestApprovals;
using RawGitLabMergeRequestChanges = KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw.GitLabMergeRequestChanges;

namespace KuriousLabs.Management.KPIAnalysis.Tests.Unit;

/// <summary>
/// Unit tests for TeamMetricsService.
/// </summary>
public sealed class TeamMetricsServiceTests
{
    [Fact]
    public async Task CalculateTeamMetricsAsync_WithValidTeam_ReturnsMetrics()
    {
        // Arrange
        const string teamId = "backend-team";
        const int windowDays = 30;

        var team = new TeamDefinition
        {
            Id = teamId,
            Name = "Backend Team",
            Members = [1, 2]
        };

        var metricsConfig = new MetricsConfiguration
        {
            Identity = new IdentityConfiguration(),
            Excludes = new ExclusionConfiguration(),
            TeamMapping = new TeamMappingConfiguration
            {
                Teams = [team]
            }
        };

        var mockHttpClient = new Mock<IGitLabHttpClient>();
        var mockOptions = new Mock<IOptions<MetricsConfiguration>>();
        var mockLogger = new Mock<ILogger<TeamMetricsService>>();

        mockOptions.Setup(o => o.Value).Returns(metricsConfig);

        // Setup mock responses
        var contributedProjects = new List<GitLabContributedProject>
        {
            new GitLabContributedProject
            {
                Id = 100,
                Name = "test-project",
                Path = "test-project",
                PathWithNamespace = "company/test-project",
                NameWithNamespace = "Company / Test Project",
                DefaultBranch = "main",
                WebUrl = "https://gitlab.example.com/company/test-project",
                ForksCount = 0
            }
        };

        mockHttpClient
            .Setup(x => x.GetUserContributedProjectsAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(contributedProjects);

        mockHttpClient
            .Setup(x => x.GetProjectsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitLabProject>
            {
                new GitLabProject
                {
                    Id = 100,
                    Name = "test-project",
                    PathWithNamespace = "company/test-project"
                }
            });

        var now = DateTime.UtcNow;
        var mergeRequests = new List<GitLabMergeRequest>
        {
            new GitLabMergeRequest
            {
                Id = 1,
                Iid = 1,
                ProjectId = 100,
                Title = "Test MR",
                State = "merged",
                CreatedAt = now.AddDays(-5),
                MergedAt = now.AddDays(-4),
                Author = new GitLabUser { Id = 1, Username = "user1" }
            }
        };

        mockHttpClient
            .Setup(x => x.GetMergeRequestsAsync(It.IsAny<long>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mergeRequests);

        mockHttpClient
            .Setup(x => x.GetMergeRequestApprovalsAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RawGitLabMergeRequestApprovals?)null);

        mockHttpClient
            .Setup(x => x.GetCommitsAsync(It.IsAny<long>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RawGitLabCommit>
            {
                new RawGitLabCommit
                {
                    Id = "commit1",
                    ShortId = "abc123",
                    Title = "Test commit",
                    AuthorName = "Test User",
                    AuthorEmail = "test@example.com",
                    CommittedDate = now.AddDays(-5),
                    ProjectId = 100
                }
            });

        mockHttpClient
            .Setup(x => x.GetMergeRequestChangesAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RawGitLabMergeRequestChanges
            {
                Additions = 100,
                Deletions = 50,
                Total = 150
            });

        var service = new TeamMetricsService(mockHttpClient.Object, mockOptions.Object, mockLogger.Object);

        // Act
        var result = await service.CalculateTeamMetricsAsync(teamId, windowDays);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(teamId, result.TeamId);
        Assert.Equal("Backend Team", result.TeamName);
        Assert.Equal(2, result.MemberCount);
        Assert.Equal(windowDays, result.WindowDays);
        Assert.Equal(1, result.TotalCommits); // Should have 1 commit from mock
        Assert.Equal(150, result.TotalLinesChanged); // Should have 150 lines from mock (100 additions + 50 deletions)
        Assert.Single(result.ProjectActivities); // Should have 1 project
        Assert.Equal(1, result.ProjectActivities.First().CommitCount); // Should have 1 commit for that project
    }

    [Fact]
    public async Task CalculateTeamMetricsAsync_WithNonExistentTeam_ThrowsException()
    {
        // Arrange
        const string teamId = "non-existent-team";

        var metricsConfig = new MetricsConfiguration
        {
            Identity = new IdentityConfiguration(),
            Excludes = new ExclusionConfiguration(),
            TeamMapping = new TeamMappingConfiguration
            {
                Teams = []
            }
        };

        var mockHttpClient = new Mock<IGitLabHttpClient>();
        var mockOptions = new Mock<IOptions<MetricsConfiguration>>();
        var mockLogger = new Mock<ILogger<TeamMetricsService>>();

        mockOptions.Setup(o => o.Value).Returns(metricsConfig);

        var service = new TeamMetricsService(mockHttpClient.Object, mockOptions.Object, mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await service.CalculateTeamMetricsAsync(teamId, 30));
    }

    [Fact]
    public async Task CalculateTeamMetricsAsync_WithEmptyTeam_ReturnsEmptyMetrics()
    {
        // Arrange
        const string teamId = "empty-team";
        const int windowDays = 30;

        var team = new TeamDefinition
        {
            Id = teamId,
            Name = "Empty Team",
            Members = []
        };

        var metricsConfig = new MetricsConfiguration
        {
            Identity = new IdentityConfiguration(),
            Excludes = new ExclusionConfiguration(),
            TeamMapping = new TeamMappingConfiguration
            {
                Teams = [team]
            }
        };

        var mockHttpClient = new Mock<IGitLabHttpClient>();
        var mockOptions = new Mock<IOptions<MetricsConfiguration>>();
        var mockLogger = new Mock<ILogger<TeamMetricsService>>();

        mockOptions.Setup(o => o.Value).Returns(metricsConfig);

        var service = new TeamMetricsService(mockHttpClient.Object, mockOptions.Object, mockLogger.Object);

        // Act
        var result = await service.CalculateTeamMetricsAsync(teamId, windowDays);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(teamId, result.TeamId);
        Assert.Equal("Empty Team", result.TeamName);
        Assert.Equal(0, result.MemberCount);
        Assert.Equal(0, result.TotalMergedMrs);
        Assert.Equal(0, result.CrossProjectContributors);
    }
}
