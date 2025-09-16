namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;

public sealed class RawCommit
{
    public long Id { get; init; } // Auto-incrementing primary key
    public required long ProjectId { get; init; }
    public required string ProjectName { get; init; }
    public required string CommitId { get; init; }
    public required long AuthorUserId { get; init; }
    public required string AuthorName { get; init; }
    public required string AuthorEmail { get; init; }
    public DateTimeOffset CommittedAt { get; init; }
    public required string Message { get; init; }
    public int Additions { get; init; }
    public int Deletions { get; init; }
    public bool IsSigned { get; init; }
    public DateTimeOffset IngestedAt { get; init; }
}
