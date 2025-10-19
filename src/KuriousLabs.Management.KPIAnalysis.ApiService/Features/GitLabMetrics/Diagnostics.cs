using System.Diagnostics;

using static KuriousLabs.Management.KPIAnalysis.Constants;

namespace KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics;

internal static class Diagnostics
{
    internal static readonly ActivitySource ActivitySource = new("KuriousLabs.Management.KPIAnalysis.ApiService.GitLabMetrics", ServiceVersion);
}
