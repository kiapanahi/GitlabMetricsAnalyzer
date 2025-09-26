namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Configuration;

public sealed class GitLabConfiguration
{
    public const string SectionName = "GitLab";

    public required string BaseUrl { get; init; }
    
    /// <summary>
    /// GitLab Personal Access Token. Can be configured via environment variable GITLAB_TOKEN
    /// </summary>
    public required string Token { get; init; }
}
