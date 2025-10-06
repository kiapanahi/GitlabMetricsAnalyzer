namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

/// <summary>
/// Service for enriching raw GitLab data (e.g., adding metadata, labels)
/// </summary>
public interface IDataEnrichmentService
{
    Task EnrichAsync(CancellationToken cancellationToken = default);
}
