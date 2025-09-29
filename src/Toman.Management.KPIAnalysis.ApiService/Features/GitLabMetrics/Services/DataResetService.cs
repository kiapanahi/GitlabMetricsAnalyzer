using Microsoft.EntityFrameworkCore;

using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Data;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

/// <summary>
/// Service for resetting raw data and ingestion states
/// </summary>
public interface IDataResetService
{
    /// <summary>
    /// Clears all raw data tables
    /// </summary>
    Task ClearAllRawDataAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets ingestion state to force re-collection
    /// </summary>
    Task ResetIngestionStateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets only incremental collection state
    /// </summary>
    Task ResetIncrementalStateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears specific raw data tables by type
    /// </summary>
    Task ClearRawDataByTypeAsync(string dataType, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of data reset service
/// </summary>
public sealed class DataResetService : IDataResetService
{
    private readonly GitLabMetricsDbContext _dbContext;
    private readonly ILogger<DataResetService> _logger;

    public DataResetService(
        GitLabMetricsDbContext dbContext,
        ILogger<DataResetService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task ClearAllRawDataAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Starting to clear all raw data tables");

        using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // Clear raw data tables in reverse dependency order
            await _dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE raw_merge_request_note CASCADE", cancellationToken);
            await _dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE raw_job CASCADE", cancellationToken);
            await _dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE raw_pipeline CASCADE", cancellationToken);
            await _dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE raw_mr CASCADE", cancellationToken);
            await _dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE raw_commit CASCADE", cancellationToken);

            // Clear operational tables
            await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM collection_runs", cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation("Successfully cleared all raw data tables");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to clear raw data tables");
            throw;
        }
    }

    public async Task ResetIngestionStateAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Resetting all ingestion states");

        try
        {
            await _dbContext.Database.ExecuteSqlRawAsync("DELETE FROM ingestion_state", cancellationToken);

            _logger.LogInformation("Successfully reset all ingestion states");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset ingestion states");
            throw;
        }
    }

    public async Task ResetIncrementalStateAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Resetting incremental collection state");

        try
        {
            var incrementalState = await _dbContext.IngestionStates
                .Where(s => s.Entity == "incremental")
                .FirstOrDefaultAsync(cancellationToken);

            if (incrementalState is not null)
            {
                _dbContext.IngestionStates.Remove(incrementalState);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            _logger.LogInformation("Successfully reset incremental collection state");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset incremental state");
            throw;
        }
    }

    public async Task ClearRawDataByTypeAsync(string dataType, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Clearing raw data for type: {DataType}", dataType);

        try
        {
            var sql = dataType.ToLowerInvariant() switch
            {
                "commits" => "TRUNCATE TABLE raw_commit CASCADE",
                "mergerequests" or "mrs" => "TRUNCATE TABLE raw_mr CASCADE",
                "pipelines" => "TRUNCATE TABLE raw_pipeline CASCADE",
                "jobs" => "TRUNCATE TABLE raw_job CASCADE",
                "notes" or "comments" => "TRUNCATE TABLE raw_merge_request_note CASCADE",
                _ => throw new ArgumentException($"Unknown data type: {dataType}", nameof(dataType))
            };

            await _dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);

            _logger.LogInformation("Successfully cleared raw data for type: {DataType}", dataType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear raw data for type: {DataType}", dataType);
            throw;
        }
    }
}
