using Microsoft.Extensions.Logging;
using Moq;
using KuriousLabs.Management.KPIAnalysis.ApiService.Features.JiraMetrics.Infrastructure;
using KuriousLabs.Management.KPIAnalysis.ApiService.Features.JiraMetrics.Infrastructure.DTOs;
using KuriousLabs.Management.KPIAnalysis.ApiService.Features.JiraMetrics.Services;

namespace KuriousLabs.Management.KPIAnalysis.Tests.Unit;

/// <summary>
/// Unit tests for Jira metrics services
/// </summary>
public sealed class JiraMetricsServicesTests
{
    [Fact]
    public async Task IssueTrackingMetricsService_WithValidData_ReturnsMetrics()
    {
        // Arrange
        var mockHttpClient = new Mock<IJiraHttpClient>();
        var mockLogger = new Mock<ILogger<IssueTrackingMetricsService>>();
        var service = new IssueTrackingMetricsService(mockHttpClient.Object, mockLogger.Object);

        const string projectKey = "MAIN";
        const int windowDays = 30;

        mockHttpClient
            .Setup(x => x.SearchIssuesAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JiraSearchResult<JiraIssue>
            {
                StartAt = 0,
                MaxResults = 50,
                Total = 0,
                Issues = []
            });

        // Act
        var result = await service.CalculateIssueTrackingMetricsAsync(projectKey, windowDays, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IssuesCreated >= 0);
        Assert.True(result.IssuesResolved >= 0);
        Assert.True(result.IssuesOpen >= 0);
    }

    [Fact]
    public async Task IssueTrackingMetricsService_WithNoIssues_ReturnsZeroMetrics()
    {
        // Arrange
        var mockHttpClient = new Mock<IJiraHttpClient>();
        var mockLogger = new Mock<ILogger<IssueTrackingMetricsService>>();
        var service = new IssueTrackingMetricsService(mockHttpClient.Object, mockLogger.Object);

        mockHttpClient
            .Setup(x => x.SearchIssuesAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JiraSearchResult<JiraIssue> { StartAt = 0, MaxResults = 50, Total = 0, Issues = [] });

        // Act
        var result = await service.CalculateIssueTrackingMetricsAsync("EMPTY", 30, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.IssuesCreated);
        Assert.Equal(0, result.IssuesResolved);
    }

    [Fact]
    public async Task UserJiraMetricsService_WithValidUser_ReturnsMetrics()
    {
        // Arrange
        var mockHttpClient = new Mock<IJiraHttpClient>();
        var mockLogger = new Mock<ILogger<UserJiraMetricsService>>();
        var service = new UserJiraMetricsService(mockHttpClient.Object, mockLogger.Object);

        const string accountId = "user-001";
        const int windowDays = 30;

        var user = new JiraUser
        {
            AccountId = "user-001",
            DisplayName = "Test User",
            EmailAddress = "test@example.com",
            Active = true
        };

        mockHttpClient
            .Setup(x => x.GetUserAsync(accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        mockHttpClient
            .Setup(x => x.SearchIssuesAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JiraSearchResult<JiraIssue>
            {
                StartAt = 0,
                MaxResults = 50,
                Total = 0,
                Issues = []
            });

        // Act
        var result = await service.CalculateUserMetricsAsync(accountId, windowDays, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(accountId, result.AccountId);
        Assert.Equal(user.DisplayName, result.DisplayName);
        Assert.True(result.IssuesAssigned >= 0);
    }

    [Fact]
    public async Task ProjectJiraMetricsService_WithValidProject_ReturnsMetrics()
    {
        // Arrange
        var mockHttpClient = new Mock<IJiraHttpClient>();
        var mockLogger = new Mock<ILogger<ProjectJiraMetricsService>>();
        var service = new ProjectJiraMetricsService(mockHttpClient.Object, mockLogger.Object);

        const string projectKey = "MAIN";
        const int windowDays = 30;

        var project = new JiraProject
        {
            Id = "10001",
            Key = projectKey,
            Name = "Main Service",
            ProjectTypeKey = "software",
            Lead = new JiraUser
            {
                AccountId = "user-001",
                DisplayName = "Project Lead",
                EmailAddress = "lead@example.com",
                Active = true
            }
        };

        mockHttpClient
            .Setup(x => x.GetProjectAsync(projectKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

        mockHttpClient
            .Setup(x => x.SearchIssuesAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JiraSearchResult<JiraIssue>
            {
                StartAt = 0,
                MaxResults = 50,
                Total = 0,
                Issues = []
            });

        // Act
        var result = await service.CalculateProjectMetricsAsync(projectKey, windowDays, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(projectKey, result.ProjectKey);
        Assert.True(result.BacklogSize >= 0);
        Assert.True(result.VelocityIssuesPerWeek >= 0);
    }
}
