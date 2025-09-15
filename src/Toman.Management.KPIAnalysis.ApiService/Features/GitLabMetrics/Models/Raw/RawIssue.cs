using System.Text.Json;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;

public sealed class RawIssue
{
    public long Id { get; init; } // Auto-incrementing primary key
    public required long ProjectId { get; init; }
    public required long IssueId { get; init; }
    public required long AuthorUserId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ClosedAt { get; init; }
    public required string State { get; init; }
    public int ReopenedCount { get; init; }
    public JsonDocument? Labels { get; init; }
}
