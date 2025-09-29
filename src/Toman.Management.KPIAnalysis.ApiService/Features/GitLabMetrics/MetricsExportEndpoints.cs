using Microsoft.AspNetCore.Mvc;

using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics;

internal static class MetricsExportEndpoints
{
    internal static void MapMetricsExportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/metrics/export")
            .WithTags("Metrics Export")
            .WithOpenApi();

        group.MapPost("/catalog", ExportCatalog)
            .WithName("ExportMetricsCatalog")
            .WithSummary("Export metrics catalog")
            .WithDescription("Generates and exports the current metrics catalog to JSON");

        group.MapPost("/per-developer", ExportPerDeveloperMetrics)
            .WithName("ExportPerDeveloperMetrics")
            .WithSummary("Export per-developer metrics")
            .WithDescription("Exports per-developer metrics for specified developers and window");

        group.MapGet("/files", GetAvailableExports)
            .WithName("GetAvailableExports")
            .WithSummary("List available export files")
            .WithDescription("Returns list of all available export files with metadata");

        group.MapPost("/persist", PersistMetrics)
            .WithName("PersistComputedMetrics")
            .WithSummary("Persist computed metrics")
            .WithDescription("Computes and persists metrics aggregates to database");

        group.MapGet("/aggregates", GetPersistedAggregates)
            .WithName("GetPersistedAggregates")
            .WithSummary("Get persisted aggregates")
            .WithDescription("Retrieves persisted metrics aggregates for specified parameters");
    }

    private static async Task<IResult> ExportCatalog(
        IMetricsExportService exportService,
        CancellationToken cancellationToken)
    {
        try
        {
            var filePath = await exportService.ExportCatalogAsync(cancellationToken);
            return Results.Ok(new ExportCatalogResponse { FilePath = filePath, Success = true });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail: ex.Message,
                title: "Error exporting catalog",
                statusCode: 500);
        }
    }

    private static async Task<IResult> ExportPerDeveloperMetrics(
        [FromBody] ExportPerDeveloperMetricsRequest request,
        IMetricsExportService exportService,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await exportService.ExportPerDeveloperMetricsAsync(
                request.DeveloperIds, 
                request.WindowDays, 
                request.WindowEnd ?? DateTime.UtcNow,
                cancellationToken);

            return Results.Ok(new ExportPerDeveloperMetricsResponse
            {
                CatalogFilePath = result.CatalogFilePath,
                DataFilePaths = result.DataFilePaths,
                ExportedCount = result.ExportedCount,
                ExportedAt = result.ExportedAt,
                SchemaVersion = result.SchemaVersion,
                Success = true
            });
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { Error = ex.Message });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail: ex.Message,
                title: "Error exporting per-developer metrics",
                statusCode: 500);
        }
    }

    private static async Task<IResult> GetAvailableExports(
        IMetricsExportService exportService,
        CancellationToken cancellationToken)
    {
        try
        {
            var files = await exportService.GetAvailableExportsAsync(cancellationToken);
            return Results.Ok(new GetAvailableExportsResponse { Files = files, Success = true });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail: ex.Message,
                title: "Error retrieving export files",
                statusCode: 500);
        }
    }

    private static async Task<IResult> PersistMetrics(
        [FromBody] PersistMetricsRequest request,
        IPerDeveloperMetricsComputationService computationService,
        IMetricsAggregatesPersistenceService persistenceService,
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

            var results = await computationService.ComputeMetricsAsync(request.DeveloperIds, options, cancellationToken);
            var aggregateIds = await persistenceService.PersistAggregatesAsync(results.Values, cancellationToken);

            return Results.Ok(new PersistMetricsResponse
            {
                PersistedCount = aggregateIds.Count,
                AggregateIds = aggregateIds,
                ComputationOptions = options,
                Success = true
            });
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { Error = ex.Message });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail: ex.Message,
                title: "Error persisting metrics",
                statusCode: 500);
        }
    }

    private static async Task<IResult> GetPersistedAggregates(
        [FromQuery] long[] developerIds,
        [FromQuery] int windowDays,
        [FromQuery] DateTime? windowEnd,
        IMetricsAggregatesPersistenceService persistenceService,
        CancellationToken cancellationToken)
    {
        try
        {
            if (developerIds.Length == 0)
                return Results.BadRequest(new { Error = "At least one developer ID is required" });

            var results = await persistenceService.GetAggregatesAsync(
                developerIds, 
                windowDays, 
                windowEnd ?? DateTime.UtcNow, 
                cancellationToken);

            return Results.Ok(new GetPersistedAggregatesResponse
            {
                Aggregates = results.Select(MapToResponse).ToList(),
                Count = results.Count,
                Success = true
            });
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { Error = ex.Message });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail: ex.Message,
                title: "Error retrieving persisted aggregates",
                statusCode: 500);
        }
    }

    private static PersistedAggregateResponse MapToResponse(PerDeveloperMetricsResult result)
    {
        return new PersistedAggregateResponse
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
                // Map all metrics properties
                MrCycleTimeP50H = result.Metrics.MrCycleTimeP50H,
                TimeToFirstReviewP50H = result.Metrics.TimeToFirstReviewP50H,
                TimeInReviewP50H = result.Metrics.TimeInReviewP50H,
                WipAgeP50H = result.Metrics.WipAgeP50H,
                WipAgeP90H = result.Metrics.WipAgeP90H,
                BranchTtlP50H = result.Metrics.BranchTtlP50H,
                BranchTtlP90H = result.Metrics.BranchTtlP90H,
                
                PipelineSuccessRate = result.Metrics.PipelineSuccessRate,
                ApprovalBypassRatio = result.Metrics.ApprovalBypassRatio,
                ReworkRate = result.Metrics.ReworkRate,
                FlakyJobRate = result.Metrics.FlakyJobRate,
                SignedCommitRatio = result.Metrics.SignedCommitRatio,
                IssueSlaBreachRate = result.Metrics.IssueSlaBreachRate,
                ReopenedIssueRate = result.Metrics.ReopenedIssueRate,
                DefectEscapeRate = result.Metrics.DefectEscapeRate,
                
                DeploymentFrequencyWk = result.Metrics.DeploymentFrequencyWk,
                MrThroughputWk = result.Metrics.MrThroughputWk,
                WipMrCount = result.Metrics.WipMrCount,
                ReleasesCadenceWk = result.Metrics.ReleasesCadenceWk,
                RollbackIncidence = result.Metrics.RollbackIncidence,
                DirectPushesDefault = result.Metrics.DirectPushesDefault,
                ForcePushesProtected = result.Metrics.ForcePushesProtected,
                
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
public sealed class ExportPerDeveloperMetricsRequest
{
    public required IReadOnlyList<long> DeveloperIds { get; init; }
    public required int WindowDays { get; init; }
    public DateTime? WindowEnd { get; init; }
}

public sealed class PersistMetricsRequest
{
    public required IReadOnlyList<long> DeveloperIds { get; init; }
    public required int WindowDays { get; init; }
    public DateTime? EndDate { get; init; }
    public IReadOnlyList<long>? ProjectIds { get; init; }
    public bool? ApplyWinsorization { get; init; }
    public bool? ApplyFileExclusions { get; init; }
}

public sealed class ExportCatalogResponse
{
    public required string FilePath { get; init; }
    public required bool Success { get; init; }
}

public sealed class ExportPerDeveloperMetricsResponse
{
    public required string CatalogFilePath { get; init; }
    public required IReadOnlyList<string> DataFilePaths { get; init; }
    public required int ExportedCount { get; init; }
    public required DateTime ExportedAt { get; init; }
    public required string SchemaVersion { get; init; }
    public required bool Success { get; init; }
}

public sealed class GetAvailableExportsResponse
{
    public required IReadOnlyList<ExportFileInfo> Files { get; init; }
    public required bool Success { get; init; }
}

public sealed class PersistMetricsResponse
{
    public required int PersistedCount { get; init; }
    public required IReadOnlyList<long> AggregateIds { get; init; }
    public required MetricsComputationOptions ComputationOptions { get; init; }
    public required bool Success { get; init; }
}

public sealed class GetPersistedAggregatesResponse
{
    public required IReadOnlyList<PersistedAggregateResponse> Aggregates { get; init; }
    public required int Count { get; init; }
    public required bool Success { get; init; }
}

public sealed class PersistedAggregateResponse
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
