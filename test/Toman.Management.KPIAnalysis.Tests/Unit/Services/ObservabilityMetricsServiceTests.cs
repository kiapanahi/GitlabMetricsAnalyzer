using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

namespace Toman.Management.KPIAnalysis.Tests.Unit.Services;

public class ObservabilityMetricsServiceTests
{
    [Fact]
    public void ObservabilityMetricsService_CanBeCreated()
    {
        // Arrange & Act
        using var service = new ObservabilityMetricsService();

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public void RecordRunDuration_DoesNotThrow()
    {
        // Arrange
        using var service = new ObservabilityMetricsService();
        var runId = Guid.NewGuid();

        // Act & Assert
        var exception = Record.Exception(() =>
            service.RecordRunDuration("backfill", "completed", TimeSpan.FromMinutes(5), runId));

        Assert.Null(exception);
    }

    [Fact]
    public void RecordGitLabApiCall_DoesNotThrow()
    {
        // Arrange
        using var service = new ObservabilityMetricsService();

        // Act & Assert
        var exception = Record.Exception(() =>
            service.RecordGitLabApiCall("/projects", "GET", 200));

        Assert.Null(exception);
    }

    [Fact]
    public void RecordDeveloperCoverageRatio_DoesNotThrow()
    {
        // Arrange
        using var service = new ObservabilityMetricsService();

        // Act & Assert
        var exception = Record.Exception(() =>
            service.RecordDeveloperCoverageRatio(0.85));

        Assert.Null(exception);
    }

    [Fact]
    public void RecordCollectionStats_DoesNotThrow()
    {
        // Arrange
        using var service = new ObservabilityMetricsService();
        var runId = Guid.NewGuid();

        // Act & Assert
        var exception = Record.Exception(() =>
            service.RecordCollectionStats(5, 100, 20, 15, 30, runId));

        Assert.Null(exception);
    }

    [Fact]
    public void RecordApiError_DoesNotThrow()
    {
        // Arrange
        using var service = new ObservabilityMetricsService();
        var runId = Guid.NewGuid();

        // Act & Assert
        var exception = Record.Exception(() =>
            service.RecordApiError("timeout", 408, runId));

        Assert.Null(exception);
    }

    [Fact]
    public void RecordDataQualityCheck_DoesNotThrow()
    {
        // Arrange
        using var service = new ObservabilityMetricsService();

        // Act & Assert
        var exception = Record.Exception(() =>
            service.RecordDataQualityCheck("referential_integrity", "passed", 0.95));

        Assert.Null(exception);
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        // Arrange
        var service = new ObservabilityMetricsService();

        // Act & Assert
        var exception = Record.Exception(() => service.Dispose());

        Assert.Null(exception);
    }
}
