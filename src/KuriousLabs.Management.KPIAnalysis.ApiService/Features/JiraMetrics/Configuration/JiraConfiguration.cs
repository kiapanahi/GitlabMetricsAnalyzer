namespace KuriousLabs.Management.KPIAnalysis.ApiService.Features.JiraMetrics.Configuration;

public sealed class JiraConfiguration
{
    public const string SectionName = "Jira";

    /// <summary>
    /// Jira instance base URL (e.g., https://jira.tomanpay.net/)
    /// </summary>
    public required string BaseUrl { get; init; }

    /// <summary>
    /// Jira authentication token (Personal Access Token or API Token).
    /// Can be configured via environment variable Jira__Token or user secret Jira:Token
    /// </summary>
    public required string Token { get; init; }
}
