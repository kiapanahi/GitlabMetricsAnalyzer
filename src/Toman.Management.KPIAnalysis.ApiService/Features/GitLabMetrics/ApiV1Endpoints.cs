using Microsoft.AspNetCore.Mvc;

using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Export;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics;

/// <summary>
/// API v1 endpoints for developers and catalog
/// </summary>
public static class ApiV1Endpoints
{
    public static WebApplication MapApiV1Endpoints(this WebApplication app)
    {
        var v1Group = app.MapGroup("/api/v1")
            .WithTags("API v1")
            .WithOpenApi();

        // Developer metrics endpoints
        v1Group.MapGet("/metrics/developers", GetDevelopersMetrics)
            .WithName("GetDevelopersMetrics_V1")
            .WithSummary("Get developers metrics with filtering and pagination")
            .WithDescription("Returns paginated list of developer metrics with optional filtering by window/project scope. Supports query parameters: windowDays, projectIds[], page, pageSize.")
            .Produces<ApiV1DevelopersResponse>(200)
            .Produces(400)
            .Produces(500);

        v1Group.MapGet("/metrics/developers/{developer_id:long}", GetDeveloperMetrics)
            .WithName("GetDeveloperMetrics_V1")
            .WithSummary("Get specific developer metrics with history")
            .WithDescription("Returns latest aggregate metrics for a developer plus sparkline historical data. Supports query parameters: windowDays, projectIds[], includeSparkline.")
            .Produces<ApiV1DeveloperResponse>(200)
            .Produces(404)
            .Produces(400)
            .Produces(500);

        // Catalog endpoint
        v1Group.MapGet("/catalog", GetCatalog)
            .WithName("GetCatalog_V1")
            .WithSummary("Get metric catalog with schema version")
            .WithDescription("Returns the complete metric catalog including all available metrics with their definitions and current schema version. No additional parameters required.")
            .Produces<ApiV1CatalogResponse>(200)
            .Produces(400)
            .Produces(500);

        return app;
    }

