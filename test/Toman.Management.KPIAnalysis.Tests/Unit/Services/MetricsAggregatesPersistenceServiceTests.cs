using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Data;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Entities;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

namespace Toman.Management.KPIAnalysis.Tests.Unit.Services;

public sealed class MetricsAggregatesPersistenceServiceTests : IDisposable
{
    private readonly DbContextOptions<GitLabMetricsDbContext> _dbOptions;
    private readonly GitLabMetricsDbContext _context;
    private readonly MetricsAggregatesPersistenceService _service;

    public MetricsAggregatesPersistenceServiceTests()
    {
        _dbOptions = new DbContextOptionsBuilder<GitLabMetricsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .EnableSensitiveDataLogging()
            .Options;

        _context = new GitLabMetricsDbContext(_dbOptions);
        _service = new MetricsAggregatesPersistenceService(_context);

        // Seed test data
        SeedTestData();
    }

    [Fact]
    public async Task PersistAggregateAsync_ShouldPersistMetricsSuccessfully()
    {
        // Arrange
        var result = CreateTestMetricsResult();

        // Act
        var aggregateId = await _service.PersistAggregateAsync(result);

        // Assert
        Assert.True(aggregateId > 0);
        
        var persisted = await _context.DeveloperMetricsAggregates
            .FirstOrDefaultAsync(a => a.Id == aggregateId);
            
        Assert.NotNull(persisted);
        Assert.Equal(result.DeveloperId, persisted.DeveloperId);
        Assert.Equal(SchemaVersion.Current, persisted.SchemaVersion);
        Assert.Equal(result.WindowDays, persisted.WindowDays);
        Assert.NotNull(persisted.AuditMetadata);
    }

    [Fact]
    public async Task GetAggregateAsync_ShouldReturnPersistedMetrics()
    {
        // Arrange
        var result = CreateTestMetricsResult();
        await _service.PersistAggregateAsync(result);

        // Act
        var retrieved = await _service.GetAggregateAsync(
            result.DeveloperId, 
            result.WindowDays, 
            result.WindowEnd);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(result.DeveloperId, retrieved.DeveloperId);
        Assert.Equal(result.WindowDays, retrieved.WindowDays);
        Assert.Equal(result.Metrics.MrCycleTimeP50H, retrieved.Metrics.MrCycleTimeP50H);
    }

    [Fact]
    public async Task AggregateExistsAsync_ShouldReturnTrueForExistingAggregate()
    {
        // Arrange
        var result = CreateTestMetricsResult();
        await _service.PersistAggregateAsync(result);

        // Act
        var exists = await _service.AggregateExistsAsync(
            result.DeveloperId, 
            result.WindowDays, 
            result.WindowEnd);

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task AggregateExistsAsync_ShouldReturnFalseForNonExistingAggregate()
    {
        // Act
        var exists = await _service.AggregateExistsAsync(999, 14, DateTime.UtcNow);

        // Assert
        Assert.False(exists);
    }

    private void SeedTestData()
    {
        var developer = new Developer
        {
            Id = 1,
            GitLabUserId = 123,
            PrimaryEmail = "test@example.com",
            PrimaryUsername = "testuser",
            DisplayName = "Test User"
        };

        _context.Developers.Add(developer);
        _context.SaveChanges();
    }

    private static PerDeveloperMetricsResult CreateTestMetricsResult()
    {
        var windowEnd = DateTime.UtcNow.Date;
        var windowStart = windowEnd.AddDays(-14);

        return new PerDeveloperMetricsResult
        {
            DeveloperId = 1,
            DeveloperName = "Test User",
            DeveloperEmail = "test@example.com",
            ComputationDate = DateTime.UtcNow,
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

    public void Dispose()
    {
        _context.Dispose();
    }
}
