using Microsoft.Extensions.Logging;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Methodology;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

namespace Toman.Management.KPIAnalysis.Tests;

/// <summary>
/// Tests for the methodology documentation system
/// </summary>
public class MethodologyServiceTests
{
    private readonly MethodologyService _methodologyService;

    public MethodologyServiceTests()
    {
        _methodologyService = new MethodologyService(new TestLogger<MethodologyService>());
    }

    [Fact]
    public async Task GetMethodologyAsync_ProductivityScore_ReturnsComprehensiveInfo()
    {
        // Act
        var methodology = await _methodologyService.GetMethodologyAsync("productivityscore");

        // Assert
        Assert.NotNull(methodology);
        Assert.Equal("ProductivityScore", methodology.MetricName);
        Assert.Contains("Composite score measuring developer output", methodology.Definition);
        Assert.Contains("commits_per_day", methodology.Calculation);
        Assert.NotEmpty(methodology.DataSources);
        Assert.NotEmpty(methodology.Limitations);
        Assert.NotEmpty(methodology.Interpretation.Ranges);
        Assert.Equal("2.1", methodology.Version);
    }

    [Fact]
    public async Task GetMethodologyAsync_NonexistentMetric_ReturnsNull()
    {
        // Act
        var methodology = await _methodologyService.GetMethodologyAsync("nonexistent");

        // Assert
        Assert.Null(methodology);
    }

    [Fact]
    public async Task GetAllMethodologiesAsync_ReturnsAllDefinedMetrics()
    {
        // Act
        var methodologies = await _methodologyService.GetAllMethodologiesAsync();

        // Assert
        Assert.NotEmpty(methodologies);
        Assert.Contains(methodologies, m => m.MetricName == "ProductivityScore");
        Assert.Contains(methodologies, m => m.MetricName == "VelocityScore");
        Assert.Contains(methodologies, m => m.MetricName == "PipelineSuccessRate");
        Assert.Contains(methodologies, m => m.MetricName == "CollaborationScore");
        
        // Ensure all have required fields
        foreach (var methodology in methodologies)
        {
            Assert.NotNull(methodology.Definition);
            Assert.NotNull(methodology.Calculation);
            Assert.NotEmpty(methodology.DataSources);
            Assert.NotEmpty(methodology.Limitations);
            Assert.NotNull(methodology.Version);
        }
    }

    [Fact]
    public async Task GetChangeLogAsync_ReturnsVersionHistory()
    {
        // Act
        var changeLog = await _methodologyService.GetChangeLogAsync();

        // Assert
        Assert.NotEmpty(changeLog);
        
        // Should be ordered by date descending
        var dates = changeLog.Select(c => c.ChangeDate).ToList();
        var sortedDates = dates.OrderByDescending(d => d).ToList();
        Assert.Equal(sortedDates, dates);
        
        // Check ProductivityScore version 2.1 change
        var productivityV21 = changeLog.FirstOrDefault(c => c.Metric == "ProductivityScore" && c.Version == "2.1");
        Assert.NotNull(productivityV21);
        Assert.Contains(productivityV21.Changes, change => change.Contains("pipeline success rate weighting"));
        Assert.Equal("VP Engineering", productivityV21.ApprovedBy);
    }

    [Fact]
    public async Task SearchMethodologiesAsync_FindsRelevantMetrics()
    {
        // Act - search for "pipeline"
        var results = await _methodologyService.SearchMethodologiesAsync("pipeline");

        // Assert
        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.MetricName == "ProductivityScore");
        Assert.Contains(results, r => r.MetricName == "PipelineSuccessRate");
    }

    [Fact]
    public async Task SearchMethodologiesAsync_EmptyQuery_ReturnsAllMetrics()
    {
        // Act
        var results = await _methodologyService.SearchMethodologiesAsync("");

        // Assert
        var allMethodologies = await _methodologyService.GetAllMethodologiesAsync();
        Assert.Equal(allMethodologies.Count, results.Count);
    }

    [Fact]
    public async Task RecordAuditTrailAsync_StoresEntry()
    {
        // Arrange
        var auditEntry = new AuditTrailEntry
        {
            Metric = "ProductivityScore",
            CalculatedAt = DateTimeOffset.UtcNow,
            AlgorithmVersion = "2.1",
            DataQualityScore = 8.5,
            Notes = "Test calculation"
        };

        // Act
        await _methodologyService.RecordAuditTrailAsync(auditEntry);

        // Verify it was stored
        var auditTrail = await _methodologyService.GetAuditTrailAsync("ProductivityScore");
        var storedEntry = auditTrail.FirstOrDefault();

        // Assert
        Assert.NotNull(storedEntry);
        Assert.Equal("ProductivityScore", storedEntry.Metric);
        Assert.Equal("2.1", storedEntry.AlgorithmVersion);
        Assert.Equal(8.5, storedEntry.DataQualityScore);
        Assert.Equal("Test calculation", storedEntry.Notes);
    }

    [Fact]
    public void GetMetricFootnote_ProductivityScore_ReturnsAppropriateFootnote()
    {
        // Act
        var footnote = _methodologyService.GetMetricFootnote("productivityscore");

        // Assert
        Assert.Contains("weighted algorithm", footnote);
        Assert.Contains("commit frequency", footnote);
        Assert.Contains("MR throughput", footnote);
        Assert.Contains("pipeline success rate", footnote);
    }

    [Fact]
    public void GetMethodologyLink_ReturnsCorrectFormat()
    {
        // Act
        var link = _methodologyService.GetMethodologyLink("ProductivityScore");

        // Assert
        Assert.Equal("/api/methodology/productivityscore", link);
    }

    [Theory]
    [InlineData("productivityscore")]
    [InlineData("velocityscore")]
    [InlineData("pipelinessuccessrate")]
    [InlineData("collaborationscore")]
    public async Task AllDefinedMetrics_HaveCompleteDocumentation(string metricName)
    {
        // Act
        var methodology = await _methodologyService.GetMethodologyAsync(metricName);

        // Assert - Verify comprehensive documentation
        Assert.NotNull(methodology);
        Assert.NotEmpty(methodology.Definition);
        Assert.NotEmpty(methodology.Calculation);
        Assert.NotEmpty(methodology.DataSources);
        Assert.NotEmpty(methodology.Limitations);
        Assert.NotEmpty(methodology.Interpretation.Ranges);
        Assert.NotNull(methodology.Version);
        Assert.True(methodology.LastUpdated > DateTimeOffset.MinValue);
        
        // Verify data sources have proper typing
        Assert.All(methodology.DataSources, ds =>
        {
            Assert.NotEmpty(ds.Source);
            Assert.Contains(ds.Type, new[] { "exact", "approximation", "derived" });
        });
        
        // Verify interpretation ranges are meaningful
        Assert.True(methodology.Interpretation.Ranges.Count >= 2);
    }
}

/// <summary>
/// Test logger implementation for unit tests
/// </summary>
public class TestLogger<T> : ILogger<T>
{
    public TestLogger() { }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        // For tests, we can just ignore logging or write to console
        Console.WriteLine($"[{logLevel}] {formatter(state, exception)}");
    }
}