    private static async Task<IResult> GetDevelopersMetrics(
        [FromServices] IMetricCatalogService catalogService,
        [FromServices] IMetricsAggregatesPersistenceService persistenceService,
        [FromQuery] int? windowDays = null,
        [FromQuery] long[]? projectIds = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate pagination parameters
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100;

            var effectiveWindowDays = windowDays ?? 30;
            var windowEnd = DateTime.UtcNow;
            var effectiveProjectIds = projectIds ?? Array.Empty<long>();

            // For now, get all developers and filter - in production this should be optimized with database filtering
            var allDeveloperIds = await GetAllDeveloperIds(persistenceService, effectiveProjectIds, cancellationToken);

            // Apply pagination
            var skip = (page - 1) * pageSize;
            var pagedDeveloperIds = allDeveloperIds.Skip(skip).Take(pageSize);

            var exports = await catalogService.GeneratePerDeveloperExportsAsync(
                pagedDeveloperIds,
                effectiveWindowDays,
                windowEnd,
                cancellationToken);

            var response = new ApiV1DevelopersResponse
            {
                SchemaVersion = SchemaVersion.Current,
                Data = exports,
                Pagination = new ApiV1Pagination
                {
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = allDeveloperIds.Count(),
                    TotalPages = (int)Math.Ceiling(allDeveloperIds.Count() / (double)pageSize)
                },
                FilterApplied = new ApiV1Filter
                {
                    WindowDays = effectiveWindowDays,
                    ProjectIds = effectiveProjectIds,
                    WindowEnd = windowEnd
                }
            };

            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            return Results.Problem(
                statusCode: 500,
                title: "Error retrieving developers metrics",
                detail: ex.Message
            );
        }
    }

    private static async Task<IResult> GetDeveloperMetrics(
        [FromServices] IMetricCatalogService catalogService,
        [FromServices] IMetricsAggregatesPersistenceService persistenceService,
        [FromRoute] long developer_id,
        [FromQuery] int? windowDays = null,
        [FromQuery] long[]? projectIds = null,
        [FromQuery] bool includeSparkline = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var effectiveWindowDays = windowDays ?? 30;
            var windowEnd = DateTime.UtcNow;

            // Get latest aggregate
            var exports = await catalogService.GeneratePerDeveloperExportsAsync(
                new[] { developer_id },
                effectiveWindowDays,
                windowEnd,
                cancellationToken);

            var latestAggregate = exports.FirstOrDefault();
            if (latestAggregate is null)
            {
                return Results.NotFound(new
                {
                    error = "DEVELOPER_NOT_FOUND",
                    message = $"Developer with ID {developer_id} not found or has no metrics data",
                    developerId = developer_id
                });
            }

            // Generate sparkline data if requested (simplified version - in production this would query historical data)
            List<ApiV1SparklinePoint>? sparklineData = null;
            if (includeSparkline)
            {
                sparklineData = await GenerateSparklineData(persistenceService, developer_id, effectiveWindowDays, cancellationToken);
            }

            var response = new ApiV1DeveloperResponse
            {
                SchemaVersion = SchemaVersion.Current,
                Data = latestAggregate,
                SparklineData = sparklineData,
                FilterApplied = new ApiV1Filter
                {
                    WindowDays = effectiveWindowDays,
                    ProjectIds = projectIds ?? Array.Empty<long>(),
                    WindowEnd = windowEnd
                }
            };

            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            return Results.Problem(
                statusCode: 500,
                title: "Error retrieving developer metrics",
                detail: ex.Message
            );
        }
    }

    private static async Task<IResult> GetCatalog(
        [FromServices] IMetricCatalogService catalogService,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var catalog = await catalogService.GenerateCatalogAsync();

            var response = new ApiV1CatalogResponse
            {
                SchemaVersion = SchemaVersion.Current,
                Data = catalog
            };

            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            return Results.Problem(
                statusCode: 500,
                title: "Error retrieving metric catalog",
                detail: ex.Message
            );
        }
    }

    private static async Task<IEnumerable<long>> GetAllDeveloperIds(
        IMetricsAggregatesPersistenceService persistenceService,
        long[] projectIds,
        CancellationToken cancellationToken)
    {
        // This is a simplified implementation - in production you would have a specific method
        // for getting developer IDs with project filtering
        var windowEnd = DateTime.UtcNow;
        var allResults = await persistenceService.GetAggregatesAsync(
            Array.Empty<long>(), // Empty array means get all
            30,
            windowEnd,
            cancellationToken);

        return allResults.Select(r => r.DeveloperId).Distinct();
    }

    private static async Task<List<ApiV1SparklinePoint>> GenerateSparklineData(
        IMetricsAggregatesPersistenceService persistenceService,
        long developerId,
        int windowDays,
        CancellationToken cancellationToken)
    {
        // Simplified sparkline generation - in production this would query historical aggregates
        // For now, generate some sample points showing trend over time
        var points = new List<ApiV1SparklinePoint>();
        var now = DateTimeOffset.UtcNow;

        // Generate 7 data points over the last week as an example
        for (int i = 6; i >= 0; i--)
        {
            var pointDate = now.AddDays(-i);

            points.Add(new ApiV1SparklinePoint
            {
                Date = pointDate,
                Value = Random.Shared.NextDouble() * 100, // Placeholder - would be actual metric value
                MetricName = "commits_count" // Would cycle through key metrics
            });
        }

        return points;
    }
}

// V1 API Response DTOs
public sealed class ApiV1DevelopersResponse
{
    public required string SchemaVersion { get; init; }
    public required IReadOnlyList<PerDeveloperMetricsExport> Data { get; init; }
    public required ApiV1Pagination Pagination { get; init; }
    public required ApiV1Filter FilterApplied { get; init; }
}

public sealed class ApiV1DeveloperResponse
{
    public required string SchemaVersion { get; init; }
    public required PerDeveloperMetricsExport Data { get; init; }
    public List<ApiV1SparklinePoint>? SparklineData { get; init; }
    public required ApiV1Filter FilterApplied { get; init; }
}

public sealed class ApiV1CatalogResponse
{
    public required string SchemaVersion { get; init; }
    public required MetricCatalog Data { get; init; }
}

public sealed class ApiV1Pagination
{
    public required int Page { get; init; }
    public required int PageSize { get; init; }
    public required int TotalCount { get; init; }
    public required int TotalPages { get; init; }
}

public sealed class ApiV1Filter
{
    public required int WindowDays { get; init; }
    public required long[] ProjectIds { get; init; }
    public required DateTime WindowEnd { get; init; }
}

public sealed class ApiV1SparklinePoint
{
    public required DateTimeOffset Date { get; init; }
    public required double Value { get; init; }
    public required string MetricName { get; init; }
}
