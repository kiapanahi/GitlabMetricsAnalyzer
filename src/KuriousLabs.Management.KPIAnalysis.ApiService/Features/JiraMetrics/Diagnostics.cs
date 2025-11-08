using System.Diagnostics;
using System.Diagnostics.Metrics;

using static KuriousLabs.Management.KPIAnalysis.Constants;

namespace KuriousLabs.Management.KPIAnalysis.ApiService.Features.JiraMetrics;

internal static class Diagnostics
{
    internal static readonly ActivitySource ActivitySource = new("KuriousLabs.Management.KPIAnalysis.ApiService.JiraMetrics", ServiceVersion);
    internal static readonly Meter Meter = new("KuriousLabs.Management.KPIAnalysis.ApiService.JiraMetrics", ServiceVersion);
}
