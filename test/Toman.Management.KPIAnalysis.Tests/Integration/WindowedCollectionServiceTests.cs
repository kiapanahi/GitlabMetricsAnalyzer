using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Toman.Management.KPIAnalysis.ApiService.Configuration;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Configuration;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Data;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Operational;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;
using Xunit;

namespace Toman.Management.KPIAnalysis.Tests.Integration;

public class WindowedCollectionServiceTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly GitLabMetricsDbContext _dbContext;

    public WindowedCollectionServiceTests()
    {
        // Setup in-memory database
        var services = new ServiceCollection();
        services.AddDbContext<GitLabMetricsDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        // Configure minimal options
        var collectionOptions = Options.Create(new CollectionConfiguration());
        var metricsOptions = Options.Create(new MetricsConfiguration
        {
            Identity = new IdentityConfiguration(),
            Excludes = new ExclusionConfiguration()
        });

        services.AddSingleton(collectionOptions);
        services.AddSingleton(metricsOptions);
        services.AddLogging();

        // Add services with mocks
        services.AddScoped<IDataEnrichmentService, DataEnrichmentService>();
        
        // Simple mock services
        var mockGitLabService = new Mock<IGitLabService>();
        var mockUserSyncService = new Mock<IUserSyncService>();
        
        services.AddSingleton(mockGitLabService.Object);
        services.AddSingleton(mockUserSyncService.Object);
        services.AddScoped<IGitLabCollectorService, GitLabCollectorService>();

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<GitLabMetricsDbContext>();
        _dbContext.Database.EnsureCreated();
    }

    [Fact]
    public async Task CollectionRun_CanBeCreatedAndRetrieved()
    {
        // Arrange
        var collectionRun = new CollectionRun
        {
            RunType = "incremental",
            Status = "running",
            TriggerSource = "test",
            WindowSizeHours = 2
        };

        // Act
        _dbContext.CollectionRuns.Add(collectionRun);
        await _dbContext.SaveChangesAsync();

        var retrieved = await _dbContext.CollectionRuns
            .Where(r => r.RunType == "incremental")
            .FirstOrDefaultAsync();

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("incremental", retrieved.RunType);
        Assert.Equal("running", retrieved.Status);
        Assert.Equal(2, retrieved.WindowSizeHours);
    }

    [Fact]
    public async Task IngestionState_CanBeCreatedWithWindowInfo()
    {
        // Arrange
        var state = new IngestionState
        {
            Entity = "incremental",
            LastSeenUpdatedAt = DateTimeOffset.UtcNow,
            LastRunAt = DateTimeOffset.UtcNow,
            WindowSizeHours = 24,
            LastWindowEnd = DateTimeOffset.UtcNow.AddHours(-1)
        };

        // Act
        _dbContext.IngestionStates.Add(state);
        await _dbContext.SaveChangesAsync();

        var retrieved = await _dbContext.IngestionStates
            .Where(s => s.Entity == "incremental")
            .FirstOrDefaultAsync();

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(24, retrieved.WindowSizeHours);
        Assert.NotNull(retrieved.LastWindowEnd);
    }

    [Fact]
    public void DataEnrichmentService_CanDetectHotfixPatterns()
    {
        // Arrange
        var enrichmentService = _serviceProvider.GetRequiredService<IDataEnrichmentService>();

        // Act & Assert
        Assert.False(enrichmentService.ShouldExcludeCommit("Regular commit message"));
        Assert.False(enrichmentService.ShouldExcludeBranch("feature/new-feature"));
        Assert.False(enrichmentService.ShouldExcludeFile("src/Controllers/TestController.cs"));
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
        _serviceProvider?.Dispose();
    }
}