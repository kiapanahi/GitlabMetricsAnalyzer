namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Entities;

/// <summary>
/// Central developer identity with aliases support
/// </summary>
public sealed class Developer
{
    public long Id { get; init; }
    public long GitLabUserId { get; init; }
    public required string PrimaryEmail { get; init; }
    public required string PrimaryUsername { get; init; }
    public required string DisplayName { get; init; }
    public bool IsActive { get; init; } = true;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public ICollection<DeveloperAlias> Aliases { get; init; } = new List<DeveloperAlias>();
    public ICollection<CommitFact> Commits { get; init; } = new List<CommitFact>();
    public ICollection<MergeRequestFact> MergeRequests { get; init; } = new List<MergeRequestFact>();
    public ICollection<PipelineFact> Pipelines { get; init; } = new List<PipelineFact>();
    public ICollection<ReviewEvent> ReviewsGiven { get; init; } = new List<ReviewEvent>();
    public ICollection<DeveloperMetricsAggregate> MetricsAggregates { get; init; } = new List<DeveloperMetricsAggregate>();
}