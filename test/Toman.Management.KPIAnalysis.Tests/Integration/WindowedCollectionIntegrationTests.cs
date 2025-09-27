using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Toman.Management.KPIAnalysis.ApiService.Configuration;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Configuration;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Data;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Operational;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;
using Xunit;

namespace Toman.Management.KPIAnalysis.Tests.Integration;

public class WindowedCollectionIntegrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly GitLabMetricsDbContext _dbContext;
    private readonly IGitLabCollectorService _collectorService;

    public WindowedCollectionIntegrationTests()
    {

        // Setup in-memory database
        var services = new ServiceCollection();
        services.AddDbContext<GitLabMetricsDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        // Configure services with simple values
        var collectionOptions = Options.Create(new CollectionConfiguration
        {
            DefaultWindowSizeHours = 1,
            MaxParallelProjects = 1,
            ProjectProcessingDelayMs = 10,
            MaxRetries = 1,
            CollectReviewEvents = true,
            EnrichMergeRequestData = true
        });
        
        var metricsOptions = Options.Create(new MetricsConfiguration
        {
            Identity = new IdentityConfiguration(),
            Excludes = new ExclusionConfiguration()
        });

        services.AddSingleton(collectionOptions);
        services.AddSingleton(metricsOptions);

        services.AddLogging(builder => builder.AddConsole());

        // Add services
        services.AddScoped<IGitLabCollectorService, GitLabCollectorService>();
        services.AddScoped<IDataEnrichmentService, DataEnrichmentService>();
        
        // Mock GitLab service and user sync service
        var mockGitLabService = new Mock<IGitLabService>();
        mockGitLabService.Setup(x => x.TestConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        mockGitLabService.Setup(x => x.GetProjectsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GitLabProject>().AsReadOnly());
        
        var mockUserSyncService = new Mock<IUserSyncService>();
        mockUserSyncService.Setup(x => x.SyncMissingUsersFromRawDataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        services.AddSingleton(mockGitLabService.Object);
        services.AddSingleton(mockUserSyncService.Object);

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<GitLabMetricsDbContext>();
        _collectorService = _serviceProvider.GetRequiredService<IGitLabCollectorService>();

        // Ensure database is created
        _dbContext.Database.EnsureCreated();
    }

    [Fact]
    public async Task StartCollectionRun_WithIncrementalType_ReturnsValidResponse()
    {
        // Arrange
        var request = new StartCollectionRunRequest
        {
            RunType = "incremental",
            WindowSizeHours = 1,
            TriggerSource = "test"
        };

        // Act
        var result = await _collectorService.StartCollectionRunAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("incremental", result.RunType);
        Assert.True(result.RunId != Guid.Empty);
    }

    [Fact]
    public async Task GetCollectionRunStatus_WithValidRunId_ReturnsStatus()
    {
        // Arrange - Start a run first
        var startRequest = new StartCollectionRunRequest
        {
            RunType = "incremental",
            WindowSizeHours = 1,
            TriggerSource = "test"
        };

        var startResult = await _collectorService.StartCollectionRunAsync(startRequest);

        // Act
        var statusResult = await _collectorService.GetCollectionRunStatusAsync(startResult.RunId);

        // Assert
        Assert.NotNull(statusResult);
        Assert.Equal(startResult.RunId, statusResult.RunId);
        Assert.Equal("incremental", statusResult.RunType);
    }

    [Fact]
    public async Task GetRecentCollectionRuns_ReturnsListOfRuns()
    {
        // Arrange - Start a run first
        var request = new StartCollectionRunRequest
        {
            RunType = "incremental",
            TriggerSource = "test"
        };
        await _collectorService.StartCollectionRunAsync(request);

        // Act
        var result = await _collectorService.GetRecentCollectionRunsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task BackfillCollection_WithDateRange_ReturnsValidResponse()
    {
        // Arrange
        var request = new StartCollectionRunRequest
        {
            RunType = "backfill",
            BackfillStartDate = DateTimeOffset.UtcNow.AddDays(-30),
            BackfillEndDate = DateTimeOffset.UtcNow.AddDays(-1),
            TriggerSource = "test"
        };

        // Act
        var result = await _collectorService.StartCollectionRunAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("backfill", result.RunType);
        Assert.True(result.RunId != Guid.Empty);
        Assert.NotNull(result.WindowStart);
        Assert.NotNull(result.WindowEnd);
    }

    [Fact]
    public async Task GetCollectionRunStatus_WithInvalidRunId_ThrowsKeyNotFoundException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _collectorService.GetCollectionRunStatusAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task WindowedIncremental_TracksIngestionState()
    {
        // Arrange
        var request = new StartCollectionRunRequest
        {
            RunType = "incremental",
            WindowSizeHours = 2,
            TriggerSource = "test"
        };

        // Act
        await _collectorService.StartCollectionRunAsync(request);

        // Assert - Check that ingestion state was updated
        var state = await _dbContext.IngestionStates
            .Where(s => s.Entity == "incremental")
            .FirstOrDefaultAsync();

        Assert.NotNull(state);
        Assert.Equal(2, state.WindowSizeHours);
        Assert.NotNull(state.LastWindowEnd);
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
        _serviceProvider?.Dispose();
    }
}