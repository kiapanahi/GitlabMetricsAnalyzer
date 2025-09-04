using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text.Json;
using Toman.Management.KPIAnalysis.ApiService.Configuration;
using Toman.Management.KPIAnalysis.ApiService.Data;
using Toman.Management.KPIAnalysis.ApiService.Models.Export;

namespace Toman.Management.KPIAnalysis.ApiService.Services;

public interface IMetricsExportService
{
    Task<MetricsExport[]> GenerateExportsAsync(DateOnly date, CancellationToken cancellationToken = default);
    Task WriteExportsAsync(DateOnly date, CancellationToken cancellationToken = default);
    Task<string> GetExportPathAsync(DateOnly date, string format);
}

public sealed class MetricsExportService : IMetricsExportService
{
    private readonly GitLabMetricsDbContext _dbContext;
    private readonly ExportsConfiguration _exportsConfig;
    private readonly ILogger<MetricsExportService> _logger;

    public MetricsExportService(
        GitLabMetricsDbContext dbContext,
        IOptions<ExportsConfiguration> exportsConfig,
        ILogger<MetricsExportService> logger)
    {
        _dbContext = dbContext;
        _exportsConfig = exportsConfig.Value;
        _logger = logger;
    }

    public async Task<MetricsExport[]> GenerateExportsAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating exports for {Date}", date);

        var exports = new List<MetricsExport>();

        // Get all active projects
        var projects = await _dbContext.DimProjects
            .Where(p => p.ActiveFlag)
            .ToListAsync(cancellationToken);

        foreach (var project in projects)
        {
            var metrics = await CalculateMetricsForProjectAsync(project.ProjectId, date, cancellationToken);
            
            // Extract team/org information from project path
            var (org, team) = ExtractOrgAndTeam(project.PathWithNamespace);

            var export = new MetricsExport(
                date.ToString("yyyy-MM-dd"),
                org,
                team,
                project.PathWithNamespace,
                metrics
            );

            exports.Add(export);
        }

