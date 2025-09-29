using Microsoft.AspNetCore.Mvc;

using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics;

internal static class PerDeveloperMetricsEndpoints
{
    internal static void MapPerDeveloperMetricsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/metrics/per-developer")
            .WithTags("Per-Developer Metrics")
            .WithOpenApi();

        group.MapGet("/windows", GetSupportedWindows)
            .WithName("GetSupportedMetricsWindows")
            .WithSummary("Get supported rolling window sizes")
            .WithDescription("Returns the list of supported rolling window sizes in days for metrics computation");

        group.MapPost("/{developerId:long}/compute", ComputeMetrics)
            .WithName("ComputePerDeveloperMetrics")
            .WithSummary("Compute metrics for a specific developer")
            .WithDescription("Computes the PRD's 30 per-developer metrics over a rolling window");

        group.MapPost("/batch/compute", ComputeBatchMetrics)
            .WithName("ComputeBatchPerDeveloperMetrics")
            .WithSummary("Compute metrics for multiple developers")
            .WithDescription("Computes metrics for multiple developers in a single request");
    }

    private static async Task<IResult> GetSupportedWindows(IPerDeveloperMetricsComputationService service)
    {
        var windows = service.GetSupportedWindowDays();
        return Results.Ok(new SupportedWindowsResponse(windows));
    }

    private static async Task<IResult> ComputeMetrics(
        long developerId,
        [FromBody] ComputeMetricsRequest request,
        IPerDeveloperMetricsComputationService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var options = new MetricsComputationOptions
            {
                WindowDays = request.WindowDays,
                EndDate = request.EndDate ?? DateTime.UtcNow,
                ProjectIds = request.ProjectIds ?? Array.Empty<long>(),
                ApplyWinsorization = request.ApplyWinsorization ?? true,
                ApplyFileExclusions = request.ApplyFileExclusions ?? true
            };

            var result = await service.ComputeMetricsAsync(developerId, options, cancellationToken);
            return Results.Ok(MapToResponse(result));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { Error = ex.Message });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail: ex.Message,
                title: "Error computing metrics",
                statusCode: 500);
        }
    }

    private static async Task<IResult> ComputeBatchMetrics(
        [FromBody] ComputeBatchMetricsRequest request,
        IPerDeveloperMetricsComputationService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var options = new MetricsComputationOptions
            {
                WindowDays = request.WindowDays,
                EndDate = request.EndDate ?? DateTime.UtcNow,
                ProjectIds = request.ProjectIds ?? Array.Empty<long>(),
                ApplyWinsorization = request.ApplyWinsorization ?? true,
                ApplyFileExclusions = request.ApplyFileExclusions ?? true
            };

            var results = await service.ComputeMetricsAsync(request.DeveloperIds, options, cancellationToken);
            
            var response = new ComputeBatchMetricsResponse
            {
                Results = results.Values.Select(MapToResponse).ToList(),
                SuccessCount = results.Count,
                RequestedCount = request.DeveloperIds.Count,
                FailureCount = request.DeveloperIds.Count - results.Count
            };

            return Results.Ok(response);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { Error = ex.Message });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail: ex.Message,
                title: "Error computing batch metrics",
                statusCode: 500);
        }
    }

    private static PerDeveloperMetricsResponse MapToResponse(PerDeveloperMetricsResult result)
    {
        return new PerDeveloperMetricsResponse
        {
            DeveloperId = result.DeveloperId,
            DeveloperName = result.DeveloperName,
            DeveloperEmail = result.DeveloperEmail,
            ComputationDate = result.ComputationDate,
            WindowStart = result.WindowStart,
            WindowEnd = result.WindowEnd,
            WindowDays = result.WindowDays,
            Metrics = new PerDeveloperMetricsDto
            {
                // Cycle time and review metrics (medians in hours)
                MrCycleTimeP50H = result.Metrics.MrCycleTimeP50H,
                TimeToFirstReviewP50H = result.Metrics.TimeToFirstReviewP50H,
                TimeInReviewP50H = result.Metrics.TimeInReviewP50H,
                WipAgeP50H = result.Metrics.WipAgeP50H,
                WipAgeP90H = result.Metrics.WipAgeP90H,
                BranchTtlP50H = result.Metrics.BranchTtlP50H,
                BranchTtlP90H = result.Metrics.BranchTtlP90H,

                // Rate and ratio metrics (percentages)
                PipelineSuccessRate = result.Metrics.PipelineSuccessRate,
                ApprovalBypassRatio = result.Metrics.ApprovalBypassRatio,
                ReworkRate = result.Metrics.ReworkRate,
                FlakyJobRate = result.Metrics.FlakyJobRate,
                SignedCommitRatio = result.Metrics.SignedCommitRatio,
                IssueSlaBreachRate = result.Metrics.IssueSlaBreachRate,
                ReopenedIssueRate = result.Metrics.ReopenedIssueRate,
                DefectEscapeRate = result.Metrics.DefectEscapeRate,

                // Count-based metrics
                DeploymentFrequencyWk = result.Metrics.DeploymentFrequencyWk,
                MrThroughputWk = result.Metrics.MrThroughputWk,
                WipMrCount = result.Metrics.WipMrCount,
                ReleasesCadenceWk = result.Metrics.ReleasesCadenceWk,
                RollbackIncidence = result.Metrics.RollbackIncidence,
                DirectPushesDefault = result.Metrics.DirectPushesDefault,
                ForcePushesProtected = result.Metrics.ForcePushesProtected,

                // Duration metrics (seconds)
                MeanTimeToGreenSec = result.Metrics.MeanTimeToGreenSec,
                AvgPipelineDurationSec = result.Metrics.AvgPipelineDurationSec
            },
            Audit = new MetricsAuditDto
            {
                HasMergeRequestData = result.Audit.HasMergeRequestData,
                HasPipelineData = result.Audit.HasPipelineData,
                HasCommitData = result.Audit.HasCommitData,
                HasReviewData = result.Audit.HasReviewData,
                LowMergeRequestCount = result.Audit.LowMergeRequestCount,
                LowPipelineCount = result.Audit.LowPipelineCount,
                LowCommitCount = result.Audit.LowCommitCount,
                LowReviewCount = result.Audit.LowReviewCount,
                NullReasons = result.Audit.NullReasons,
                TotalMergeRequests = result.Audit.TotalMergeRequests,
                TotalPipelines = result.Audit.TotalPipelines,
                TotalCommits = result.Audit.TotalCommits,
                TotalReviews = result.Audit.TotalReviews,
                ExcludedFiles = result.Audit.ExcludedFiles,
                WinsorizedMetrics = result.Audit.WinsorizedMetrics,
                DataQuality = result.Audit.DataQuality,
                HasSufficientData = result.Audit.HasSufficientData
            }
        };
    }
}

