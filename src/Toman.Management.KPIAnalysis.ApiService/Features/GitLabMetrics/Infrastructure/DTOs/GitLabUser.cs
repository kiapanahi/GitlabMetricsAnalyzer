using System.Text.Json.Serialization;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure.DTOs;

public sealed record GitLabUser(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("email")] string? Email,
    [property: JsonPropertyName("bot")] bool Bot
);
