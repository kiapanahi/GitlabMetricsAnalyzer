using System.Text.Json;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;

public sealed class RawIssue
{
    public required int ProjectId { get; init; }
    public required int IssueId { get; init; }
    public required int AuthorUserId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ClosedAt { get; init; }
    public required string State { get; init; }
    public int ReopenedCount { get; init; }
    public JsonDocument? Labels { get; init; }
}
