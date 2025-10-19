using System.Text.Json.Serialization;

namespace KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;

/// <summary>
/// Represents a project in the GitLab API contributed_projects response.
/// Based on GitLab API documentation: https://docs.gitlab.com/api/projects/#list-projects-a-user-has-contributed-to
/// </summary>
public sealed class GitLabContributedProject
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("name_with_namespace")]
    public string NameWithNamespace { get; init; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; init; } = string.Empty;

    [JsonPropertyName("path_with_namespace")]
    public string PathWithNamespace { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("description_html")]
    public string? DescriptionHtml { get; init; }

    [JsonPropertyName("default_branch")]
    public string? DefaultBranch { get; init; }

    [JsonPropertyName("visibility")]
    public string Visibility { get; init; } = string.Empty;

    [JsonPropertyName("ssh_url_to_repo")]
    public string SshUrlToRepo { get; init; } = string.Empty;

    [JsonPropertyName("http_url_to_repo")]
    public string HttpUrlToRepo { get; init; } = string.Empty;

    [JsonPropertyName("web_url")]
    public string WebUrl { get; init; } = string.Empty;

    [JsonPropertyName("readme_url")]
    public string? ReadmeUrl { get; init; }

    [JsonPropertyName("avatar_url")]
    public string? AvatarUrl { get; init; }

    [JsonPropertyName("forks_count")]
    public int ForksCount { get; init; }

    [JsonPropertyName("star_count")]
    public int StarCount { get; init; }

    [JsonPropertyName("last_activity_at")]
    public DateTime LastActivityAt { get; init; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; init; }

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; init; }

    [JsonPropertyName("namespace")]
    public GitLabNamespace? Namespace { get; init; }

    [JsonPropertyName("topics")]
    public List<string> Topics { get; init; } = [];

    [JsonPropertyName("archived")]
    public bool Archived { get; init; }

    [JsonPropertyName("issues_enabled")]
    public bool IssuesEnabled { get; init; }

    [JsonPropertyName("merge_requests_enabled")]
    public bool MergeRequestsEnabled { get; init; }

    [JsonPropertyName("wiki_enabled")]
    public bool WikiEnabled { get; init; }

    [JsonPropertyName("jobs_enabled")]
    public bool JobsEnabled { get; init; }

    [JsonPropertyName("snippets_enabled")]
    public bool SnippetsEnabled { get; init; }

    [JsonPropertyName("container_registry_enabled")]
    public bool ContainerRegistryEnabled { get; init; }
}

/// <summary>
/// Represents a namespace in the GitLab API response.
/// </summary>
public sealed class GitLabNamespace
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; init; } = string.Empty;

    [JsonPropertyName("kind")]
    public string Kind { get; init; } = string.Empty;

    [JsonPropertyName("full_path")]
    public string FullPath { get; init; } = string.Empty;

    [JsonPropertyName("parent_id")]
    public long? ParentId { get; init; }

    [JsonPropertyName("avatar_url")]
    public string? AvatarUrl { get; init; }

    [JsonPropertyName("web_url")]
    public string WebUrl { get; init; } = string.Empty;
}
