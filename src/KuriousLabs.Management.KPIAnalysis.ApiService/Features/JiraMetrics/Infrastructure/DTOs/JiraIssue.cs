using System.Text.Json.Serialization;

namespace KuriousLabs.Management.KPIAnalysis.ApiService.Features.JiraMetrics.Infrastructure.DTOs;

/// <summary>
/// DTO for Jira Issue from API responses
/// </summary>
public sealed record JiraIssue
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("key")]
    public required string Key { get; init; }

    [JsonPropertyName("fields")]
    public required JiraIssueFields Fields { get; init; }
}

public sealed record JiraIssueFields
{
    [JsonPropertyName("summary")]
    public string? Summary { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("status")]
    public JiraStatus? Status { get; init; }

    [JsonPropertyName("issuetype")]
    public JiraIssueType? IssueType { get; init; }

    [JsonPropertyName("priority")]
    public JiraPriority? Priority { get; init; }

    [JsonPropertyName("assignee")]
    public JiraUser? Assignee { get; init; }

    [JsonPropertyName("reporter")]
    public JiraUser? Reporter { get; init; }

    [JsonPropertyName("created")]
    public DateTime? Created { get; init; }

    [JsonPropertyName("updated")]
    public DateTime? Updated { get; init; }

    [JsonPropertyName("resolutiondate")]
    public DateTime? ResolutionDate { get; init; }

    [JsonPropertyName("project")]
    public JiraProject? Project { get; init; }
}

public sealed record JiraStatus
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("statusCategory")]
    public JiraStatusCategory? StatusCategory { get; init; }
}

public sealed record JiraStatusCategory
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("key")]
    public string? Key { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }
}

public sealed record JiraIssueType
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("subtask")]
    public bool Subtask { get; init; }
}

public sealed record JiraPriority
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }
}
