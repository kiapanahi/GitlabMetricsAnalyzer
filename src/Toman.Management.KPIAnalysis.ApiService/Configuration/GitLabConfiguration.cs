namespace Toman.Management.KPIAnalysis.ApiService.Configuration;

public sealed class GitLabConfiguration
{
    public const string SectionName = "GitLab";

    public required string BaseUrl { get; init; }
    public required string Token { get; init; }
    public required string[] RootGroups { get; init; }
}
