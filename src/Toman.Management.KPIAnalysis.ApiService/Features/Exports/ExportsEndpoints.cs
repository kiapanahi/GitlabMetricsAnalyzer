using Microsoft.AspNetCore.Mvc;

using Toman.Management.KPIAnalysis.ApiService.Services;

namespace Toman.Management.KPIAnalysis.ApiService.Features.Exports;

public static class ExportsEndpoints
{
    public static void MapExportsEndpoints(this WebApplication app)
    {
        app.MapGet("/exports/daily/{date}.json", async (
            [FromRoute] string date,
            [FromServices] IMetricsExportService exportService,
            CancellationToken cancellationToken) =>
        {
            if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var exportDate))
            {
                return Results.BadRequest(new { error = "Invalid date format. Expected: yyyy-MM-dd" });
            }

            try
            {
                var exports = await exportService.GenerateExportsAsync(exportDate, cancellationToken);
                return Results.Ok(exports);
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    statusCode: 500,
                    title: "Failed to generate exports",
                    detail: ex.Message
                );
            }
        })
        .WithName("GetDailyExportsJson")
        .WithTags("Exports")
        .Produces<Models.Export.MetricsExport[]>();

        app.MapGet("/exports/daily/{date}.csv", async (
            [FromRoute] string date,
            [FromServices] IMetricsExportService exportService,
            CancellationToken cancellationToken) =>
        {
            if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var exportDate))
            {
                return Results.BadRequest(new { error = "Invalid date format. Expected: yyyy-MM-dd" });
            }

            try
            {
                var filePath = await exportService.GetExportPathAsync(exportDate, "csv");
                
                if (!File.Exists(filePath))
                {
                    // Generate the CSV if it doesn't exist
                    await exportService.WriteExportsAsync(exportDate, cancellationToken);
                }

                var fileBytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
                return Results.File(fileBytes, "text/csv", $"{date}.csv");
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    statusCode: 500,
                    title: "Failed to get CSV export",
                    detail: ex.Message
                );
            }
        })
        .WithName("GetDailyExportsCsv")
        .WithTags("Exports")
        .Produces<FileResult>();
    }
}
