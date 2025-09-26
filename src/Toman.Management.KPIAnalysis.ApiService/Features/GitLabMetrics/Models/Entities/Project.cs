namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Entities;

/// <summary>
/// GitLab project information
/// </summary>
public sealed class Project
{
    public long Id { get; init; } // GitLab project ID
    public required string Name { get; init; }
    public required string PathWithNamespace { get; init; }
    public string? WebUrl { get; init; }
    public string? DefaultBranch { get; init; }
    public string? VisibilityLevel { get; init; }
    public bool Archived { get; init; } = false;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset IngestedAt { get; init; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public ICollection<CommitFact> Commits { get; init; } = new List<CommitFact>();
    public ICollection<MergeRequestFact> MergeRequests { get; init; } = new List<MergeRequestFact>();
    public ICollection<PipelineFact> Pipelines { get; init; } = new List<PipelineFact>();
}