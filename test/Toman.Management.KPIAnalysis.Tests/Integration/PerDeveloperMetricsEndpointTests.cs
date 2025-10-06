using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

namespace Toman.Management.KPIAnalysis.Tests.Integration;

/// <summary>
/// Integration tests for the per-developer metrics endpoint
/// </summary>
public sealed class PerDeveloperMetricsEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public PerDeveloperMetricsEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Use mock GitLab client for testing
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IGitLabHttpClient));
                if (descriptor is not null)
                {
                    services.Remove(descriptor);
                }
                services.AddSingleton<IGitLabHttpClient, MockGitLabHttpClient>();
            });
        });
    }

    [Fact]
    public async Task GetPipelineSuccessRate_WithValidUserId_ReturnsOk()
    {
        // Arrange
        var client = _factory.CreateClient();
        var userId = 1; // Alice from mock data

        // Act
        var response = await client.GetAsync($"/api/metrics/per-developer/{userId}/pipeline-success-rate?lookbackDays=30");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<PipelineSuccessRateResult>();
        Assert.NotNull(result);
        Assert.Equal(userId, result.UserId);
        Assert.Equal("alice.dev", result.Username);
        Assert.NotNull(result.PipelineSuccessRate);
        Assert.InRange(result.PipelineSuccessRate.Value, 0.0m, 1.0m);
    }

    [Fact]
    public async Task GetPipelineSuccessRate_WithInvalidLookbackDays_ReturnsBadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();
        var userId = 1;

        // Act
        var response = await client.GetAsync($"/api/metrics/per-developer/{userId}/pipeline-success-rate?lookbackDays=0");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetPipelineSuccessRate_WithTooManyDays_ReturnsBadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();
        var userId = 1;

        // Act
        var response = await client.GetAsync($"/api/metrics/per-developer/{userId}/pipeline-success-rate?lookbackDays=400");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetPipelineSuccessRate_WithNonExistentUser_ReturnsNotFound()
    {
        // Arrange
        var client = _factory.CreateClient();
        var userId = 999;

        // Act
        var response = await client.GetAsync($"/api/metrics/per-developer/{userId}/pipeline-success-rate?lookbackDays=30");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetPipelineSuccessRate_WithDefaultLookbackDays_ReturnsOk()
    {
        // Arrange
        var client = _factory.CreateClient();
        var userId = 2; // Bob from mock data

        // Act
        var response = await client.GetAsync($"/api/metrics/per-developer/{userId}/pipeline-success-rate");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<PipelineSuccessRateResult>();
        Assert.NotNull(result);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(30, result.LookbackDays);
    }

    [Fact]
    public async Task GetPipelineSuccessRate_ReturnsProjectBreakdown()
    {
        // Arrange
        var client = _factory.CreateClient();
        var userId = 1;

        // Act
        var response = await client.GetAsync($"/api/metrics/per-developer/{userId}/pipeline-success-rate?lookbackDays=30");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        var result = await response.Content.ReadFromJsonAsync<PipelineSuccessRateResult>();
        Assert.NotNull(result);
        Assert.NotNull(result.Projects);
        // Projects should contain pipeline data
        if (result.TotalPipelines > 0)
        {
            Assert.NotEmpty(result.Projects);
            
            // Verify project summary data integrity
            var totalFromProjects = result.Projects.Sum(p => p.TotalPipelines);
            Assert.Equal(result.TotalPipelines, totalFromProjects);
        }
    }
}
