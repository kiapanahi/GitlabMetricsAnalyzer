using Moq;

using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

namespace Toman.Management.KPIAnalysis.Tests.Unit.Services;

public sealed class MetricCatalogServiceTests
{
    private readonly MetricCatalogService _service;

    public MetricCatalogServiceTests()
    {
        var mockPersistenceService = new Mock<IMetricsAggregatesPersistenceService>();
        _service = new MetricCatalogService(mockPersistenceService.Object);
    }

    [Fact]
    public async Task GenerateCatalogAsync_ShouldReturnValidCatalog()
    {
        // Act
        var catalog = await _service.GenerateCatalogAsync();

        // Assert
        Assert.NotNull(catalog);
        Assert.Equal(SchemaVersion.Current, catalog.Version);
        Assert.True((DateTimeOffset.UtcNow - catalog.GeneratedAt).TotalMinutes < 1);
        Assert.NotEmpty(catalog.Description);
        Assert.NotEmpty(catalog.Metrics);
    }

    [Fact]
    public async Task GenerateCatalogAsync_ShouldIncludeAllPRDMetrics()
    {
        // Act
        var catalog = await _service.GenerateCatalogAsync();

        // Assert - Check for key PRD metrics
        var metricNames = catalog.Metrics.Select(m => m.Name).ToList();
        
        Assert.Contains(nameof(PerDeveloperMetrics.MrCycleTimeP50H), metricNames);
        Assert.Contains(nameof(PerDeveloperMetrics.PipelineSuccessRate), metricNames);
        Assert.Contains(nameof(PerDeveloperMetrics.DeploymentFrequencyWk), metricNames);
        Assert.Contains(nameof(PerDeveloperMetrics.MrThroughputWk), metricNames);
        
        // Verify all metrics have required properties
        foreach (var metric in catalog.Metrics)
        {
            Assert.NotEmpty(metric.Name);
            Assert.NotEmpty(metric.DisplayName);
            Assert.NotEmpty(metric.Description);
            Assert.NotEmpty(metric.DataType);
            Assert.NotEmpty(metric.Unit);
        }
    }

    [Fact]
    public void GeneratePerDeveloperExportsFromResults_ShouldMapCorrectly()
    {
        // Arrange
        var results = new[]
        {
            CreateTestMetricsResult(1, "User One"),
            CreateTestMetricsResult(2, "User Two")
        };

        // Act
        var exports = _service.GeneratePerDeveloperExportsFromResults(results);

        // Assert
        Assert.Equal(2, exports.Count);
        
        var firstExport = exports.First();
        Assert.Equal(SchemaVersion.Current, firstExport.SchemaVersion);
        Assert.Equal(1, firstExport.DeveloperId);
        Assert.Equal("User One", firstExport.DeveloperName);
        Assert.NotNull(firstExport.Metrics);
        Assert.NotNull(firstExport.Audit);
    }

    private static PerDeveloperMetricsResult CreateTestMetricsResult(long developerId, string developerName)
    {
        var windowEnd = DateTimeOffset.UtcNow.Date;
        var windowStart = windowEnd.AddDays(-14);

        return new PerDeveloperMetricsResult
        {
            DeveloperId = developerId,
            DeveloperName = developerName,
            DeveloperEmail = $"user{developerId}@example.com",
            ComputationDate = DateTimeOffset.UtcNow,
            WindowStart = windowStart,
            WindowEnd = windowEnd,
            WindowDays = 14,
            Metrics = new PerDeveloperMetrics
            {
                MrCycleTimeP50H = 24.5m,
                PipelineSuccessRate = 0.95m,
                DeploymentFrequencyWk = 3,
                MrThroughputWk = 5
            },
            Audit = new MetricsAudit
            {
                HasMergeRequestData = true,
                HasPipelineData = true,
                HasCommitData = true,
                HasReviewData = true,
                LowMergeRequestCount = false,
                LowPipelineCount = false,
                LowCommitCount = false,
                LowReviewCount = false,
                NullReasons = new Dictionary<string, string>(),
                TotalMergeRequests = 10,
                TotalPipelines = 15,
                TotalCommits = 25,
                TotalReviews = 8,
                ExcludedFiles = 2,
                WinsorizedMetrics = 1,
                DataQuality = "Good",
                HasSufficientData = true
            }
        };
    }
}