// Request/Response DTOs
public sealed class ComputeMetricsRequest
{
    /// <summary>
    /// Rolling window size in days (14, 28, or 90)
    /// </summary>
    public required int WindowDays { get; init; }

    /// <summary>
    /// End date for the computation window. If not provided, uses current date.
    /// </summary>
    public DateTime? EndDate { get; init; }

    /// <summary>
    /// Optional project IDs to scope the computation to
    /// </summary>
    public IReadOnlyList<long>? ProjectIds { get; init; }

    /// <summary>
    /// Whether to apply winsorization to outlier values (default: true)
    /// </summary>
    public bool? ApplyWinsorization { get; init; }

    /// <summary>
    /// Whether to apply file exclusion rules (default: true)
    /// </summary>
    public bool? ApplyFileExclusions { get; init; }
}

public sealed class ComputeBatchMetricsRequest
{
    /// <summary>
    /// List of developer IDs to compute metrics for
    /// </summary>
    public required IReadOnlyList<long> DeveloperIds { get; init; }

    /// <summary>
    /// Rolling window size in days (14, 28, or 90)
    /// </summary>
    public required int WindowDays { get; init; }

    /// <summary>
    /// End date for the computation window. If not provided, uses current date.
    /// </summary>
    public DateTime? EndDate { get; init; }

