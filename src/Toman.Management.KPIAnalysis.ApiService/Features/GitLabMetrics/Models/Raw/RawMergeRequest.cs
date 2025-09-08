namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;

public sealed class RawMergeRequest
{
    public required long ProjectId { get; init; }
    public required long MrId { get; init; }
    public required long AuthorUserId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? MergedAt { get; init; }
    public DateTimeOffset? ClosedAt { get; init; }
    public required string State { get; init; }
    public int ChangesCount { get; init; }
    public required string SourceBranch { get; init; }
    public required string TargetBranch { get; init; }
    public int ApprovalsRequired { get; init; }
    public int ApprovalsGiven { get; init; }
    public DateTimeOffset? FirstReviewAt { get; init; }
}
