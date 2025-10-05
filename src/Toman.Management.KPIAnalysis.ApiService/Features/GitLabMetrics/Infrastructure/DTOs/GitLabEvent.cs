using System.Text.Json.Serialization;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure.DTOs;

public sealed record GitLabEvent(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("project_id")] long? ProjectId,
    [property: JsonPropertyName("action_name")] string ActionName,
    [property: JsonPropertyName("target_type")] string? TargetType,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("push_data")] GitLabPushData? PushData,
    [property: JsonPropertyName("author")] GitLabEventAuthor? Author
);

public sealed record GitLabPushData(
    [property: JsonPropertyName("commit_count")] int CommitCount,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("ref_type")] string RefType,
    [property: JsonPropertyName("commit_from")] string? CommitFrom,
    [property: JsonPropertyName("commit_to")] string? CommitTo,
    [property: JsonPropertyName("ref")] string Ref,
    [property: JsonPropertyName("commit_title")] string? CommitTitle
);

public sealed record GitLabEventAuthor(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("name")] string Name
);
