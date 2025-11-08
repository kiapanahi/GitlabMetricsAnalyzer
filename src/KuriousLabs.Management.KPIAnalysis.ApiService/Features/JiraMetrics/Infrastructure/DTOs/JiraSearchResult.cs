using System.Text.Json.Serialization;

namespace KuriousLabs.Management.KPIAnalysis.ApiService.Features.JiraMetrics.Infrastructure.DTOs;

/// <summary>
/// DTO for paginated search results from Jira API
/// </summary>
public sealed record JiraSearchResult<T>
{
    [JsonPropertyName("expand")]
    public string? Expand { get; init; }

    [JsonPropertyName("startAt")]
    public int StartAt { get; init; }

    [JsonPropertyName("maxResults")]
    public int MaxResults { get; init; }

    [JsonPropertyName("total")]
    public int Total { get; init; }

    [JsonPropertyName("issues")]
    public List<T> Issues { get; init; } = [];
}
