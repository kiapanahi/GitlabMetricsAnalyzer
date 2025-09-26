using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Toman.Management.KPIAnalysis.ApiService.Configuration;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

/// <summary>
/// Service for filtering bots based on configured patterns
/// </summary>
public sealed class IdentityMappingService : IIdentityMappingService
{
    private readonly MetricsConfiguration _configuration;
    private readonly List<Regex> _botRegexes;

    public IdentityMappingService(IOptions<MetricsConfiguration> configuration)
    {
        _configuration = configuration.Value;
        
        // Compile bot regex patterns for better performance
        _botRegexes = _configuration.Identity.BotRegexPatterns
            .Select(pattern => new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase))
            .ToList();
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
}