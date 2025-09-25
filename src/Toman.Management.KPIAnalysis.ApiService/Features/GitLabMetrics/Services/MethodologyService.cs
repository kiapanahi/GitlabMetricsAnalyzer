using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Methodology;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

/// <summary>
/// Service for managing methodology documentation and audit trails
/// </summary>
public sealed class MethodologyService : IMethodologyService
{
    private readonly ILogger<MethodologyService> _logger;
    
    // In-memory storage for methodology information (in production this could be database-backed)
    private static readonly Dictionary<string, MethodologyInfo> _methodologies = InitializeMethodologies();
    private static readonly List<MethodologyChange> _changeLog = InitializeChangeLog();
    private static readonly List<AuditTrailEntry> _auditTrail = new();

    public MethodologyService(ILogger<MethodologyService> logger)
    {
        _logger = logger;
    }

    public Task<MethodologyInfo?> GetMethodologyAsync(string metricName, CancellationToken cancellationToken = default)
    {
        _methodologies.TryGetValue(metricName.ToLowerInvariant(), out var methodology);
        return Task.FromResult(methodology);
    }

    public Task<List<MethodologyInfo>> GetAllMethodologiesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_methodologies.Values.ToList());
    }

    public Task<List<MethodologyChange>> GetChangeLogAsync(string? metricName = null, CancellationToken cancellationToken = default)
    {
        var result = string.IsNullOrEmpty(metricName) 
            ? _changeLog 
            : _changeLog.Where(c => c.Metric.Equals(metricName, StringComparison.OrdinalIgnoreCase)).ToList();
        
        return Task.FromResult(result.OrderByDescending(c => c.ChangeDate).ToList());
    }

    public Task<List<AuditTrailEntry>> GetAuditTrailAsync(string? metricName = null, DateTimeOffset? fromDate = null, DateTimeOffset? toDate = null, CancellationToken cancellationToken = default)
    {
        var query = _auditTrail.AsEnumerable();
        
        if (!string.IsNullOrEmpty(metricName))
            query = query.Where(a => a.Metric.Equals(metricName, StringComparison.OrdinalIgnoreCase));
        
        if (fromDate.HasValue)
            query = query.Where(a => a.CalculatedAt >= fromDate.Value);
        
        if (toDate.HasValue)
            query = query.Where(a => a.CalculatedAt <= toDate.Value);
        
        return Task.FromResult(query.OrderByDescending(a => a.CalculatedAt).ToList());
    }

    public Task<List<MethodologyInfo>> SearchMethodologiesAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return Task.FromResult(_methodologies.Values.ToList());
        
        var searchTermLower = searchTerm.ToLowerInvariant();
        
        var results = _methodologies.Values
            .Where(m => 
                m.MetricName.ToLowerInvariant().Contains(searchTermLower) ||
                m.Definition.ToLowerInvariant().Contains(searchTermLower) ||
                m.Calculation.ToLowerInvariant().Contains(searchTermLower) ||
                m.DataSources.Any(d => d.Source.ToLowerInvariant().Contains(searchTermLower)) ||
                m.Limitations.Any(l => l.ToLowerInvariant().Contains(searchTermLower)))
            .ToList();
        
        return Task.FromResult(results);
    }

    public Task RecordAuditTrailAsync(AuditTrailEntry entry, CancellationToken cancellationToken = default)
    {
        _auditTrail.Add(entry);
        _logger.LogInformation("Recorded audit trail entry for metric {Metric} calculated at {CalculatedAt}", 
            entry.Metric, entry.CalculatedAt);
        return Task.CompletedTask;
    }

    public string GetMetricFootnote(string metricName)
    {
        return metricName.ToLowerInvariant() switch
        {
            "productivityscore" => "Calculated using weighted algorithm combining commit frequency, MR throughput, and pipeline success rate",
            "velocityscore" => "Measures development velocity based on commit and merge request frequency",
            "pipelinessuccessrate" => "Percentage of successful pipeline executions from total triggered pipelines",
            "collaborationscore" => "Composite score based on review participation and knowledge sharing activities",
            "codequalityscore" => "Derived from pipeline success rates, code review patterns, and revert frequencies",
            _ => "See methodology documentation for detailed calculation information"
        };
    }

    public string GetMethodologyLink(string metricName)
    {
        return $"/api/methodology/{metricName.ToLowerInvariant()}";
    }

    private static Dictionary<string, MethodologyInfo> InitializeMethodologies()
    {
        var methodologies = new Dictionary<string, MethodologyInfo>();
        
        // Productivity Score Methodology
        methodologies["productivityscore"] = new MethodologyInfo
        {
            MetricName = "ProductivityScore",
            Definition = "Composite score measuring developer output and efficiency across multiple dimensions",
            Calculation = """
            Step-by-step calculation:
            1. Calculate commits per day: commits / period_days
            2. Calculate MRs per week: merge_requests / (period_days / 7)
            3. Get pipeline success rate: successful_pipelines / total_pipelines
            4. Apply weighted formula: (commits_per_day × 2) + (mrs_per_week × 3) + (pipeline_success_rate × 5)
            5. Normalize to 0-10 scale: min(10, max(0, score))
            """,
            DataSources = new List<DataSource>
            {
                new() { Source = "GitLab commit history", Type = "exact", Description = "Direct API access to commit data" },
                new() { Source = "GitLab merge request data", Type = "exact", Description = "Complete MR lifecycle information" },
                new() { Source = "GitLab pipeline results", Type = "exact", Description = "CI/CD pipeline execution outcomes" }
            },
            Limitations = new List<string>
            {
                "Does not account for commit/MR complexity or size",
                "May favor frequent small commits over thoughtful larger ones",
                "Pipeline success influenced by infrastructure issues outside developer control",
                "No consideration for code review quality or thoroughness",
                "Weekend and holiday work patterns not normalized"
            },
            Interpretation = new InterpretationGuide
            {
                Ranges = new Dictionary<string, string>
                {
                    ["0-2.9"] = "Below expectations - significant improvement needed",
                    ["3.0-4.9"] = "Needs attention - some areas for improvement",
                    ["5.0-7.4"] = "Meeting expectations - solid performance",
                    ["7.5-10.0"] = "Exceeding expectations - excellent performance"
                },
                Notes = new List<InterpretationNote>
                {
                    new() { Context = "New developers", Explanation = "Lower scores expected during onboarding period" },
                    new() { Context = "Senior developers", Explanation = "May have lower commit frequency but higher impact contributions" },
                    new() { Context = "Project complexity", Explanation = "Complex projects may naturally result in lower velocity scores" }
                }
            },
            IndustryContext = "Based on research from Google's DORA metrics and Microsoft's developer productivity studies. Aligns with industry standards for measuring developer effectiveness.",
            LastUpdated = DateTimeOffset.UtcNow,
            Version = "2.1"
        };

        // Velocity Score Methodology
        methodologies["velocityscore"] = new MethodologyInfo
        {
            MetricName = "VelocityScore",
            Definition = "Measures development velocity based on the frequency of commits and merge requests",
            Calculation = """
            Step-by-step calculation:
            1. Calculate commits per day: commits / period_days
            2. Calculate MRs per week: merge_requests / (period_days / 7)
            3. Apply weighted formula: (commits_per_day × 2) + (mrs_per_week × 3)
            4. Normalize to 0-10 scale: min(10, result)
            """,
            DataSources = new List<DataSource>
            {
                new() { Source = "GitLab commit history", Type = "exact" },
                new() { Source = "GitLab merge request data", Type = "exact" }
            },
            Limitations = new List<string>
            {
                "Pure velocity metric without quality considerations",
                "Favors quantity over impact",
                "Does not account for work complexity"
            },
            Interpretation = new InterpretationGuide
            {
                Ranges = new Dictionary<string, string>
                {
                    ["0-3"] = "Low velocity",
                    ["4-6"] = "Moderate velocity", 
                    ["7-10"] = "High velocity"
                },
                Notes = new List<InterpretationNote>
                {
                    new() { Context = "Velocity vs Quality", Explanation = "High velocity should be balanced with code quality metrics" }
                }
            },
            LastUpdated = DateTimeOffset.UtcNow,
            Version = "1.0"
        };

        // Pipeline Success Rate Methodology
        methodologies["pipelinessuccessrate"] = new MethodologyInfo
        {
            MetricName = "PipelineSuccessRate",
            Definition = "Percentage of successful CI/CD pipeline executions indicating code quality and testing effectiveness",
            Calculation = """
            Simple calculation:
            1. Count total pipelines triggered by user
            2. Count successful pipeline executions
            3. Calculate rate: successful_pipelines / total_pipelines
            4. Express as percentage: rate × 100
            """,
            DataSources = new List<DataSource>
            {
                new() { Source = "GitLab pipeline execution logs", Type = "exact", Description = "Real-time pipeline status data" }
            },
            Limitations = new List<string>
            {
                "Infrastructure failures may impact score unfairly",
                "Flaky tests can skew results",
                "External dependency failures outside developer control",
                "Does not differentiate between different types of failures"
            },
            Interpretation = new InterpretationGuide
            {
                Ranges = new Dictionary<string, string>
                {
                    ["0-70%"] = "Poor - significant quality issues",
                    ["70-85%"] = "Fair - some improvement needed",
                    ["85-95%"] = "Good - solid quality practices",
                    ["95-100%"] = "Excellent - exceptional quality"
                },
                Notes = new List<InterpretationNote>
                {
                    new() { Context = "Industry Benchmark", Explanation = "85%+ is considered good practice in most organizations" },
                    new() { Context = "Perfect Scores", Explanation = "100% may indicate insufficient test coverage or overly conservative practices" }
                }
            },
            IndustryContext = "DORA metrics suggest high-performing teams maintain >90% success rates",
            LastUpdated = DateTimeOffset.UtcNow,
            Version = "1.0"
        };

        // Collaboration Score Methodology
        methodologies["collaborationscore"] = new MethodologyInfo
        {
            MetricName = "CollaborationScore",
            Definition = "Composite score measuring developer participation in collaborative activities and knowledge sharing",
            Calculation = """
            Complex calculation involving:
            1. Review participation: reviews_given + reviews_received
            2. Knowledge sharing: unique_reviewers + unique_reviewees
            3. Weighted scoring based on collaboration diversity and activity level
            4. Normalization to 0-10 scale
            """,
            DataSources = new List<DataSource>
            {
                new() { Source = "GitLab merge request reviews", Type = "exact" },
                new() { Source = "GitLab issue comments", Type = "exact" },
                new() { Source = "Derived reviewer relationships", Type = "derived" }
            },
            Limitations = new List<string>
            {
                "Does not measure quality of collaboration, only quantity",
                "May not capture informal knowledge sharing",
                "Biased toward teams that use formal review processes",
                "Solo developers may score artificially low"
            },
            Interpretation = new InterpretationGuide
            {
                Ranges = new Dictionary<string, string>
                {
                    ["0-3"] = "Limited collaboration",
                    ["4-6"] = "Moderate collaboration",
                    ["7-10"] = "Strong collaborative behavior"
                },
                Notes = new List<InterpretationNote>
                {
                    new() { Context = "Team Size", Explanation = "Larger teams naturally provide more collaboration opportunities" },
                    new() { Context = "Role Considerations", Explanation = "Senior developers may mentor more, junior developers may receive more reviews" }
                }
            },
            LastUpdated = DateTimeOffset.UtcNow,
            Version = "1.5"
        };

        return methodologies;
    }

    private static List<MethodologyChange> InitializeChangeLog()
    {
        return new List<MethodologyChange>
        {
            new()
            {
                Metric = "ProductivityScore",
                Version = "2.1",
                ChangeDate = new DateTimeOffset(2024, 9, 15, 0, 0, 0, TimeSpan.Zero),
                Changes = new List<string>
                {
                    "Added pipeline success rate weighting (weight: 5)",
                    "Removed weekend commit penalty",
                    "Updated team comparison normalization"
                },
                Rationale = "Feedback from VP Engineering on fairness concerns and better alignment with business outcomes",
                ApprovedBy = "VP Engineering"
            },
            new()
            {
                Metric = "ProductivityScore", 
                Version = "2.0",
                ChangeDate = new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero),
                Changes = new List<string>
                {
                    "Introduced weighted algorithm replacing simple additive model",
                    "Added MR throughput component",
                    "Normalized scoring to 0-10 range"
                },
                Rationale = "Original algorithm was too simplistic and didn't reflect actual developer impact",
                ApprovedBy = "Engineering Leadership Team"
            },
            new()
            {
                Metric = "CollaborationScore",
                Version = "1.5", 
                ChangeDate = new DateTimeOffset(2024, 8, 20, 0, 0, 0, TimeSpan.Zero),
                Changes = new List<string>
                {
                    "Added knowledge sharing diversity component",
                    "Improved handling of team size variations",
                    "Fixed bias against solo contributors"
                },
                Rationale = "Address feedback about unfairness to individual contributors and small teams",
                ApprovedBy = "Product Engineering Director"
            }
        };
    }
}