using System.Text.Json.Serialization;

namespace KuriousLabs.Management.KPIAnalysis.ApiService.Features.JiraMetrics.Infrastructure.DTOs;

/// <summary>
/// DTO for Jira Project from API responses
/// </summary>
public sealed record JiraProject
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("key")]
    public required string Key { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("projectTypeKey")]
    public string? ProjectTypeKey { get; init; }

    [JsonPropertyName("lead")]
    public JiraUser? Lead { get; init; }
}
