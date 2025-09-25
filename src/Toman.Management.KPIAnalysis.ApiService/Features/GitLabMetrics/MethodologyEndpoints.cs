using Microsoft.AspNetCore.Mvc;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Methodology;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics;

public static class MethodologyEndpoints
{
    public static WebApplication MapMethodologyEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/methodology")
            .WithTags("Methodology Documentation")
            .WithOpenApi();

        group.MapGet("/{metricName}", GetMethodology)
            .WithName("GetMethodology")
            .WithSummary("Get comprehensive methodology for a specific metric")
            .WithDescription("Returns detailed methodology information including calculation steps, data sources, limitations, and interpretation guides for executive decision-making.")
            .Produces<MethodologyInfo>(200)
            .Produces(404)
            .Produces(500);

        group.MapGet("/", GetAllMethodologies)
            .WithName("GetAllMethodologies")
            .WithSummary("Get methodology documentation for all metrics")
            .WithDescription("Returns comprehensive methodology documentation for all available metrics in the system.")
            .Produces<List<MethodologyInfo>>(200)
            .Produces(500);

        group.MapGet("/changelog", GetChangeLog)
            .WithName("GetMethodologyChangeLog")
            .WithSummary("Get methodology change log")
            .WithDescription("Returns historical log of all methodology changes for audit compliance and transparency.")
            .Produces<List<MethodologyChange>>(200)
            .Produces(500);

        group.MapGet("/changelog/{metricName}", GetChangeLogForMetric)
            .WithName("GetMethodologyChangeLogForMetric")
            .WithSummary("Get methodology change log for a specific metric")
            .WithDescription("Returns historical log of methodology changes for a specific metric.")
            .Produces<List<MethodologyChange>>(200)
            .Produces(404)
            .Produces(500);

        group.MapGet("/audit-trail", GetAuditTrail)
            .WithName("GetAuditTrail")
            .WithSummary("Get methodology audit trail")
            .WithDescription("Returns audit trail entries for compliance tracking and methodology version history.")
            .Produces<List<AuditTrailEntry>>(200)
            .Produces(500);

        group.MapGet("/search", SearchMethodologies)
            .WithName("SearchMethodologies")
            .WithSummary("Search methodology documentation")
            .WithDescription("Search across all methodology documentation by keyword for quick lookup.")
            .Produces<List<MethodologyInfo>>(200)
            .Produces(400)
            .Produces(500);

        group.MapGet("/footnote/{metricName}", GetMetricFootnote)
            .WithName("GetMetricFootnote")
            .WithSummary("Get footnote text for metric display")
            .WithDescription("Returns concise footnote text suitable for metric displays in dashboards.")
            .Produces<MetricFootnoteResponse>(200)
            .Produces(404)
            .Produces(500);

        group.MapGet("/executive-summary", GetExecutiveSummary)
            .WithName("GetExecutiveSummary")
            .WithSummary("Get executive summary of all methodologies")
            .WithDescription("Returns high-level overview of all metrics with key information for executive briefings.")
            .Produces<ExecutiveSummaryResponse>(200)
            .Produces(500);

