namespace KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Raw;

/// <summary>
/// Represents a GitLab pipeline job.
/// </summary>
public sealed class GitLabPipelineJob
{
    /// <summary>
    /// The job ID.
    /// </summary>
    public required long Id { get; init; }

    /// <summary>
    /// The job name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The job status (success, failed, canceled, etc.).
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// The job stage.
    /// </summary>
    public required string Stage { get; init; }

    /// <summary>
    /// When the job was created.
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// When the job started.
    /// </summary>
    public DateTime? StartedAt { get; init; }

    /// <summary>
    /// When the job finished.
    /// </summary>
    public DateTime? FinishedAt { get; init; }

    /// <summary>
    /// Job duration in seconds.
    /// </summary>
    public double? Duration { get; init; }

    /// <summary>
    /// Time spent in queue before job started (in seconds).
    /// </summary>
    public double? QueuedDuration { get; init; }

    /// <summary>
    /// Coverage percentage from job.
    /// </summary>
    public string? Coverage { get; init; }

    /// <summary>
    /// The user who triggered the job.
    /// </summary>
    public GitLabUser? User { get; init; }

    /// <summary>
    /// The pipeline this job belongs to.
    /// </summary>
    public required long PipelineId { get; init; }

    /// <summary>
    /// The project ID.
    /// </summary>
    public required long ProjectId { get; init; }

    /// <summary>
    /// Pipeline SHA.
    /// </summary>
    public required string Sha { get; init; }

    /// <summary>
    /// Pipeline ref (branch).
    /// </summary>
    public required string Ref { get; init; }

    /// <summary>
    /// Whether the job is allowed to fail.
    /// </summary>
    public bool AllowFailure { get; init; }

    /// <summary>
    /// Whether this is a tag pipeline.
    /// </summary>
    public bool Tag { get; init; }

    /// <summary>
    /// Job web URL.
    /// </summary>
    public string? WebUrl { get; init; }
}