        _logger.LogInformation("Generated {Count} exports for {Date}", exports.Count, date);
        return exports.ToArray();
    }

    public async Task WriteExportsAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        var exports = await GenerateExportsAsync(date, cancellationToken);

        // Write JSON export
        var jsonPath = await GetExportPathAsync(date, "json");
        await WriteJsonExportAsync(jsonPath, exports, cancellationToken);

        // Write CSV export
        var csvPath = await GetExportPathAsync(date, "csv");
        await WriteCsvExportAsync(csvPath, exports, cancellationToken);

        _logger.LogInformation("Exported {Count} records to {JsonPath} and {CsvPath}", 
            exports.Length, jsonPath, csvPath);
    }

    public Task<string> GetExportPathAsync(DateOnly date, string format)
    {
        var fileName = $"{date:yyyy-MM-dd}.{format}";
        var path = Path.Combine(_exportsConfig.Directory, "daily", fileName);
        
        // Ensure directory exists
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        
        return Task.FromResult(path);
    }

    private async Task<MetricsData> CalculateMetricsForProjectAsync(int projectId, DateOnly date, CancellationToken cancellationToken)
    {
        var dateTime = date.ToDateTime(TimeOnly.MinValue);
        var startDate = dateTime.AddDays(-30); // 30-day window
        var endDate = dateTime.AddDays(1);

        // MR Metrics
        var mrFacts = await _dbContext.FactMergeRequests
            .Where(f => f.ProjectId == projectId)
            .ToListAsync(cancellationToken);

        var mrCycleTimeP50H = CalculatePercentile(mrFacts.Select(f => f.CycleTimeHours), 0.5m);
        var timeToFirstReviewP50H = CalculatePercentile(mrFacts.Select(f => f.ReviewWaitHours), 0.5m);
        var reworkRate = mrFacts.Count > 0 ? mrFacts.Count(f => f.ReworkCount > 0) / (decimal)mrFacts.Count : 0;

        // Pipeline Metrics
        var pipelineFacts = await _dbContext.FactPipelines
            .Where(f => f.ProjectId == projectId)
            .ToListAsync(cancellationToken);

        var pipelineSuccessRate = pipelineFacts.Count > 0 
            ? pipelineFacts.Count(f => f.DurationSec > 0) / (decimal)pipelineFacts.Count 
            : 0;

        var meanTimeToGreenSec = pipelineFacts.Count > 0 
            ? pipelineFacts.Average(f => f.MtgSeconds) 
            : 0;

        var avgPipelineDurationSec = pipelineFacts.Count > 0 
            ? pipelineFacts.Average(f => f.DurationSec) 
            : 0;

        var flakyJobRate = pipelineFacts.Count > 0 
            ? pipelineFacts.Count(f => f.IsFlakyCandidate) / (decimal)pipelineFacts.Count 
            : 0;

        var deploymentFrequencyWk = pipelineFacts.Count(f => f.IsProd);
        var rollbackIncidence = pipelineFacts.Count(f => f.IsRollback);

        // Git Hygiene Metrics
        var hygieneFacts = await _dbContext.FactGitHygiene
            .Where(f => f.ProjectId == projectId && f.Day >= DateOnly.FromDateTime(startDate) && f.Day < DateOnly.FromDateTime(endDate))
            .ToListAsync(cancellationToken);

        var directPushesDefault = hygieneFacts.Sum(f => f.DirectPushesDefault);
        var forcePushesProtected = hygieneFacts.Sum(f => f.ForcePushesProtected);
        var unsignedCommits = hygieneFacts.Sum(f => f.UnsignedCommitCount);
        var totalCommits = Math.Max(1, hygieneFacts.Sum(f => f.DirectPushesDefault + f.UnsignedCommitCount));
        var signedCommitRatio = 1 - (unsignedCommits / (decimal)totalCommits);

        return new MetricsData(
            MrCycleTimeP50H: mrCycleTimeP50H,
            PipelineSuccessRate: pipelineSuccessRate,
            DeploymentFrequencyWk: deploymentFrequencyWk,
            ApprovalBypassRatio: 0, // Would need additional data
            TimeToFirstReviewP50H: timeToFirstReviewP50H,
            TimeInReviewP50H: 0, // Would need additional data
            ReworkRate: reworkRate,
            MrThroughputWk: mrFacts.Count,
            WipMrCount: 0, // Would need current open MRs
            WipAgeP50H: 0, // Would need current open MRs
            WipAgeP90H: 0, // Would need current open MRs
            ReleasesCadenceWk: 0, // Would need release data
            MeanTimeToGreenSec: (decimal)meanTimeToGreenSec,
            AvgPipelineDurationSec: (decimal)avgPipelineDurationSec,
            FlakyJobRate: flakyJobRate,
            RollbackIncidence: rollbackIncidence,
            DirectPushesDefault: directPushesDefault,
            ForcePushesProtected: forcePushesProtected,
            SignedCommitRatio: signedCommitRatio,
            BranchTtlP50H: 0, // Would need branch lifecycle data
            BranchTtlP90H: 0, // Would need branch lifecycle data
            IssueSlaBreachRate: 0, // Would need issue SLA data
            ReopenedIssueRate: 0, // Would need issue data
            DefectEscapeRate: 0 // Would need defect classification
        );
    }

    private static decimal CalculatePercentile(IEnumerable<decimal> values, decimal percentile)
    {
        var sorted = values.OrderBy(v => v).ToArray();
        if (sorted.Length == 0) return 0;

        var index = (int)Math.Ceiling(sorted.Length * percentile) - 1;
        return sorted[Math.Max(0, Math.Min(index, sorted.Length - 1))];
    }

    private static (string org, string team) ExtractOrgAndTeam(string pathWithNamespace)
    {
        var parts = pathWithNamespace.Split('/');
        
        if (parts.Length < 2)
            return ("Unknown", "Unknown");

        var org = parts[0];
        var team = parts.Length > 2 ? parts[1] : "Unknown";

        // Map to business lines according to PRD
        team = team.ToLowerInvariant() switch
        {
            "corporate-services" => "Corporate Services",
            "exchange" => "Exchange", 
            "c-side" => "C-Side",
            "core" or "platform" => "Platform",
            _ => team
        };

        return (org, team);
    }

    private static async Task WriteJsonExportAsync(string filePath, MetricsExport[] exports, CancellationToken cancellationToken)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var json = JsonSerializer.Serialize(exports, options);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);
    }

    private static async Task WriteCsvExportAsync(string filePath, MetricsExport[] exports, CancellationToken cancellationToken)
    {
        await using var writer = new StreamWriter(filePath);
        
        // Write CSV header
        await writer.WriteLineAsync(string.Join(",", [
            "Date", "Org", "Team", "Repo",
            "MR_Cycle_Time_P50_H", "Pipeline_Success_Rate", "Deployment_Frequency_Wk",
            "Approval_Bypass_Ratio", "Time_To_First_Review_P50_H", "Time_In_Review_P50_H",
            "Rework_Rate", "MR_Throughput_Wk", "WIP_MR_Count", "WIP_Age_P50_H", "WIP_Age_P90_H",
            "Releases_Cadence_Wk", "Mean_Time_To_Green_Sec", "Avg_Pipeline_Duration_Sec",
            "Flaky_Job_Rate", "Rollback_Incidence", "Direct_Pushes_Default", "Force_Pushes_Protected",
            "Signed_Commit_Ratio", "Branch_TTL_P50_H", "Branch_TTL_P90_H",
            "Issue_SLA_Breach_Rate", "Reopened_Issue_Rate", "Defect_Escape_Rate"
        ]));

        // Write data rows
        foreach (var export in exports)
        {
            var values = new object[]
            {
                export.Date, export.Org, export.Team, export.Repo,
                export.Metrics.MrCycleTimeP50H.ToString(CultureInfo.InvariantCulture),
                export.Metrics.PipelineSuccessRate.ToString(CultureInfo.InvariantCulture),
                export.Metrics.DeploymentFrequencyWk,
                export.Metrics.ApprovalBypassRatio.ToString(CultureInfo.InvariantCulture),
                export.Metrics.TimeToFirstReviewP50H.ToString(CultureInfo.InvariantCulture),
                export.Metrics.TimeInReviewP50H.ToString(CultureInfo.InvariantCulture),
                export.Metrics.ReworkRate.ToString(CultureInfo.InvariantCulture),
                export.Metrics.MrThroughputWk,
                export.Metrics.WipMrCount,
                export.Metrics.WipAgeP50H.ToString(CultureInfo.InvariantCulture),
                export.Metrics.WipAgeP90H.ToString(CultureInfo.InvariantCulture),
                export.Metrics.ReleasesCadenceWk,
                export.Metrics.MeanTimeToGreenSec.ToString(CultureInfo.InvariantCulture),
                export.Metrics.AvgPipelineDurationSec.ToString(CultureInfo.InvariantCulture),
                export.Metrics.FlakyJobRate.ToString(CultureInfo.InvariantCulture),
                export.Metrics.RollbackIncidence,
                export.Metrics.DirectPushesDefault,
                export.Metrics.ForcePushesProtected,
                export.Metrics.SignedCommitRatio.ToString(CultureInfo.InvariantCulture),
                export.Metrics.BranchTtlP50H.ToString(CultureInfo.InvariantCulture),
                export.Metrics.BranchTtlP90H.ToString(CultureInfo.InvariantCulture),
                export.Metrics.IssueSlaBreachRate.ToString(CultureInfo.InvariantCulture),
                export.Metrics.ReopenedIssueRate.ToString(CultureInfo.InvariantCulture),
                export.Metrics.DefectEscapeRate.ToString(CultureInfo.InvariantCulture)
            };

            await writer.WriteLineAsync(string.Join(",", values));
        }
    }
}
