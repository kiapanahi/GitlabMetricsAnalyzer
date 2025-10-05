namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Configuration;

public sealed class GitLabConfiguration
{
    public const string SectionName = "GitLab";

    public required string BaseUrl { get; init; }

    /// <summary>
    /// GitLab Personal Access Token. Can be configured via environment variable GitLab__Token or user secret GitLab:Token
    /// </summary>
    public required string Token { get; init; }

    /// <summary>
    /// Whether to use the mock GitLab client for testing. Defaults to false (use actual GitLab API).
    /// </summary>
    public bool UseMockClient { get; init; } = false;
}
