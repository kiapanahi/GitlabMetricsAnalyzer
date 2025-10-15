using System.Text.Json.Serialization;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure.DTOs;

public sealed record GitLabBranch(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("merged")] bool Merged,
    [property: JsonPropertyName("protected")] bool Protected,
    [property: JsonPropertyName("developers_can_push")] bool DevelopersCanPush,
    [property: JsonPropertyName("developers_can_merge")] bool DevelopersCanMerge,
    [property: JsonPropertyName("commit")] GitLabBranchCommit Commit
);

public sealed record GitLabBranchCommit(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("short_id")] string ShortId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("author_name")] string AuthorName,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("committed_date")] DateTimeOffset CommittedDate
);
