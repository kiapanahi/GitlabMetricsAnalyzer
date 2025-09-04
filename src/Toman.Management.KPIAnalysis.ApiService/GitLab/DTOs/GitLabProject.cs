using System.Text.Json.Serialization;

namespace Toman.Management.KPIAnalysis.ApiService.GitLab.DTOs;

public sealed record GitLabProject(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("path_with_namespace")] string PathWithNamespace,
    [property: JsonPropertyName("default_branch")] string DefaultBranch,
    [property: JsonPropertyName("visibility")] string Visibility,
    [property: JsonPropertyName("last_activity_at")] DateTimeOffset LastActivityAt
);
