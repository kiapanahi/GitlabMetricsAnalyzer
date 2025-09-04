namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;

public sealed class RawCommit
{
    public required int ProjectId { get; init; }
    public required string CommitId { get; init; }
    public required int AuthorUserId { get; init; }
    public DateTimeOffset CommittedAt { get; init; }
    public int Additions { get; init; }
    public int Deletions { get; init; }
    public bool IsSigned { get; init; }
}
