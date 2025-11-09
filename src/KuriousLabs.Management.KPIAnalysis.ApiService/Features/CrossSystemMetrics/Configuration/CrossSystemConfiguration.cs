namespace KuriousLabs.Management.KPIAnalysis.ApiService.Features.CrossSystemMetrics.Configuration;

/// <summary>
/// Configuration for cross-system integration between Jira and GitLab
/// </summary>
public sealed class CrossSystemConfiguration
{
    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string SectionName = "CrossSystem";

    /// <summary>
    /// Regex patterns for valid Jira project keys
    /// Default: [A-Z][A-Z0-9]+ (e.g., PROJECT, MAIN, ABC123)
    /// </summary>
    public string JiraProjectKeyPattern { get; init; } = @"[A-Z][A-Z0-9]+";

    /// <summary>
    /// Map GitLab usernames/emails to Jira account IDs
    /// Key: GitLab username or email
    /// Value: Jira accountId
    /// </summary>
    public Dictionary<string, string> IdentityMappings { get; init; } = new();

    /// <summary>
    /// Whether to attempt email-based matching if explicit mapping not found
    /// </summary>
    public bool EnableEmailFallback { get; init; } = true;
}
