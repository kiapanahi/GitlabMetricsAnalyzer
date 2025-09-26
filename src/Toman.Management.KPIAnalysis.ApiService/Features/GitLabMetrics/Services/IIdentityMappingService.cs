using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Toman.Management.KPIAnalysis.ApiService.Configuration;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

/// <summary>
/// Service for filtering bots based on configured patterns
/// </summary>
public interface IIdentityMappingService
{
    /// <summary>
    /// Determines if a user is a bot based on configured patterns
    /// </summary>
    /// <param name="username">Username to check</param>
    /// <param name="email">Email to check</param>
    /// <param name="displayName">Display name to check</param>
    /// <returns>True if the user matches bot patterns</returns>
    bool IsBot(string username, string? email = null, string? displayName = null);
}