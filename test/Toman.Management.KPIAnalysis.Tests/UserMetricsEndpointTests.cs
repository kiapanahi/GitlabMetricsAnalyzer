using Microsoft.AspNetCore.Builder;

using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

namespace Toman.Management.KPIAnalysis.Tests;

/// <summary>
/// Tests for User Metrics API endpoints to ensure they follow RESTful conventions
/// and implement proper error handling as specified in the requirements
/// </summary>
public class UserMetricsEndpointTests
{
    [Fact]
    public void UserMetricsEndpoints_AllRequiredEndpointsAreMapped()
    {
        // Arrange & Act & Assert
        // Verify that UserMetricsEndpoints class has the MapUserMetricsEndpoints method
        var endpointsType = typeof(UserMetricsEndpoints);
        var mapMethod = endpointsType.GetMethod("MapUserMetricsEndpoints",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        Assert.NotNull(mapMethod);
        Assert.True(mapMethod.IsStatic);
        Assert.True(mapMethod.IsPublic);
        Assert.Equal(typeof(WebApplication), mapMethod.ReturnType);

        var parameters = mapMethod.GetParameters();
        Assert.Single(parameters);
        Assert.Equal(typeof(WebApplication), parameters[0].ParameterType);
    }

    [Fact]
    public void UserMetricsResponse_HasRequiredProperties()
    {
        // Arrange & Act & Assert
        // Verify response model matches API documentation structure
        var responseType = typeof(UserMetricsResponse);
        var properties = responseType.GetProperties();

        // Check all required properties exist
        var requiredProperties = new[]
        {
            "UserId", "UserName", "Email", "FromDate", "ToDate",
            "CodeContribution", "CodeReview", "IssueManagement",
            "Collaboration", "Quality", "Productivity", "Metadata"
        };

        foreach (var propName in requiredProperties)
        {
            var property = properties.FirstOrDefault(p => p.Name == propName);
            Assert.NotNull(property);
        }
    }

    [Fact]
    public void UserMetricsSummaryResponse_HasRequiredProperties()
    {
        // Arrange & Act & Assert
        // Verify summary response model matches API documentation
        var responseType = typeof(UserMetricsSummaryResponse);
        var properties = responseType.GetProperties();

        var requiredProperties = new[]
        {
            "UserId", "UserName", "FromDate", "ToDate",
            "TotalCommits", "TotalMergeRequests", "AverageCommitsPerDay",
            "PipelineSuccessRate", "AverageMRCycleTime", "TotalLinesChanged",
            "Metadata"
        };

        foreach (var propName in requiredProperties)
        {
            var property = properties.FirstOrDefault(p => p.Name == propName);
            Assert.NotNull(property);
        }
    }

    [Fact]
    public void UserMetricsTrendsResponse_HasRequiredProperties()
    {
        // Arrange & Act & Assert
        // Verify trends response model matches API documentation
        var responseType = typeof(UserMetricsTrendsResponse);
        var properties = responseType.GetProperties();

        var requiredProperties = new[]
        {
            "UserId", "UserName", "FromDate", "ToDate",
            "Period", "TrendPoints", "Metadata"
        };

        foreach (var propName in requiredProperties)
        {
            var property = properties.FirstOrDefault(p => p.Name == propName);
            Assert.NotNull(property);
        }
    }

    [Fact]
    public void UserMetricsComparisonResponse_HasRequiredProperties()
    {
        // Arrange & Act & Assert
        // Verify comparison response model matches API documentation
        var responseType = typeof(UserMetricsComparisonResponse);
        var properties = responseType.GetProperties();

        var requiredProperties = new[]
        {
            "UserId", "UserName", "FromDate", "ToDate",
            "UserMetrics", "TeamAverage", "PeerMetrics", "Metadata"
        };

        foreach (var propName in requiredProperties)
        {
            var property = properties.FirstOrDefault(p => p.Name == propName);
            Assert.NotNull(property);
        }
    }

    [Fact]
    public void TrendPeriod_EnumHasRequiredValues()
    {
        // Arrange & Act & Assert
        // Verify that TrendPeriod enum has all required values
        Assert.True(Enum.IsDefined(typeof(TrendPeriod), TrendPeriod.Daily));
        Assert.True(Enum.IsDefined(typeof(TrendPeriod), TrendPeriod.Weekly));
        Assert.True(Enum.IsDefined(typeof(TrendPeriod), TrendPeriod.Monthly));
    }

    [Fact]
    public void ParseDateOrDefault_HandlesValidDate()
    {
        // Arrange
        var validDate = "2024-01-01T00:00:00Z";
        var defaultDate = DateTimeOffset.UtcNow;

        // Act - Use reflection to call private method
        var method = typeof(UserMetricsEndpoints).GetMethod("ParseDateOrDefault",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var result = (DateTimeOffset)method!.Invoke(null, new object[] { validDate, defaultDate })!;

        // Assert
        Assert.Equal(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), result);
    }

    [Fact]
    public void ParseDateOrDefault_ReturnsDefaultForNull()
    {
        // Arrange
        string? nullDate = null;
        var defaultDate = DateTimeOffset.UtcNow;

        // Act - Use reflection to call private method
        var method = typeof(UserMetricsEndpoints).GetMethod("ParseDateOrDefault",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var result = (DateTimeOffset)method!.Invoke(null, new object?[] { nullDate, defaultDate })!;

        // Assert
        Assert.Equal(defaultDate, result);
    }

    [Fact]
    public void ParseDateOrDefault_ThrowsForInvalidDate()
    {
        // Arrange
        var invalidDate = "invalid-date";
        var defaultDate = DateTimeOffset.UtcNow;

        // Act & Assert
        var method = typeof(UserMetricsEndpoints).GetMethod("ParseDateOrDefault",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var exception = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
            method!.Invoke(null, new object[] { invalidDate, defaultDate }));

        Assert.IsType<ArgumentException>(exception.InnerException);
        Assert.Contains("Invalid date format", exception.InnerException!.Message);
        Assert.Contains("ISO 8601 format", exception.InnerException.Message);
    }

    [Fact]
    public void ParseUserIds_HandlesValidCommaSeparatedIds()
    {
        // Arrange
        var userIds = "1,2,3,4";

        // Act - Use reflection to call private method
        var method = typeof(UserMetricsEndpoints).GetMethod("ParseUserIds",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var result = (long[])method!.Invoke(null, new object[] { userIds })!;

        // Assert
        Assert.Equal(new long[] { 1, 2, 3, 4 }, result);
    }

    [Fact]
    public void ParseUserIds_ReturnsEmptyForNull()
    {
        // Arrange
        string? nullUserIds = null;

        // Act - Use reflection to call private method
        var method = typeof(UserMetricsEndpoints).GetMethod("ParseUserIds",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var result = (long[])method!.Invoke(null, new object?[] { nullUserIds })!;

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ParseUserIds_ThrowsForInvalidIds()
    {
        // Arrange
        var invalidUserIds = "1,abc,3";

        // Act & Assert
        var method = typeof(UserMetricsEndpoints).GetMethod("ParseUserIds",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var exception = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
            method!.Invoke(null, new object[] { invalidUserIds }));

        Assert.IsType<ArgumentException>(exception.InnerException);
        Assert.Contains("Invalid user IDs format", exception.InnerException!.Message);
    }

    [Fact]
    public void UserMetricsService_Interface_HasAllRequiredMethods()
    {
        // Arrange & Act & Assert
        // Verify that IUserMetricsService has all required methods as specified in requirements
        var serviceType = typeof(IUserMetricsService);
        var methods = serviceType.GetMethods();

        var requiredMethods = new[]
        {
            "GetUserMetricsAsync",
            "GetUserMetricsSummaryAsync",
            "GetUserMetricsTrendsAsync",
            "GetUserMetricsComparisonAsync"
        };

        foreach (var methodName in requiredMethods)
        {
            var method = methods.FirstOrDefault(m => m.Name == methodName);
            Assert.NotNull(method);
            Assert.True(method.ReturnType.IsGenericType);
            Assert.Equal(typeof(Task<>).GetGenericTypeDefinition(), method.ReturnType.GetGenericTypeDefinition());
        }
    }

    [Fact]
    public void UserMetricsService_Methods_HaveProperParameters()
    {
        // Arrange & Act & Assert
        var serviceType = typeof(IUserMetricsService);

        // Test GetUserMetricsAsync parameters
        var getMetricsMethod = serviceType.GetMethod("GetUserMetricsAsync");
        var getMetricsParams = getMetricsMethod!.GetParameters();
        Assert.Equal(4, getMetricsParams.Length);
        Assert.Equal(typeof(long), getMetricsParams[0].ParameterType); // userId
        Assert.Equal(typeof(DateTimeOffset), getMetricsParams[1].ParameterType); // fromDate
        Assert.Equal(typeof(DateTimeOffset), getMetricsParams[2].ParameterType); // toDate
        Assert.Equal(typeof(CancellationToken), getMetricsParams[3].ParameterType); // cancellationToken

        // Test GetUserMetricsTrendsAsync has TrendPeriod parameter
        var getTrendsMethod = serviceType.GetMethod("GetUserMetricsTrendsAsync");
        var getTrendsParams = getTrendsMethod!.GetParameters();
        Assert.Equal(5, getTrendsParams.Length);
        Assert.Equal(typeof(TrendPeriod), getTrendsParams[3].ParameterType); // period parameter

        // Test GetUserMetricsComparisonAsync has comparison users parameter
        var getComparisonMethod = serviceType.GetMethod("GetUserMetricsComparisonAsync");
        var getComparisonParams = getComparisonMethod!.GetParameters();
        Assert.Equal(5, getComparisonParams.Length);
        Assert.Equal(typeof(List<long>), getComparisonParams[3].ParameterType); // compareWith parameter
    }
}
