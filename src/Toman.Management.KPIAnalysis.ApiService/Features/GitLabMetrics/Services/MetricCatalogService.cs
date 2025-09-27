using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Export;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

/// <summary>
/// Implementation of metric catalog service
/// </summary>
public sealed class MetricCatalogService : IMetricCatalogService
{
    private readonly IMetricsAggregatesPersistenceService _persistenceService;

    public MetricCatalogService(IMetricsAggregatesPersistenceService persistenceService)
    {
        _persistenceService = persistenceService;
    }

    public Task<MetricCatalog> GenerateCatalogAsync()
    {
        var catalog = new MetricCatalog
        {
            Version = SchemaVersion.Current,
            GeneratedAt = DateTimeOffset.UtcNow,
            Description = "GitLab Developer Productivity Metrics Catalog - Contains all available metrics as defined in the PRD",
            Metrics = GetMetricDefinitions()
        };

        return Task.FromResult(catalog);
    }

    public async Task<IReadOnlyList<PerDeveloperMetricsExport>> GeneratePerDeveloperExportsAsync(
        IEnumerable<long> developerIds, 
        int windowDays, 
        DateTimeOffset windowEnd,
        CancellationToken cancellationToken = default)
    {
        var results = await _persistenceService.GetAggregatesAsync(developerIds, windowDays, windowEnd, cancellationToken);
        return GeneratePerDeveloperExportsFromResults(results);
    }

    public IReadOnlyList<PerDeveloperMetricsExport> GeneratePerDeveloperExportsFromResults(
        IEnumerable<PerDeveloperMetricsResult> results)
    {
        return results.Select(result => new PerDeveloperMetricsExport
        {
            SchemaVersion = SchemaVersion.Current,
            DeveloperId = result.DeveloperId,
            DeveloperName = result.DeveloperName,
            DeveloperEmail = result.DeveloperEmail,
            ComputationDate = result.ComputationDate,
            WindowStart = result.WindowStart,
            WindowEnd = result.WindowEnd,
            WindowDays = result.WindowDays,
            Metrics = result.Metrics,
            Audit = result.Audit
        }).ToList();
    }

