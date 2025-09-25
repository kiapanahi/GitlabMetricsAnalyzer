using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Methodology;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

/// <summary>
/// Service for managing methodology documentation and audit trails
/// </summary>
public interface IMethodologyService
{
    /// <summary>
    /// Get comprehensive methodology information for a specific metric
    /// </summary>
    Task<MethodologyInfo?> GetMethodologyAsync(string metricName, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all available methodology documentation
    /// </summary>
    Task<List<MethodologyInfo>> GetAllMethodologiesAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get methodology change log for a specific metric or all metrics
    /// </summary>
    Task<List<MethodologyChange>> GetChangeLogAsync(string? metricName = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get audit trail entries for compliance tracking
    /// </summary>
    Task<List<AuditTrailEntry>> GetAuditTrailAsync(string? metricName = null, DateTimeOffset? fromDate = null, DateTimeOffset? toDate = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Search methodology documentation by keyword
    /// </summary>
    Task<List<MethodologyInfo>> SearchMethodologiesAsync(string searchTerm, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Record an audit trail entry for metric calculation
    /// </summary>
    Task RecordAuditTrailAsync(AuditTrailEntry entry, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get footnote text for a metric display
    /// </summary>
    string GetMetricFootnote(string metricName);
    
    /// <summary>
    /// Get methodology link for a metric
    /// </summary>
    string GetMethodologyLink(string metricName);
}