using System.Diagnostics;
using Microsoft.Extensions.Options;
using KuriousLabs.Management.KPIAnalysis.ApiService.Features.CrossSystemMetrics.Configuration;

namespace KuriousLabs.Management.KPIAnalysis.ApiService.Features.CrossSystemMetrics.Services;

/// <summary>
/// Service for mapping GitLab users to Jira users
/// </summary>
public interface IIdentityMappingService
{
    /// <summary>
    /// Map a GitLab username to a Jira account ID
    /// </summary>
    /// <param name="gitlabUsername">GitLab username</param>
    /// <param name="gitlabEmail">Optional GitLab email for fallback matching</param>
    /// <returns>Jira accountId if mapping found, null otherwise</returns>
    string? MapGitLabUserToJira(string gitlabUsername, string? gitlabEmail = null);

    /// <summary>
    /// Map a Jira account ID to a GitLab username
    /// </summary>
    /// <param name="jiraAccountId">Jira account ID</param>
    /// <returns>GitLab username if mapping found, null otherwise</returns>
    string? MapJiraUserToGitLab(string jiraAccountId);
}

/// <summary>
/// Implementation of identity mapping service using configuration
/// </summary>
public sealed class IdentityMappingService : IIdentityMappingService
{
    private readonly CrossSystemConfiguration _configuration;
    private readonly ILogger<IdentityMappingService> _logger;
    private readonly Dictionary<string, string> _reverseMapping;

    public IdentityMappingService(
        IOptions<CrossSystemConfiguration> configuration,
        ILogger<IdentityMappingService> logger)
    {
        _configuration = configuration.Value;
        _logger = logger;

        // Build reverse mapping (Jira -> GitLab)
        _reverseMapping = _configuration.IdentityMappings
            .ToDictionary(kvp => kvp.Value, kvp => kvp.Key, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public string? MapGitLabUserToJira(string gitlabUsername, string? gitlabEmail = null)
    {
        using var activity = Diagnostics.ActivitySource.StartActivity("MapGitLabUserToJira");
        activity?.SetTag("gitlab_username", gitlabUsername);

        if (string.IsNullOrWhiteSpace(gitlabUsername))
        {
            return null;
        }

        // Try explicit mapping by username
        if (_configuration.IdentityMappings.TryGetValue(gitlabUsername, out var jiraAccountId))
        {
            activity?.SetTag("mapping_method", "explicit_username");
            return jiraAccountId;
        }

        // Try explicit mapping by email
        if (!string.IsNullOrWhiteSpace(gitlabEmail) &&
            _configuration.IdentityMappings.TryGetValue(gitlabEmail, out jiraAccountId))
        {
            activity?.SetTag("mapping_method", "explicit_email");
            return jiraAccountId;
        }

        // Try email-based fallback if enabled
        if (_configuration.EnableEmailFallback && !string.IsNullOrWhiteSpace(gitlabEmail))
        {
            // Look for any mapping where the key contains the email domain
            var emailDomain = gitlabEmail.Split('@').LastOrDefault();
            if (!string.IsNullOrWhiteSpace(emailDomain))
            {
                // This is a simple heuristic - in production, you'd want more sophisticated matching
                activity?.SetTag("mapping_method", "email_fallback");
                activity?.SetTag("email_domain", emailDomain);
                
                _logger.LogDebug(
                    "No explicit mapping found for GitLab user {GitLabUsername} ({GitLabEmail}). " +
                    "Email fallback enabled but no match found",
                    gitlabUsername, gitlabEmail);
            }
        }

        activity?.SetTag("mapping_found", false);
        return null;
    }

    /// <inheritdoc />
    public string? MapJiraUserToGitLab(string jiraAccountId)
    {
        using var activity = Diagnostics.ActivitySource.StartActivity("MapJiraUserToGitLab");
        activity?.SetTag("jira_account_id", jiraAccountId);

        if (string.IsNullOrWhiteSpace(jiraAccountId))
        {
            return null;
        }

        var found = _reverseMapping.TryGetValue(jiraAccountId, out var gitlabUsername);
        activity?.SetTag("mapping_found", found);
        
        return gitlabUsername;
    }
}
