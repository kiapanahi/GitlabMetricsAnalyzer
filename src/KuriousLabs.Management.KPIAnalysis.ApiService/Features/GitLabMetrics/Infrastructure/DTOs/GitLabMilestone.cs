using System.Text.Json.Serialization;

namespace KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure.DTOs;

public sealed record GitLabMilestone(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("iid")] int Iid,
    [property: JsonPropertyName("project_id")] int ProjectId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt,
    [property: JsonPropertyName("due_date")] DateOnly? DueDate,
    [property: JsonPropertyName("start_date")] DateOnly? StartDate,
    [property: JsonPropertyName("expired")] bool Expired,
    [property: JsonPropertyName("web_url")] string WebUrl
);