        return app;
    }

    private static async Task<IResult> GetMethodology(
        [FromServices] IMethodologyService methodologyService,
        [FromRoute] string metricName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var methodology = await methodologyService.GetMethodologyAsync(metricName, cancellationToken);
            
            if (methodology is null)
            {
                return Results.NotFound($"Methodology for metric '{metricName}' not found");
            }

            return Results.Ok(methodology);
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Methodology Retrieval Failed",
                detail: ex.Message,
                statusCode: 500);
        }
    }

    private static async Task<IResult> GetAllMethodologies(
        [FromServices] IMethodologyService methodologyService,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var methodologies = await methodologyService.GetAllMethodologiesAsync(cancellationToken);
            return Results.Ok(methodologies);
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Methodology Retrieval Failed",
                detail: ex.Message,
                statusCode: 500);
        }
    }

    private static async Task<IResult> GetChangeLog(
        [FromServices] IMethodologyService methodologyService,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var changeLog = await methodologyService.GetChangeLogAsync(null, cancellationToken);
            return Results.Ok(changeLog);
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Change Log Retrieval Failed",
                detail: ex.Message,
                statusCode: 500);
        }
    }

    private static async Task<IResult> GetChangeLogForMetric(
        [FromServices] IMethodologyService methodologyService,
        [FromRoute] string metricName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var changeLog = await methodologyService.GetChangeLogAsync(metricName, cancellationToken);
            return Results.Ok(changeLog);
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Change Log Retrieval Failed",
                detail: ex.Message,
                statusCode: 500);
        }
    }

    private static async Task<IResult> GetAuditTrail(
        [FromServices] IMethodologyService methodologyService,
        [FromQuery] string? metricName = null,
        [FromQuery] string? fromDate = null,
        [FromQuery] string? toDate = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var from = ParseDateOrNull(fromDate);
            var to = ParseDateOrNull(toDate);

            var auditTrail = await methodologyService.GetAuditTrailAsync(metricName, from, to, cancellationToken);
            return Results.Ok(auditTrail);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { Error = ex.Message });
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Audit Trail Retrieval Failed",
                detail: ex.Message,
                statusCode: 500);
        }
    }

    private static async Task<IResult> SearchMethodologies(
        [FromServices] IMethodologyService methodologyService,
        [FromQuery] string q,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return Results.BadRequest(new { Error = "Search query 'q' parameter is required" });
            }

            var results = await methodologyService.SearchMethodologiesAsync(q, cancellationToken);
            return Results.Ok(results);
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Methodology Search Failed",
                detail: ex.Message,
                statusCode: 500);
        }
    }

    private static async Task<IResult> GetMetricFootnote(
        [FromServices] IMethodologyService methodologyService,
        [FromRoute] string metricName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var footnote = methodologyService.GetMetricFootnote(metricName);
            var link = methodologyService.GetMethodologyLink(metricName);

            var response = new MetricFootnoteResponse
            {
                MetricName = metricName,
                Footnote = footnote,
                MethodologyLink = link
            };

            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Footnote Retrieval Failed",
                detail: ex.Message,
                statusCode: 500);
        }
    }

    private static async Task<IResult> GetExecutiveSummary(
        [FromServices] IMethodologyService methodologyService,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var methodologies = await methodologyService.GetAllMethodologiesAsync(cancellationToken);
            
            var summary = new ExecutiveSummaryResponse
            {
                LastUpdated = DateTimeOffset.UtcNow,
                TotalMetrics = methodologies.Count,
                KeyLimitations = new List<string>
                {
                    "Metrics reflect GitLab activity only, not overall developer impact",
                    "Pipeline success rates may be affected by infrastructure issues",
                    "Code complexity and business value not directly measured",
                    "Collaboration scores favor teams using formal review processes"
                },
                DataFreshness = "Metrics calculated from real-time GitLab API data",
                UpdateFrequency = "On-demand calculation with up-to-date information",
                ContactInformation = "For methodology questions, contact VP Engineering or Product Engineering Director",
                Metrics = methodologies.Select(m => new MetricSummary
                {
                    Name = m.MetricName,
                    Definition = m.Definition.Length > 150 ? m.Definition[..150] + "..." : m.Definition,
                    KeyLimitations = m.Limitations.Take(3).ToList(),
                    Version = m.Version,
                    LastUpdated = m.LastUpdated
                }).ToList()
            };

            return Results.Ok(summary);
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Executive Summary Failed",
                detail: ex.Message,
                statusCode: 500);
        }
    }

    private static DateTimeOffset? ParseDateOrNull(string? dateString)
    {
        if (string.IsNullOrEmpty(dateString))
            return null;

        if (DateTimeOffset.TryParse(dateString, out var result))
            return result;

        throw new ArgumentException($"Invalid date format: {dateString}. Use ISO 8601 format (e.g., 2024-01-01T00:00:00Z).");
    }
}

// Response models
public sealed record MetricFootnoteResponse
{
    public required string MetricName { get; init; }
    public required string Footnote { get; init; }
    public required string MethodologyLink { get; init; }
}

public sealed record ExecutiveSummaryResponse
{
    public required DateTimeOffset LastUpdated { get; init; }
    public required int TotalMetrics { get; init; }
    public required List<string> KeyLimitations { get; init; }
    public required string DataFreshness { get; init; }
    public required string UpdateFrequency { get; init; }
    public required string ContactInformation { get; init; }
    public required List<MetricSummary> Metrics { get; init; }
}

public sealed record MetricSummary
{
    public required string Name { get; init; }
    public required string Definition { get; init; }
    public required List<string> KeyLimitations { get; init; }
    public required string Version { get; init; }
    public required DateTimeOffset LastUpdated { get; init; }
}