    private static IReadOnlyList<MetricDefinition> GetMetricDefinitions()
    {
        return new[]
        {
            // Cycle time and review metrics (medians in hours)
            new MetricDefinition
            {
                Name = nameof(PerDeveloperMetrics.MrCycleTimeP50H),
                DisplayName = "MR Cycle Time (50th percentile)",
                Description = "Median time from MR creation to merge in hours",
                DataType = "decimal",
                Unit = "hours",
                Category = "Cycle Time",
                IsNullable = true,
                NullReason = "No merged MRs in the window or insufficient data"
            },
            new MetricDefinition
            {
                Name = nameof(PerDeveloperMetrics.TimeToFirstReviewP50H),
                DisplayName = "Time to First Review (50th percentile)",
                Description = "Median time from MR creation to first review in hours",
                DataType = "decimal",
                Unit = "hours",
                Category = "Review Process",
                IsNullable = true,
                NullReason = "No reviewed MRs in the window or insufficient data"
            },
            new MetricDefinition
            {
                Name = nameof(PerDeveloperMetrics.TimeInReviewP50H),
                DisplayName = "Time in Review (50th percentile)",
                Description = "Median time spent in review state before merge in hours",
                DataType = "decimal",
                Unit = "hours",
                Category = "Review Process",
                IsNullable = true,
                NullReason = "No reviewed MRs in the window or insufficient data"
            },
            new MetricDefinition
            {
                Name = nameof(PerDeveloperMetrics.WipAgeP50H),
                DisplayName = "WIP Age (50th percentile)",
                Description = "Median age of work-in-progress MRs in hours",
                DataType = "decimal",
                Unit = "hours",
                Category = "Work in Progress",
                IsNullable = true,
                NullReason = "No WIP MRs in the window"
            },
            new MetricDefinition
            {
                Name = nameof(PerDeveloperMetrics.WipAgeP90H),
                DisplayName = "WIP Age (90th percentile)",
                Description = "90th percentile age of work-in-progress MRs in hours",
                DataType = "decimal",
                Unit = "hours",
                Category = "Work in Progress",
                IsNullable = true,
                NullReason = "No WIP MRs in the window"
            },
            new MetricDefinition
            {
                Name = nameof(PerDeveloperMetrics.BranchTtlP50H),
                DisplayName = "Branch TTL (50th percentile)",
                Description = "Median branch time-to-live from creation to merge/close in hours",
                DataType = "decimal",
                Unit = "hours",
                Category = "Branch Management",
                IsNullable = true,
                NullReason = "No closed/merged branches in the window"
            },
            new MetricDefinition
            {
                Name = nameof(PerDeveloperMetrics.BranchTtlP90H),
                DisplayName = "Branch TTL (90th percentile)",
                Description = "90th percentile branch time-to-live from creation to merge/close in hours",
                DataType = "decimal",
                Unit = "hours",
                Category = "Branch Management",
                IsNullable = true,
                NullReason = "No closed/merged branches in the window"
            },

            // Rate and ratio metrics (percentages)
            new MetricDefinition
            {
                Name = nameof(PerDeveloperMetrics.PipelineSuccessRate),
                DisplayName = "Pipeline Success Rate",
                Description = "Percentage of successful pipeline runs",
                DataType = "decimal",
                Unit = "percentage",
                Category = "Pipeline Quality",
                IsNullable = true,
                NullReason = "No pipelines triggered in the window"
            },
            new MetricDefinition
            {
                Name = nameof(PerDeveloperMetrics.ApprovalBypassRatio),
                DisplayName = "Approval Bypass Ratio",
                Description = "Percentage of MRs merged without required approvals",
                DataType = "decimal",
                Unit = "percentage",
                Category = "Review Process",
                IsNullable = true,
                NullReason = "No merged MRs in the window"
            },
            new MetricDefinition
            {
                Name = nameof(PerDeveloperMetrics.ReworkRate),
                DisplayName = "Rework Rate",
                Description = "Percentage of commits that are fixes or reverts",
                DataType = "decimal",
                Unit = "percentage",
                Category = "Quality",
                IsNullable = true,
                NullReason = "No commits in the window"
            },
            new MetricDefinition
            {
                Name = nameof(PerDeveloperMetrics.FlakyJobRate),
                DisplayName = "Flaky Job Rate",
                Description = "Percentage of jobs that fail inconsistently",
                DataType = "decimal",
                Unit = "percentage",
                Category = "Pipeline Quality",
                IsNullable = true,
                NullReason = "No jobs executed in the window"
            },
            new MetricDefinition
            {
                Name = nameof(PerDeveloperMetrics.SignedCommitRatio),
                DisplayName = "Signed Commit Ratio",
                Description = "Percentage of commits that are cryptographically signed",
                DataType = "decimal",
                Unit = "percentage",
                Category = "Security",
                IsNullable = true,
                NullReason = "No commits in the window"
            },
            new MetricDefinition
            {
                Name = nameof(PerDeveloperMetrics.IssueSlaBreachRate),
                DisplayName = "Issue SLA Breach Rate",
                Description = "Percentage of issues that breach SLA targets",
                DataType = "decimal",
                Unit = "percentage",
                Category = "Issue Management",
                IsNullable = true,
                NullReason = "No issues with SLA data in the window"
            },
            new MetricDefinition
            {
                Name = nameof(PerDeveloperMetrics.ReopenedIssueRate),
                DisplayName = "Reopened Issue Rate",
                Description = "Percentage of closed issues that are reopened",
                DataType = "decimal",
                Unit = "percentage",
                Category = "Issue Management",
                IsNullable = true,
                NullReason = "No closed issues in the window"
            },
            new MetricDefinition
            {
                Name = nameof(PerDeveloperMetrics.DefectEscapeRate),
                DisplayName = "Defect Escape Rate",
                Description = "Percentage of defects that escape to production",
                DataType = "decimal",
                Unit = "percentage",
                Category = "Quality",
                IsNullable = true,
                NullReason = "No production releases or defect data in the window"
            },

            // Count-based metrics
            new MetricDefinition
            {
                Name = nameof(PerDeveloperMetrics.DeploymentFrequencyWk),
                DisplayName = "Deployment Frequency (Weekly)",
                Description = "Number of deployments per week",
                DataType = "integer",
                Unit = "count per week",
                Category = "Deployment",
                IsNullable = false
            },
            new MetricDefinition
            {
                Name = nameof(PerDeveloperMetrics.MrThroughputWk),
                DisplayName = "MR Throughput (Weekly)",
                Description = "Number of merged MRs per week",
                DataType = "integer",
                Unit = "count per week",
                Category = "Throughput",
                IsNullable = false
            },
            new MetricDefinition
            {
                Name = nameof(PerDeveloperMetrics.WipMrCount),
                DisplayName = "WIP MR Count",
                Description = "Current number of work-in-progress merge requests",
                DataType = "integer",
                Unit = "count",
                Category = "Work in Progress",
                IsNullable = false
            },
            new MetricDefinition
            {
                Name = nameof(PerDeveloperMetrics.ReleasesCadenceWk),
                DisplayName = "Releases Cadence (Weekly)",
                Description = "Number of releases per week",
                DataType = "integer",
                Unit = "count per week",
                Category = "Release Management",
                IsNullable = false
            },
            new MetricDefinition
            {
                Name = nameof(PerDeveloperMetrics.RollbackIncidence),
                DisplayName = "Rollback Incidence",
                Description = "Number of rollbacks in the window",
                DataType = "integer",
                Unit = "count",
                Category = "Reliability",
                IsNullable = false
            },
            new MetricDefinition
            {
                Name = nameof(PerDeveloperMetrics.DirectPushesDefault),
                DisplayName = "Direct Pushes to Default Branch",
                Description = "Number of direct pushes to default branch (bypassing MR process)",
                DataType = "integer",
                Unit = "count",
                Category = "Process Compliance",
                IsNullable = false
            },
            new MetricDefinition
            {
                Name = nameof(PerDeveloperMetrics.ForcePushesProtected),
                DisplayName = "Force Pushes to Protected Branches",
                Description = "Number of force pushes to protected branches",
                DataType = "integer",
                Unit = "count",
                Category = "Process Compliance",
                IsNullable = false
            },

            // Duration metrics (seconds)
            new MetricDefinition
            {
                Name = nameof(PerDeveloperMetrics.MeanTimeToGreenSec),
                DisplayName = "Mean Time to Green",
                Description = "Average time for pipelines to become successful in seconds",
                DataType = "decimal",
                Unit = "seconds",
                Category = "Pipeline Performance",
                IsNullable = true,
                NullReason = "No pipeline data in the window"
            },
            new MetricDefinition
            {
                Name = nameof(PerDeveloperMetrics.AvgPipelineDurationSec),
                DisplayName = "Average Pipeline Duration",
                Description = "Average duration of pipeline runs in seconds",
                DataType = "decimal",
                Unit = "seconds",
                Category = "Pipeline Performance",
                IsNullable = true,
                NullReason = "No completed pipelines in the window"
            }
        };
    }
}