using System.Diagnostics;

namespace KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics;

internal static class Diagnostics
{
    internal static readonly ActivitySource ActivitySource = new("KuriousLabs.Management.KPIAnalysis.ApiService.GitLabMetrics", Constants.ServiceVersion);
}
