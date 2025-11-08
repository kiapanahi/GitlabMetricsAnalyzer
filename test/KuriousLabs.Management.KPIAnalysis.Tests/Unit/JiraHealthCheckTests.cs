using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Moq;
using KuriousLabs.Management.KPIAnalysis.ApiService.Features.JiraMetrics.HealthChecks;
using KuriousLabs.Management.KPIAnalysis.ApiService.Features.JiraMetrics.Infrastructure;
using KuriousLabs.Management.KPIAnalysis.ApiService.Features.JiraMetrics.Infrastructure.DTOs;

namespace KuriousLabs.Management.KPIAnalysis.Tests.Unit;

/// <summary>
/// Unit tests for JiraHealthCheck
/// </summary>
public sealed class JiraHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_WhenJiraIsReachable_ReturnsHealthy()
    {
        // Arrange
        var mockHttpClient = new Mock<IJiraHttpClient>();
        var mockLogger = new Mock<ILogger<JiraHealthCheck>>();
        var healthCheck = new JiraHealthCheck(mockHttpClient.Object, mockLogger.Object);

        var serverInfo = new JiraServerInfo
        {
            Version = "9.12.4",
            BuildNumber = 912004,
            ServerTitle = "Jira"
        };

        mockHttpClient
            .Setup(x => x.GetServerInfoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(serverInfo);

        // Act
        var result = await healthCheck.CheckHealthAsync(
            new HealthCheckContext(),
            CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.NotNull(result.Data);
        Assert.Contains("version", result.Data.Keys);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenJiraIsUnreachable_ReturnsUnhealthy()
    {
        // Arrange
        var mockHttpClient = new Mock<IJiraHttpClient>();
        var mockLogger = new Mock<ILogger<JiraHealthCheck>>();
        var healthCheck = new JiraHealthCheck(mockHttpClient.Object, mockLogger.Object);

        mockHttpClient
            .Setup(x => x.GetServerInfoAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection failed"));

        // Act
        var result = await healthCheck.CheckHealthAsync(
            new HealthCheckContext(),
            CancellationToken.None);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.NotNull(result.Exception);
    }
}
