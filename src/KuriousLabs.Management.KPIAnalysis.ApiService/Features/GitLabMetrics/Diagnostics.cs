using System.Diagnostics;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics;

internal static class Diagnostics
{
    internal static readonly ActivitySource ActivitySource = new("Toman.Management.KPIAnalysis.ApiService.GitLabMetrics", Constants.ServiceVersion);
}
