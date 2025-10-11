using Microsoft.Extensions.Logging;

using Moq;

using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

namespace Toman.Management.KPIAnalysis.Tests.Integration;

/// <summary>
/// Integration tests for PerDeveloperMetricsService with MockGitLabHttpClient.
/// </summary>
public sealed class PerDeveloperMetricsServiceIntegrationTests
{
    [Fact]
    public async Task CalculateMrCycleTimeAsync_WithMockClient_CalculatesMetrics()
    {
        // Arrange
        const long userId = 1; // Alice from MockGitLabHttpClient
        const int windowDays = 30;

        var mockClient = new MockGitLabHttpClient();
        var logger = Mock.Of<ILogger<PerDeveloperMetricsService>>();
        var service = new PerDeveloperMetricsService(mockClient, logger);

        // Act
        var result = await service.CalculateMrCycleTimeAsync(userId, windowDays, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(userId, result.UserId);
        Assert.Equal("alice.dev", result.Username);
        Assert.Equal(windowDays, result.WindowDays);
        
        // The mock client generates merge requests with random cycle times
        // We just verify that the service can process them without errors
        Assert.True(result.MergedMrCount >= 0);
        Assert.True(result.ExcludedMrCount >= 0);
        
        // If there were merged MRs, we should have a cycle time
        if (result.MergedMrCount > 0)
        {
            Assert.NotNull(result.MrCycleTimeP50H);
            Assert.True(result.MrCycleTimeP50H > 0);
        }
        
        // Verify projects are populated
        Assert.NotNull(result.Projects);
    }

    [Fact]
    public async Task CalculateMrCycleTimeAsync_WithMultipleUsers_ReturnsDistinctResults()
    {
        // Arrange
        var mockClient = new MockGitLabHttpClient();
        var logger = Mock.Of<ILogger<PerDeveloperMetricsService>>();
        var service = new PerDeveloperMetricsService(mockClient, logger);

        // Act - Calculate for two different users
        var result1 = await service.CalculateMrCycleTimeAsync(1, 30, TestContext.Current.CancellationToken); // Alice
        var result2 = await service.CalculateMrCycleTimeAsync(2, 30, TestContext.Current.CancellationToken); // Bob

        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.NotEqual(result1.UserId, result2.UserId);
        Assert.Equal("alice.dev", result1.Username);
        Assert.Equal("bob.smith", result2.Username);
    }
}
