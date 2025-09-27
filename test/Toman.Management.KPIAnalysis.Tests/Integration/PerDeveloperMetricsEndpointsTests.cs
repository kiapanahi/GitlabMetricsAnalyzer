using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Data;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;

using Xunit;

namespace Toman.Management.KPIAnalysis.Tests.Integration;

public sealed class PerDeveloperMetricsEndpointsTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public PerDeveloperMetricsEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace the real database with in-memory database for testing
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<GitLabMetricsDbContext>));
                if (descriptor is not null)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<GitLabMetricsDbContext>(options =>
                {
                    options.UseInMemoryDatabase("TestDb_" + Guid.NewGuid());
                });
            });
        });

        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task GetSupportedWindows_ReturnsExpectedWindows()
    {
        // Act
        var response = await _client.GetAsync("/api/metrics/per-developer/windows");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<SupportedWindowsResponse>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(result);
        Assert.Equal([14, 28, 90], result.WindowDays);
    }

    [Fact]
    public async Task ComputeMetrics_WithValidDeveloper_ReturnsMetrics()
    {
        // Arrange
        const long developerId = 12345;
        await SeedTestDataAsync(developerId);

        var request = new ComputeMetricsRequest
        {
            WindowDays = 14,
            EndDate = DateTimeOffset.UtcNow,
            ApplyWinsorization = true,
            ApplyFileExclusions = true
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/metrics/per-developer/{developerId}/compute", request);

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<PerDeveloperMetricsResponse>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(result);
        Assert.Equal(developerId, result.DeveloperId);
        Assert.Equal(14, result.WindowDays);
        Assert.NotNull(result.Metrics);
        Assert.NotNull(result.Audit);
    }

    [Fact]
    public async Task ComputeMetrics_WithInvalidWindowDays_ReturnsBadRequest()
    {
        // Arrange
        var request = new ComputeMetricsRequest
        {
            WindowDays = 30, // Unsupported window
            EndDate = DateTimeOffset.UtcNow
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/metrics/per-developer/123/compute", request);

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ComputeBatchMetrics_WithValidDevelopers_ReturnsResults()
    {
        // Arrange
        const long developer1 = 11111;
        const long developer2 = 22222;
        
        await SeedTestDataAsync(developer1);
        await SeedTestDataAsync(developer2);

        var request = new ComputeBatchMetricsRequest
        {
            DeveloperIds = [developer1, developer2],
            WindowDays = 14,
            EndDate = DateTimeOffset.UtcNow
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/metrics/per-developer/batch/compute", request);

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ComputeBatchMetricsResponse>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(result);
        Assert.Equal(2, result.SuccessCount);
        Assert.Equal(2, result.RequestedCount);
        Assert.Equal(0, result.FailureCount);
        Assert.Equal(2, result.Results.Count);
    }

    private async Task SeedTestDataAsync(long developerId)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<GitLabMetricsDbContext>();

        var windowStart = DateTimeOffset.UtcNow.AddDays(-14);
        
        // Add basic developer data
        var commit = new RawCommit
        {
            ProjectId = 1,
            ProjectName = "Test Project",
            CommitId = $"commit_{developerId}",
            AuthorUserId = developerId,
            AuthorName = $"Developer {developerId}",
            AuthorEmail = $"dev{developerId}@example.com",
            CommittedAt = windowStart.AddDays(1),
            Message = "Test commit",
            Additions = 10,
            Deletions = 5,
            IngestedAt = DateTimeOffset.UtcNow
        };

        var mergeRequest = new RawMergeRequest
        {
            ProjectId = 1,
            ProjectName = "Test Project",
            MrId = developerId, // Use developerId for uniqueness
            AuthorUserId = developerId,
            AuthorName = $"Developer {developerId}",
            Title = "Test MR",
            CreatedAt = windowStart.AddDays(1),
            MergedAt = windowStart.AddDays(2),
            State = "merged",
            SourceBranch = $"feature/test-{developerId}",
            TargetBranch = "main",
            IngestedAt = DateTimeOffset.UtcNow
        };

        var pipeline = new RawPipeline
        {
            ProjectId = 1,
            ProjectName = "Test Project",
            PipelineId = developerId, // Use developerId for uniqueness
            Sha = $"sha_{developerId}",
            Ref = "main",
            Status = "success",
            AuthorUserId = developerId,
            AuthorName = $"Developer {developerId}",
            TriggerSource = "push",
            CreatedAt = windowStart.AddDays(1),
            UpdatedAt = windowStart.AddDays(1),
            DurationSec = 300,
            IngestedAt = DateTimeOffset.UtcNow
        };

        dbContext.RawCommits.Add(commit);
        dbContext.RawMergeRequests.Add(mergeRequest);
        dbContext.RawPipelines.Add(pipeline);
        
        await dbContext.SaveChangesAsync();
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}