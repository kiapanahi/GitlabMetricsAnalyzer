using System.Text.Json.Serialization;

namespace KuriousLabs.Management.KPIAnalysis.ApiService.Features.JiraMetrics.Infrastructure.DTOs;

/// <summary>
/// DTO for Jira Server Info from API responses
/// Used for health checks
/// </summary>
public sealed record JiraServerInfo
{
    [JsonPropertyName("version")]
    public string? Version { get; init; }

    [JsonPropertyName("versionNumbers")]
    public List<int> VersionNumbers { get; init; } = [];

    [JsonPropertyName("deploymentType")]
    public string? DeploymentType { get; init; }

    [JsonPropertyName("buildNumber")]
    public int BuildNumber { get; init; }

    [JsonPropertyName("serverTitle")]
    public string? ServerTitle { get; init; }
}
