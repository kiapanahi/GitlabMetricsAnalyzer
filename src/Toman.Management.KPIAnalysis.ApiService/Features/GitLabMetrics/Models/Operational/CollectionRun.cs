namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Operational;

/// <summary>
/// Tracks individual collection run executions with status and statistics
/// </summary>
public sealed class CollectionRun
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string RunType { get; init; } // "incremental", "backfill"
    public required string Status { get; init; } // "running", "completed", "failed", "cancelled"
    public DateTime StartedAt { get; init; } = DateTime.UtcNow;
    public DateTimeOffset? CompletedAt { get; init; }
    
    // Window information for incremental runs
    public DateTime? WindowStart { get; set; }
    public DateTime? WindowEnd { get; set; }
    public int? WindowSizeHours { get; set; }
    
    // Statistics
    public int ProjectsProcessed { get; init; }
    public int CommitsCollected { get; init; }
    public int MergeRequestsCollected { get; init; }
    public int PipelinesCollected { get; init; }
    public int ReviewEventsCollected { get; init; }
    
    // Error information
    public string? ErrorMessage { get; init; }
    public string? ErrorDetails { get; init; }
    
    // Metadata
    public string? TriggerSource { get; init; } // "manual", "scheduled", "api"
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Request model for starting a collection run
/// </summary>
public sealed class StartCollectionRunRequest
{
    /// <summary>
    /// Type of collection run: "incremental" or "backfill"
    /// </summary>
    public required string RunType { get; init; }
    
    /// <summary>
    /// For incremental runs: size of the window in hours (optional, defaults to system setting)
    /// </summary>
    public int? WindowSizeHours { get; init; }
    
    /// <summary>
    /// For backfill runs: start date for data collection (optional, defaults to beginning of time)
    /// </summary>
    public DateTimeOffset? BackfillStartDate { get; init; }
    
    /// <summary>
    /// For backfill runs: end date for data collection (optional, defaults to now)
    /// </summary>
    public DateTimeOffset? BackfillEndDate { get; init; }
    
    /// <summary>
    /// Source that triggered this run
    /// </summary>
    public string? TriggerSource { get; init; } = "manual";
}

/// <summary>
/// Response model for collection run status
/// </summary>
public sealed class CollectionRunResponse
{
    public required Guid RunId { get; init; }
    public required string RunType { get; init; }
    public required string Status { get; init; }
    public required DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public TimeSpan? Duration => CompletedAt.HasValue ? CompletedAt.Value - StartedAt : DateTime.UtcNow - StartedAt;
    
    // Window information
    public DateTime? WindowStart { get; init; }
    public DateTime? WindowEnd { get; init; }
    public int? WindowSizeHours { get; init; }
    
    // Statistics
    public int ProjectsProcessed { get; init; }
    public int CommitsCollected { get; init; }
    public int MergeRequestsCollected { get; init; }
    public int PipelinesCollected { get; init; }
    public int ReviewEventsCollected { get; init; }
    
    // Error information
    public string? ErrorMessage { get; init; }
    public bool HasErrors => !string.IsNullOrEmpty(ErrorMessage);
}