    /// <summary>
    /// Optional project IDs to scope the computation to
    /// </summary>
    public IReadOnlyList<long>? ProjectIds { get; init; }

    /// <summary>
    /// Whether to apply winsorization to outlier values (default: true)
    /// </summary>
    public bool? ApplyWinsorization { get; init; }

    /// <summary>
    /// Whether to apply file exclusion rules (default: true)
    /// </summary>
    public bool? ApplyFileExclusions { get; init; }
}

public sealed class SupportedWindowsResponse
{
    public SupportedWindowsResponse(IReadOnlyList<int> windowDays)
    {
        WindowDays = windowDays;
    }

    /// <summary>
    /// Supported rolling window sizes in days
    /// </summary>
    public IReadOnlyList<int> WindowDays { get; }
}

public sealed class PerDeveloperMetricsResponse
{
    public required long DeveloperId { get; init; }
    public required string DeveloperName { get; init; }
    public required string DeveloperEmail { get; init; }
    public required DateTime ComputationDate { get; init; }
    public required DateTime WindowStart { get; init; }
    public required DateTime WindowEnd { get; init; }
    public required int WindowDays { get; init; }
    public required PerDeveloperMetricsDto Metrics { get; init; }
    public required MetricsAuditDto Audit { get; init; }
}

public sealed class ComputeBatchMetricsResponse
{
    public required List<PerDeveloperMetricsResponse> Results { get; init; }
    public required int SuccessCount { get; init; }
    public required int RequestedCount { get; init; }
    public required int FailureCount { get; init; }
}

public sealed class PerDeveloperMetricsDto
{
    // Cycle time and review metrics (medians in hours)
    public decimal? MrCycleTimeP50H { get; init; }
    public decimal? TimeToFirstReviewP50H { get; init; }
    public decimal? TimeInReviewP50H { get; init; }
    public decimal? WipAgeP50H { get; init; }
    public decimal? WipAgeP90H { get; init; }
    public decimal? BranchTtlP50H { get; init; }
    public decimal? BranchTtlP90H { get; init; }

    // Rate and ratio metrics (percentages)
    public decimal? PipelineSuccessRate { get; init; }
    public decimal? ApprovalBypassRatio { get; init; }
    public decimal? ReworkRate { get; init; }
    public decimal? FlakyJobRate { get; init; }
    public decimal? SignedCommitRatio { get; init; }
    public decimal? IssueSlaBreachRate { get; init; }
    public decimal? ReopenedIssueRate { get; init; }
    public decimal? DefectEscapeRate { get; init; }

    // Count-based metrics
    public int DeploymentFrequencyWk { get; init; }
    public int MrThroughputWk { get; init; }
    public int WipMrCount { get; init; }
    public int ReleasesCadenceWk { get; init; }
    public int RollbackIncidence { get; init; }
    public int DirectPushesDefault { get; init; }
    public int ForcePushesProtected { get; init; }

    // Duration metrics (seconds)
    public decimal? MeanTimeToGreenSec { get; init; }
    public decimal? AvgPipelineDurationSec { get; init; }
}

public sealed class MetricsAuditDto
{
    // Data availability flags
    public required bool HasMergeRequestData { get; init; }
    public required bool HasPipelineData { get; init; }
    public required bool HasCommitData { get; init; }
    public required bool HasReviewData { get; init; }

    // Low sample size flags
    public required bool LowMergeRequestCount { get; init; }
    public required bool LowPipelineCount { get; init; }
    public required bool LowCommitCount { get; init; }
    public required bool LowReviewCount { get; init; }

    // Null reasons for missing metrics
    public required Dictionary<string, string> NullReasons { get; init; }

    // Audit counts
    public required int TotalMergeRequests { get; init; }
    public required int TotalPipelines { get; init; }
    public required int TotalCommits { get; init; }
    public required int TotalReviews { get; init; }
    public required int ExcludedFiles { get; init; }
    public required int WinsorizedMetrics { get; init; }

    // Quality indicators
    public required string DataQuality { get; init; }
    public required bool HasSufficientData { get; init; }
}
