using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Data;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Entities;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

/// <summary>
/// Implementation of metrics aggregates persistence service
/// </summary>
public sealed class MetricsAggregatesPersistenceService : IMetricsAggregatesPersistenceService
{
    private readonly GitLabMetricsDbContext _context;

    public MetricsAggregatesPersistenceService(GitLabMetricsDbContext context)
    {
        _context = context;
    }

    public async Task<long> PersistAggregateAsync(PerDeveloperMetricsResult result, CancellationToken cancellationToken = default)
    {
        var aggregate = MapToAggregate(result);
        
        _context.DeveloperMetricsAggregates.Add(aggregate);
        await _context.SaveChangesAsync(cancellationToken);
        
        return aggregate.Id;
    }

    public async Task<IReadOnlyList<long>> PersistAggregatesAsync(IEnumerable<PerDeveloperMetricsResult> results, CancellationToken cancellationToken = default)
    {
        var aggregates = results.Select(MapToAggregate).ToList();
        
        _context.DeveloperMetricsAggregates.AddRange(aggregates);
        await _context.SaveChangesAsync(cancellationToken);
        
        return aggregates.Select(a => a.Id).ToList();
    }

    public async Task<PerDeveloperMetricsResult?> GetAggregateAsync(long developerId, int windowDays, DateTimeOffset windowEnd, CancellationToken cancellationToken = default)
    {
        var windowStart = windowEnd.AddDays(-windowDays);
        
        var aggregate = await _context.DeveloperMetricsAggregates
            .Include(a => a.Developer)
            .FirstOrDefaultAsync(a => 
                a.DeveloperId == developerId && 
                a.WindowDays == windowDays && 
                a.WindowEnd == windowEnd &&
                a.WindowStart == windowStart,
                cancellationToken);

        return aggregate is null ? null : MapFromAggregate(aggregate);
    }

    public async Task<IReadOnlyList<PerDeveloperMetricsResult>> GetAggregatesAsync(IEnumerable<long> developerIds, int windowDays, DateTimeOffset windowEnd, CancellationToken cancellationToken = default)
    {
        var windowStart = windowEnd.AddDays(-windowDays);
        var developerIdsList = developerIds.ToList();
        
        var aggregates = await _context.DeveloperMetricsAggregates
            .Include(a => a.Developer)
            .Where(a => 
                developerIdsList.Contains(a.DeveloperId) && 
                a.WindowDays == windowDays && 
                a.WindowEnd == windowEnd &&
                a.WindowStart == windowStart)
            .ToListAsync(cancellationToken);

        return aggregates.Select(MapFromAggregate).ToList();
    }

    public async Task<bool> AggregateExistsAsync(long developerId, int windowDays, DateTimeOffset windowEnd, CancellationToken cancellationToken = default)
    {
        var windowStart = windowEnd.AddDays(-windowDays);
        
        return await _context.DeveloperMetricsAggregates
            .AnyAsync(a => 
                a.DeveloperId == developerId && 
                a.WindowDays == windowDays && 
                a.WindowEnd == windowEnd &&
                a.WindowStart == windowStart,
                cancellationToken);
    }

    private static DeveloperMetricsAggregate MapToAggregate(PerDeveloperMetricsResult result)
    {
        var auditJson = JsonSerializer.SerializeToDocument(result.Audit);
        var nullReasonsJson = result.Audit.NullReasons.Count > 0 
            ? JsonSerializer.SerializeToDocument(result.Audit.NullReasons)
            : null;

        return new DeveloperMetricsAggregate
        {
            DeveloperId = result.DeveloperId,
            WindowStart = result.WindowStart,
            WindowEnd = result.WindowEnd,
            WindowDays = result.WindowDays,
            SchemaVersion = SchemaVersion.Current,
            
            // PRD Metrics
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
            AvgPipelineDurationSec = result.Metrics.AvgPipelineDurationSec,
            
            AuditMetadata = auditJson,
            NullReasons = nullReasonsJson,
            CalculatedAt = result.ComputationDate
        };
    }

    private static PerDeveloperMetricsResult MapFromAggregate(DeveloperMetricsAggregate aggregate)
    {
        var audit = aggregate.AuditMetadata is not null 
            ? JsonSerializer.Deserialize<MetricsAudit>(aggregate.AuditMetadata.RootElement) 
            : new MetricsAudit();
            
        var nullReasons = aggregate.NullReasons is not null
            ? JsonSerializer.Deserialize<Dictionary<string, string>>(aggregate.NullReasons.RootElement) ?? new()
            : new Dictionary<string, string>();

        // Update audit with null reasons if they exist
        if (nullReasons.Count > 0 && audit is not null)
        {
            audit = audit with { NullReasons = nullReasons };
        }

        return new PerDeveloperMetricsResult
        {
            DeveloperId = aggregate.DeveloperId,
            DeveloperName = aggregate.Developer.DisplayName,
            DeveloperEmail = aggregate.Developer.PrimaryEmail,
            ComputationDate = aggregate.CalculatedAt,
            WindowStart = aggregate.WindowStart,
            WindowEnd = aggregate.WindowEnd,
            WindowDays = aggregate.WindowDays,
            
            Metrics = new PerDeveloperMetrics
            {
                MrCycleTimeP50H = aggregate.MrCycleTimeP50H,
                TimeToFirstReviewP50H = aggregate.TimeToFirstReviewP50H,
                TimeInReviewP50H = aggregate.TimeInReviewP50H,
                WipAgeP50H = aggregate.WipAgeP50H,
                WipAgeP90H = aggregate.WipAgeP90H,
                BranchTtlP50H = aggregate.BranchTtlP50H,
                BranchTtlP90H = aggregate.BranchTtlP90H,
                
                PipelineSuccessRate = aggregate.PipelineSuccessRate,
                ApprovalBypassRatio = aggregate.ApprovalBypassRatio,
                ReworkRate = aggregate.ReworkRate,
                FlakyJobRate = aggregate.FlakyJobRate,
                SignedCommitRatio = aggregate.SignedCommitRatio,
                IssueSlaBreachRate = aggregate.IssueSlaBreachRate,
                ReopenedIssueRate = aggregate.ReopenedIssueRate,
                DefectEscapeRate = aggregate.DefectEscapeRate,
                
                DeploymentFrequencyWk = aggregate.DeploymentFrequencyWk,
                MrThroughputWk = aggregate.MrThroughputWk,
                WipMrCount = aggregate.WipMrCount,
                ReleasesCadenceWk = aggregate.ReleasesCadenceWk,
                RollbackIncidence = aggregate.RollbackIncidence,
                DirectPushesDefault = aggregate.DirectPushesDefault,
                ForcePushesProtected = aggregate.ForcePushesProtected,
                
                MeanTimeToGreenSec = aggregate.MeanTimeToGreenSec,
                AvgPipelineDurationSec = aggregate.AvgPipelineDurationSec
            },
            
            Audit = audit ?? new MetricsAudit()
        };
    }
}