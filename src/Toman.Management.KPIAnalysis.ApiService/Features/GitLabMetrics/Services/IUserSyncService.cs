namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

/// <summary>
/// Service responsible for synchronizing GitLab users with the DimUsers table
/// </summary>
public interface IUserSyncService
{
    /// <summary>
    /// Synchronizes all users from GitLab to the DimUsers table
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of users synchronized</returns>
    Task<int> SyncAllUsersAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Ensures that users referenced in raw data exist in DimUsers table
    /// Extracts unique user IDs from raw data and fetches missing user details from GitLab
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of users synchronized</returns>
    Task<int> SyncMissingUsersFromRawDataAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Ensures a specific user exists in the DimUsers table
    /// </summary>
    /// <param name="userId">GitLab user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if user exists or was created, false if user could not be found or created</returns>
    Task<bool> EnsureUserExistsAsync(long userId, CancellationToken cancellationToken = default);
}
