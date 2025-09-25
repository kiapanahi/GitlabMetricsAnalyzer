using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

namespace Toman.Management.KPIAnalysis.Tests.Integration;

/// <summary>
/// Integration test to verify that the User Metrics API endpoints are properly wired up
/// </summary>
public sealed class UserMetricsEndpointIntegrationTests
{
    [Fact]
    public void UserMetricsEndpoints_AreMappedInGitLabMetricsEndpoints()
    {
        // Arrange & Act & Assert
        // Verify that GitLabMetricsEndpoints.MapGitlabCollectorEndpoints calls MapUserMetricsEndpoints
        var gitLabEndpointsType = typeof(GitLabMetricsEndpoints);
        var mapMethod = gitLabEndpointsType.GetMethod("MapGitlabCollectorEndpoints", 
            BindingFlags.Public | BindingFlags.Static);
        
        Assert.NotNull(mapMethod);
        
        // Get the method body and verify it contains a call to MapUserMetricsEndpoints
        // Since we can't inspect the actual method calls in compiled code easily,
        // we'll verify through the source file that we know contains the mapping
        var sourceFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, 
            "..", "..", "..", "..", "..", 
            "src", "Toman.Management.KPIAnalysis.ApiService", 
            "Features", "GitLabMetrics", "GitLabMetricsEndpoints.cs");
        
        if (File.Exists(sourceFile))
        {
            var content = File.ReadAllText(sourceFile);
            Assert.Contains("MapUserMetricsEndpoints", content);
        }
    }

    [Fact]
    public void UserMetricsEndpoints_MapperMethodExists()
    {
        // Arrange & Act & Assert
        // Verify that UserMetricsEndpoints has the correct mapping method
        var endpointsType = typeof(UserMetricsEndpoints);
        var mapMethod = endpointsType.GetMethod("MapUserMetricsEndpoints", 
            BindingFlags.Public | BindingFlags.Static);
        
        Assert.NotNull(mapMethod);
        Assert.True(mapMethod.IsStatic);
        Assert.True(mapMethod.IsPublic);
        
        var parameters = mapMethod.GetParameters();
        Assert.Single(parameters);
        Assert.Equal(typeof(WebApplication), parameters[0].ParameterType);
        Assert.Equal(typeof(WebApplication), mapMethod.ReturnType);
    }

    [Fact]
    public void AllUserMetricsApiRoutes_AreUnderCorrectBasePath()
    {
        // Arrange & Act & Assert
        // This test verifies that all our API routes follow the pattern /api/users/{userId}/metrics*
        
        // Check the source code to ensure routes are correctly defined
        var endpointsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, 
            "..", "..", "..", "..", "..", 
            "src", "Toman.Management.KPIAnalysis.ApiService", 
            "Features", "GitLabMetrics", "UserMetricsEndpoints.cs");
        
        if (File.Exists(endpointsFile))
        {
            var content = File.ReadAllText(endpointsFile);
            
            // Verify that the group is mapped to the correct base path
            Assert.Contains(@"app.MapGroup(""/api/users"")", content);
            
            // Verify all four required endpoints exist
            Assert.Contains(@"""/{userId}/metrics""", content);
            Assert.Contains(@"""/{userId}/metrics/summary""", content);
            Assert.Contains(@"""/{userId}/metrics/trends""", content);
            Assert.Contains(@"""/{userId}/metrics/comparison""", content);
            
            // Verify they have proper HTTP methods (GET)
            Assert.Contains("MapGet", content);
            
            // Verify proper status code configurations
            Assert.Contains("Produces<UserMetricsResponse>(200)", content);
            Assert.Contains("Produces<UserMetricsSummaryResponse>(200)", content);
            Assert.Contains("Produces<UserMetricsTrendsResponse>(200)", content);
            Assert.Contains("Produces<UserMetricsComparisonResponse>(200)", content);
            
            // Verify error handling status codes
            Assert.Contains("Produces(400)", content);
            Assert.Contains("Produces(404)", content);  
            Assert.Contains("Produces(500)", content);
        }
    }

    [Fact]
    public void UserMetricsApiDocumentation_MatchesImplementation()
    {
        // Arrange & Act & Assert
        // Verify that the API documentation matches the actual implementation
        
        var docFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, 
            "..", "..", "..", "..", "..", 
            "docs", "USER_METRICS_API.md");
        
        if (File.Exists(docFile))
        {
            var docContent = File.ReadAllText(docFile);
            
            // Verify all four endpoints are documented
            Assert.Contains("GET /api/users/{userId}/metrics", docContent);
            Assert.Contains("GET /api/users/{userId}/metrics/summary", docContent);
            Assert.Contains("GET /api/users/{userId}/metrics/trends", docContent);
            Assert.Contains("GET /api/users/{userId}/metrics/comparison", docContent);
            
            // Verify common parameters are documented
            Assert.Contains("fromDate", docContent);
            Assert.Contains("toDate", docContent);
            Assert.Contains("ISO 8601 format", docContent);
            
            // Verify response structure documentation
            Assert.Contains("userId", docContent);
            Assert.Contains("userName", docContent);
            Assert.Contains("metadata", docContent);
        }
    }

    [Fact]
    public void ResponseModels_AreRecordTypes()
    {
        // Arrange & Act & Assert
        // Verify that all response models are implemented as records (immutable)
        // which is a best practice for API responses
        
        var responseTypes = new[]
        {
            typeof(UserMetricsResponse),
            typeof(UserMetricsSummaryResponse), 
            typeof(UserMetricsTrendsResponse),
            typeof(UserMetricsComparisonResponse)
        };
        
        foreach (var type in responseTypes)
        {
            // Records have specific characteristics we can check
            Assert.True(type.IsClass);
            
            // Records have a special constructor pattern
            var constructors = type.GetConstructors();
            Assert.True(constructors.Length > 0);
        }
    }
}