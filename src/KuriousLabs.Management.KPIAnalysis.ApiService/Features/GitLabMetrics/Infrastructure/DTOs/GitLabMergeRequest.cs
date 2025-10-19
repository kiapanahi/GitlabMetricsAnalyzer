using System.Text.Json.Serialization;

namespace KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure.DTOs;

public sealed record GitLabMergeRequest(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("iid")] int Iid,
    [property: JsonPropertyName("project_id")] int ProjectId,
    [property: JsonPropertyName("author")] GitLabUser Author,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("merged_at")] DateTimeOffset? MergedAt,
    [property: JsonPropertyName("closed_at")] DateTimeOffset? ClosedAt,
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("changes_count")] string? ChangesCount,
    [property: JsonPropertyName("source_branch")] string SourceBranch,
    [property: JsonPropertyName("target_branch")] string TargetBranch,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("labels")] List<string>? Labels,
    [property: JsonPropertyName("has_conflicts")] bool HasConflicts,
    [property: JsonPropertyName("squash")] bool Squash
);
