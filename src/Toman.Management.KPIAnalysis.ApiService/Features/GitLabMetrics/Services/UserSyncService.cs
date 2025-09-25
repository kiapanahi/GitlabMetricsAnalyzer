using Microsoft.EntityFrameworkCore;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Data;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Dimensions;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

/// <summary>
/// Service responsible for synchronizing GitLab users with the DimUsers table
/// </summary>
public sealed class UserSyncService : IUserSyncService
{
    private readonly IGitLabService _gitLabService;
    private readonly GitLabMetricsDbContext _dbContext;
    private readonly ILogger<UserSyncService> _logger;

    public UserSyncService(
        IGitLabService gitLabService,
        GitLabMetricsDbContext dbContext,
        ILogger<UserSyncService> logger)
    {
        _gitLabService = gitLabService;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<int> SyncAllUsersAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting synchronization of all GitLab users");

        var users = await _gitLabService.GetUsersAsync(cancellationToken);
        var syncedCount = 0;

        foreach (var user in users)
        {
            try
            {
                if (await SyncUserToDimTableAsync(user, cancellationToken))
                {
                    syncedCount++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to sync user {UserId} ({Username})", user.Id, user.Username);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation("Synchronized {SyncedCount} out of {TotalCount} users", syncedCount, users.Count);
        return syncedCount;
    }

    public async Task<int> SyncMissingUsersFromRawDataAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting synchronization of missing users from raw data");

        // First, collect user information from all raw tables
        var userInfoFromRawData = await CollectUserInfoFromRawDataAsync(cancellationToken);
        _logger.LogInformation("Found {UserCount} unique users in raw data", userInfoFromRawData.Count);

        // Get existing users to avoid duplicates
        var existingUsers = await _dbContext.DimUsers
            .ToDictionaryAsync(u => u.Email.ToLowerInvariant(), u => u, cancellationToken);

        var syncedCount = 0;
        foreach (var userInfo in userInfoFromRawData.Values)
        {
            try
            {
                if (!existingUsers.ContainsKey(userInfo.Email.ToLowerInvariant()))
                {
                    // Try to fetch real user from GitLab by email
                    var gitLabUsers = await _gitLabService.GetUsersAsync(cancellationToken);
                    var matchingUser = gitLabUsers.FirstOrDefault(u => 
                        u.Email?.Equals(userInfo.Email, StringComparison.OrdinalIgnoreCase) == true);

                    if (matchingUser is not null)
                    {
                        // Found real GitLab user - sync them
                        if (await SyncUserToDimTableAsync(matchingUser, cancellationToken))
                        {
                            syncedCount++;
                            
                            // Update raw data with correct user ID
                            await UpdateRawDataWithCorrectUserIdAsync(userInfo.TempUserId, matchingUser.Id, cancellationToken);
                        }
                    }
                    else
                    {
                        // Create a placeholder user for commits that don't have a matching GitLab user
                        var placeholderUser = new DimUser
                        {
                            UserId = userInfo.TempUserId,
                            Username = userInfo.Name.Replace(" ", "."),
                            Name = userInfo.Name,
                            State = "external",
                            Email = userInfo.Email,
                            IsBot = false
                        };

                        await _dbContext.DimUsers.AddAsync(placeholderUser, cancellationToken);
                        syncedCount++;
                        
                        _logger.LogDebug("Created placeholder user for {Email} with temp ID {TempUserId}", 
                            userInfo.Email, userInfo.TempUserId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to sync user with email {Email}", userInfo.Email);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation("Synchronized {SyncedCount} users from raw data", syncedCount);
        return syncedCount;
    }

    public async Task<bool> EnsureUserExistsAsync(long userId, CancellationToken cancellationToken = default)
    {
        // Check if user already exists
        var existingUser = await _dbContext.DimUsers
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        if (existingUser is not null)
        {
            _logger.LogDebug("User {UserId} already exists in DimUsers", userId);
            return true;
        }

        // Fetch user from GitLab
        var gitLabUser = await _gitLabService.GetUserByIdAsync(userId, cancellationToken);
        if (gitLabUser is null)
        {
            _logger.LogWarning("User {UserId} not found in GitLab", userId);
            return false;
        }

        // Sync user to database
        return await SyncUserToDimTableAsync(gitLabUser, cancellationToken);
    }

    private async Task<bool> SyncUserToDimTableAsync(GitLabUser gitLabUser, CancellationToken cancellationToken)
    {
        try
        {
            // Check if user already exists
            var existingUser = await _dbContext.DimUsers
                .FirstOrDefaultAsync(u => u.UserId == gitLabUser.Id, cancellationToken);

            if (existingUser is not null)
            {
                // For init-only properties, we need to remove and recreate
                _dbContext.DimUsers.Remove(existingUser);
                
                var updatedUser = new DimUser
                {
                    UserId = gitLabUser.Id,
                    Username = gitLabUser.Username ?? existingUser.Username,
                    Name = gitLabUser.Name ?? existingUser.Name,
                    State = gitLabUser.State?.ToString() ?? existingUser.State,
                    Email = gitLabUser.Email ?? existingUser.Email,
                    IsBot = false // Mock implementation doesn't distinguish bots
                };

                await _dbContext.DimUsers.AddAsync(updatedUser, cancellationToken);
                _logger.LogDebug("Updated existing user {UserId} ({Username})", gitLabUser.Id, gitLabUser.Username);
            }
            else
            {
                // Create new user
                var newUser = new DimUser
                {
                    UserId = gitLabUser.Id,
                    Username = gitLabUser.Username ?? $"user_{gitLabUser.Id}",
                    Name = gitLabUser.Name ?? gitLabUser.Username ?? $"User {gitLabUser.Id}",
                    State = gitLabUser.State?.ToString() ?? "active",
                    Email = gitLabUser.Email ?? $"user{gitLabUser.Id}@unknown.com",
                    IsBot = false // Mock implementation doesn't distinguish bots
                };

                await _dbContext.DimUsers.AddAsync(newUser, cancellationToken);
                _logger.LogDebug("Added new user {UserId} ({Username})", gitLabUser.Id, gitLabUser.Username);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync user {UserId} ({Username}) to DimUsers table", gitLabUser.Id, gitLabUser.Username);
            return false;
        }
    }

    // Helper class to store user information collected from raw data
    private sealed class UserInfo
    {
        public required long TempUserId { get; init; }
        public required string Name { get; init; }
        public required string Email { get; init; }
    }

    private async Task<Dictionary<string, UserInfo>> CollectUserInfoFromRawDataAsync(CancellationToken cancellationToken)
    {
        var userInfoDict = new Dictionary<string, UserInfo>(StringComparer.OrdinalIgnoreCase);

        // Collect from commits
        var commitUsers = await _dbContext.RawCommits
            .Select(c => new { c.AuthorUserId, c.AuthorName, c.AuthorEmail })
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var user in commitUsers)
        {
            if (!string.IsNullOrEmpty(user.AuthorEmail) && !userInfoDict.ContainsKey(user.AuthorEmail))
            {
                userInfoDict[user.AuthorEmail] = new UserInfo
                {
                    TempUserId = user.AuthorUserId,
                    Name = user.AuthorName ?? "Unknown",
                    Email = user.AuthorEmail
                };
            }
        }

        // Note: MergeRequests and other entities might have real GitLab user IDs
        // So we'll focus on commits which often have email-based temporary IDs

        return userInfoDict;
    }

    private async Task UpdateRawDataWithCorrectUserIdAsync(long tempUserId, long realUserId, CancellationToken cancellationToken)
    {
        // Update commits
        var commitsToUpdate = await _dbContext.RawCommits
            .Where(c => c.AuthorUserId == tempUserId)
            .ToListAsync(cancellationToken);

        foreach (var commit in commitsToUpdate)
        {
            _dbContext.RawCommits.Remove(commit);
            var updatedCommit = new Models.Raw.RawCommit
            {
                Id = commit.Id,
                ProjectId = commit.ProjectId,
                ProjectName = commit.ProjectName,
                CommitId = commit.CommitId,
                AuthorUserId = realUserId, // Update with real user ID
                AuthorName = commit.AuthorName,
                AuthorEmail = commit.AuthorEmail,
                CommittedAt = commit.CommittedAt,
                Message = commit.Message,
                Additions = commit.Additions,
                Deletions = commit.Deletions,
                IsSigned = commit.IsSigned,
                IngestedAt = commit.IngestedAt
            };
            await _dbContext.RawCommits.AddAsync(updatedCommit, cancellationToken);
        }

        _logger.LogDebug("Updated {CommitCount} commits from temp user ID {TempUserId} to real user ID {RealUserId}", 
            commitsToUpdate.Count, tempUserId, realUserId);
    }
}
