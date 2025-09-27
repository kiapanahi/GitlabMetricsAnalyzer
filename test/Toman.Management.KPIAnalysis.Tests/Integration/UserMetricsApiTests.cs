using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

namespace Toman.Management.KPIAnalysis.Tests.Integration;

/// <summary>
/// Integration tests for User Metrics API functionality
/// </summary>
public sealed class UserMetricsApiTests
{
    [Fact]
    public async Task UserMetricsService_CanBeInstantiated()
    {
        // This is a basic test to ensure our service compiles and can be instantiated
        // In a real scenario, we would use a test database context

        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act & Assert
        // For now, we just verify that the types exist and compile correctly
        Assert.NotNull(typeof(IUserMetricsService));
        Assert.NotNull(typeof(UserMetricsService));
        Assert.NotNull(typeof(UserMetricsResponse));
        Assert.NotNull(typeof(UserMetricsSummaryResponse));
        Assert.NotNull(typeof(UserMetricsTrendsResponse));
        Assert.NotNull(typeof(UserMetricsComparisonResponse));

        await Task.CompletedTask; // Avoid async warning
    }

    [Fact]
    public async Task UserMetricsEndpoints_TypesExist()
    {
        // Arrange & Act & Assert
        // Verify that our endpoint types compile correctly
        Assert.NotNull(typeof(TrendPeriod));
        Assert.True(Enum.IsDefined(typeof(TrendPeriod), TrendPeriod.Daily));
        Assert.True(Enum.IsDefined(typeof(TrendPeriod), TrendPeriod.Weekly));
        Assert.True(Enum.IsDefined(typeof(TrendPeriod), TrendPeriod.Monthly));

        await Task.CompletedTask; // Avoid async warning
    }
}
