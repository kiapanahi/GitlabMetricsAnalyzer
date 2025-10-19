namespace KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Configuration;

public sealed class GitLabConfiguration
{
    public const string SectionName = "GitLab";

    public required string BaseUrl { get; init; }

    /// <summary>
    /// GitLab Personal Access Token. Can be configured via environment variable GitLab__Token or user secret GitLab:Token
    /// </summary>
    public required string Token { get; init; }
}
