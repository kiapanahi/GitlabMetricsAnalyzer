using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Toman.Management.KPIAnalysis.ApiService.Configuration;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Entities;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

/// <summary>
/// Service for mapping developer identities, handling aliases, and filtering bots
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

    /// <summary>
    /// Gets the canonical developer ID for a given email or username
    /// </summary>
    /// <param name="emailOrUsername">Email or username to resolve</param>
    /// <returns>Canonical developer configuration if found, null otherwise</returns>
    CanonicalDeveloperConfiguration? GetCanonicalDeveloper(string emailOrUsername);

    /// <summary>
    /// Consolidates aliases for a developer based on configuration
    /// </summary>
    /// <param name="email">Primary email</param>
    /// <param name="username">Primary username</param>
    /// <returns>List of all known aliases for this developer</returns>
    List<DeveloperAlias> ConsolidateAliases(string email, string username);
}