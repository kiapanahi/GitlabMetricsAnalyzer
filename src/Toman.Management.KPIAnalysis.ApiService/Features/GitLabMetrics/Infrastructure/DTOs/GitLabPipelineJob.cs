using System.Text.Json.Serialization;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure.DTOs;

public sealed record GitLabPipelineJob(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("stage")] string Stage,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("started_at")] DateTimeOffset? StartedAt,
    [property: JsonPropertyName("finished_at")] DateTimeOffset? FinishedAt,
    [property: JsonPropertyName("duration")] double? Duration,
    [property: JsonPropertyName("queued_duration")] double? QueuedDuration,
    [property: JsonPropertyName("coverage")] string? Coverage,
    [property: JsonPropertyName("user")] GitLabUser? User,
    [property: JsonPropertyName("pipeline")] GitLabJobPipeline? Pipeline,
    [property: JsonPropertyName("web_url")] string? WebUrl,
    [property: JsonPropertyName("allow_failure")] bool AllowFailure,
    [property: JsonPropertyName("tag")] bool Tag
);

public sealed record GitLabJobPipeline(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("sha")] string Sha,
    [property: JsonPropertyName("ref")] string Ref,
    [property: JsonPropertyName("status")] string Status
);
