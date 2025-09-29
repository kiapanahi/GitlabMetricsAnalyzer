using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Options;

using Toman.Management.KPIAnalysis.ApiService.Configuration;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Export;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

/// <summary>
/// Implementation of metrics export service with immutable storage
/// </summary>
public sealed class MetricsExportService : IMetricsExportService
{
    private readonly IMetricCatalogService _catalogService;
    private readonly IMetricsAggregatesPersistenceService _persistenceService;
    private readonly ExportsConfiguration _configuration;
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public MetricsExportService(
        IMetricCatalogService catalogService,
        IMetricsAggregatesPersistenceService persistenceService,
        IOptions<ExportsConfiguration> configuration)
    {
        _catalogService = catalogService;
        _persistenceService = persistenceService;
        _configuration = configuration.Value;
    }

    public async Task<string> ExportCatalogAsync(CancellationToken cancellationToken = default)
    {
        var catalog = await _catalogService.GenerateCatalogAsync();
        var fileName = GenerateCatalogFileName();
        var filePath = Path.Combine(_configuration.Directory, fileName);
        
        await EnsureDirectoryExistsAsync();
        
        var json = JsonSerializer.Serialize(catalog, JsonOptions);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);
        
        return filePath;
    }

    public async Task<ExportResult> ExportPerDeveloperMetricsAsync(
        IEnumerable<long> developerIds, 
        int windowDays, 
        DateTime windowEnd,
        CancellationToken cancellationToken = default)
    {
        var results = await _persistenceService.GetAggregatesAsync(developerIds, windowDays, windowEnd, cancellationToken);
        return await ExportPerDeveloperMetricsFromResultsAsync(results, cancellationToken);
    }

    public async Task<ExportResult> ExportPerDeveloperMetricsFromResultsAsync(
        IEnumerable<PerDeveloperMetricsResult> results,
        CancellationToken cancellationToken = default)
    {
        var resultsList = results.ToList();
        var exportedAt = DateTime.UtcNow;
        
        // Generate catalog
        var catalogPath = await ExportCatalogAsync(cancellationToken);
        
        // Export per-developer metrics
        var exports = _catalogService.GeneratePerDeveloperExportsFromResults(resultsList);
        var dataFilePaths = new List<string>();
        
        await EnsureDirectoryExistsAsync();
        
        foreach (var export in exports)
        {
            var fileName = GeneratePerDeveloperFileName(export.DeveloperId, export.WindowDays, export.WindowEnd);
            var filePath = Path.Combine(_configuration.Directory, fileName);
            
            var json = JsonSerializer.Serialize(export, JsonOptions);
            await File.WriteAllTextAsync(filePath, json, cancellationToken);
            
            dataFilePaths.Add(filePath);
        }
        
        return new ExportResult
        {
            CatalogFilePath = catalogPath,
            DataFilePaths = dataFilePaths,
            ExportedCount = exports.Count,
            ExportedAt = exportedAt,
            SchemaVersion = SchemaVersion.Current
        };
    }

    public async Task<IReadOnlyList<ExportFileInfo>> GetAvailableExportsAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_configuration.Directory))
            return Array.Empty<ExportFileInfo>();
            
        var files = Directory.GetFiles(_configuration.Directory, "*.json");
        var exportFiles = new List<ExportFileInfo>();
        
        foreach (var filePath in files)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                var fileName = fileInfo.Name;
                
                var exportFileInfo = new ExportFileInfo
                {
                    FilePath = filePath,
                    FileName = fileName,
                    FileType = DetermineFileType(fileName),
                    FileSizeBytes = fileInfo.Length,
                    CreatedAt = fileInfo.CreationTimeUtc,
                    SchemaVersion = await ExtractSchemaVersionAsync(filePath, cancellationToken),
                    WindowDays = ExtractWindowDays(fileName),
                    WindowEnd = ExtractWindowEnd(fileName)
                };
                
                exportFiles.Add(exportFileInfo);
            }
            catch
            {
                // Skip files that can't be processed
                continue;
            }
        }
        
        return exportFiles.OrderByDescending(f => f.CreatedAt).ToList();
    }

    private static string GenerateCatalogFileName()
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var version = SchemaVersion.Current.Replace(".", "-");
        return $"metric_catalog_{version}_{timestamp}.json";
    }

    private static string GeneratePerDeveloperFileName(long developerId, int windowDays, DateTime windowEnd)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var version = SchemaVersion.Current.Replace(".", "-");
        var windowEndFormatted = windowEnd.ToString("yyyyMMdd");
        return $"per_developer_metrics_{version}_dev{developerId}_w{windowDays}d_end{windowEndFormatted}_{timestamp}.json";
    }

    private static string DetermineFileType(string fileName)
    {
        if (fileName.StartsWith("metric_catalog_", StringComparison.OrdinalIgnoreCase))
            return "catalog";
        if (fileName.StartsWith("per_developer_metrics_", StringComparison.OrdinalIgnoreCase))
            return "per-developer";
        return "unknown";
    }

    private static async Task<string> ExtractSchemaVersionAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            using var document = JsonDocument.Parse(json);
            
            if (document.RootElement.TryGetProperty("version", out var versionElement) ||
                document.RootElement.TryGetProperty("schemaVersion", out versionElement))
            {
                return versionElement.GetString() ?? SchemaVersion.Current;
            }
        }
        catch
        {
            // Ignore errors and return current version
        }
        
        return SchemaVersion.Current;
    }

    private static int? ExtractWindowDays(string fileName)
    {
        var match = System.Text.RegularExpressions.Regex.Match(fileName, @"_w(\d+)d_");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var windowDays))
            return windowDays;
        return null;
    }

    private static DateTime? ExtractWindowEnd(string fileName)
    {
        var match = System.Text.RegularExpressions.Regex.Match(fileName, @"_end(\d{8})_");
        if (match.Success && DateTime.TryParseExact(match.Groups[1].Value, "yyyyMMdd", null, 
            System.Globalization.DateTimeStyles.None, out var date))
        {
            return new DateTime(date.Ticks, DateTimeKind.Utc);
        }
        return null;
    }

    private async Task EnsureDirectoryExistsAsync()
    {
        if (!Directory.Exists(_configuration.Directory))
        {
            Directory.CreateDirectory(_configuration.Directory);
        }
        
        await Task.CompletedTask;
    }
}
