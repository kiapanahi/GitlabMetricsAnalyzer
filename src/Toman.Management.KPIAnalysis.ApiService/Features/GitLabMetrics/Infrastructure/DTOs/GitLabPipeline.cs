using System.Text.Json.Serialization;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure.DTOs;

public sealed record GitLabPipeline(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("sha")] string Sha,
    [property: JsonPropertyName("ref")] string Ref,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt,
    [property: JsonPropertyName("duration")] int? Duration
);
