using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Toman.Management.KPIAnalysis.ApiService.Configuration;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Entities;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

/// <summary>
/// Service for mapping developer identities, handling aliases, and filtering bots
/// </summary>
public sealed class IdentityMappingService : IIdentityMappingService
{
    private readonly MetricsConfiguration _configuration;
    private readonly List<Regex> _botRegexes;
    private readonly Dictionary<string, CanonicalDeveloperConfiguration> _identityMap;

    public IdentityMappingService(IOptions<MetricsConfiguration> configuration)
    {
        _configuration = configuration.Value;
        
        // Compile bot regex patterns for better performance
        _botRegexes = _configuration.Identity.BotRegexPatterns
            .Select(pattern => new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase))
            .ToList();

        // Build a case-insensitive lookup for identity overrides
        _identityMap = new Dictionary<string, CanonicalDeveloperConfiguration>(StringComparer.OrdinalIgnoreCase);
        
        foreach (var kvp in _configuration.Identity.IdentityOverrides)
        {
            var canonical = kvp.Value;
            
            // Add the key itself as a lookup
            _identityMap[kvp.Key] = canonical;
            
            // Add primary email and username as lookups
            _identityMap[canonical.PrimaryEmail] = canonical;
            _identityMap[canonical.PrimaryUsername] = canonical;
            
            // Add all aliases as lookups
            foreach (var aliasEmail in canonical.AliasEmails)
            {
                _identityMap[aliasEmail] = canonical;
            }
            
            foreach (var aliasUsername in canonical.AliasUsernames)
            {
                _identityMap[aliasUsername] = canonical;
            }
        }
    }

    /// <inheritdoc />
    public bool IsBot(string username, string? email = null, string? displayName = null)
    {
        if (string.IsNullOrWhiteSpace(username))
            return false;

        // Check username against bot patterns
        if (_botRegexes.Any(regex => regex.IsMatch(username)))
            return true;

        // Check email against bot patterns if provided
        if (!string.IsNullOrWhiteSpace(email) && 
            _botRegexes.Any(regex => regex.IsMatch(email)))
            return true;

        // Check display name against bot patterns if provided
        if (!string.IsNullOrWhiteSpace(displayName) && 
            _botRegexes.Any(regex => regex.IsMatch(displayName)))
            return true;

        return false;
    }

    /// <inheritdoc />
    public CanonicalDeveloperConfiguration? GetCanonicalDeveloper(string emailOrUsername)
    {
        if (string.IsNullOrWhiteSpace(emailOrUsername))
            return null;

        return _identityMap.TryGetValue(emailOrUsername, out var canonical) 
            ? canonical 
            : null;
    }

    /// <inheritdoc />
    public List<DeveloperAlias> ConsolidateAliases(string email, string username)
    {
        var aliases = new List<DeveloperAlias>();
        
        // Get canonical developer if exists
        var canonical = GetCanonicalDeveloper(email) ?? GetCanonicalDeveloper(username);
        
        if (canonical is not null)
        {
            // Add all configured aliases for this canonical developer
            foreach (var aliasEmail in canonical.AliasEmails)
            {
                if (!string.Equals(aliasEmail, canonical.PrimaryEmail, StringComparison.OrdinalIgnoreCase))
                {
                    aliases.Add(new DeveloperAlias
                    {
                        AliasType = "email",
                        AliasValue = aliasEmail,
                        VerifiedAt = DateTimeOffset.UtcNow
                    });
                }
            }
            
            foreach (var aliasUsername in canonical.AliasUsernames)
            {
                if (!string.Equals(aliasUsername, canonical.PrimaryUsername, StringComparison.OrdinalIgnoreCase))
                {
                    aliases.Add(new DeveloperAlias
                    {
                        AliasType = "username",
                        AliasValue = aliasUsername,
                        VerifiedAt = DateTimeOffset.UtcNow
                    });
                }
            }
            
            // Add the original email/username as aliases if they differ from canonical
            if (!string.Equals(email, canonical.PrimaryEmail, StringComparison.OrdinalIgnoreCase))
            {
                aliases.Add(new DeveloperAlias
                {
                    AliasType = "email",
                    AliasValue = email
                });
            }
            
            if (!string.Equals(username, canonical.PrimaryUsername, StringComparison.OrdinalIgnoreCase))
            {
                aliases.Add(new DeveloperAlias
                {
                    AliasType = "username",
                    AliasValue = username
                });
            }
        }
        
        return aliases;
    }
}