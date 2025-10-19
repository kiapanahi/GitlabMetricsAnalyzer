namespace KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;

public sealed class RawCommit
{
    public long Id { get; init; } // Auto-incrementing primary key
    public required long ProjectId { get; init; }
    public required string ProjectName { get; init; }
    public required string CommitId { get; init; }
    public required long AuthorUserId { get; init; }
    public required string AuthorName { get; init; }
    public required string AuthorEmail { get; init; }
    public DateTime CommittedAt { get; init; }
    public required string Message { get; init; }
    public int Additions { get; init; }
    public int Deletions { get; init; }
    public bool IsSigned { get; init; }
    public DateTime IngestedAt { get; init; }

    // Enhanced fields for commit analysis
    public int FilesChanged { get; init; }
    public int AdditionsExcluded { get; init; } // Lines added excluding filtered files
    public int DeletionsExcluded { get; init; } // Lines deleted excluding filtered files
    public int FilesChangedExcluded { get; init; } // Files changed excluding filtered files
    public bool IsMergeCommit { get; init; }
    public int ParentCount { get; init; }
    public string? ParentShas { get; init; } // JSON array of parent commit SHAs
    public string? WebUrl { get; init; }
    public string? ShortSha { get; init; }
}
