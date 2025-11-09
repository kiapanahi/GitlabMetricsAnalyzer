using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace KuriousLabs.Management.KPIAnalysis.ApiService.Features.CrossSystemMetrics;

/// <summary>
/// Provides telemetry for cross-system metrics operations
/// </summary>
internal static class Diagnostics
{
    /// <summary>
    /// Activity source name for distributed tracing
    /// </summary>
    public const string ActivitySourceName = "KuriousLabs.Management.KPIAnalysis.ApiService.CrossSystemMetrics";

    /// <summary>
    /// Meter name for metrics collection
    /// </summary>
    public const string MeterName = "KuriousLabs.Management.KPIAnalysis.ApiService.CrossSystemMetrics";

    /// <summary>
    /// Activity source for distributed tracing
    /// </summary>
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    /// <summary>
    /// Meter for collecting metrics
    /// </summary>
    public static readonly Meter Meter = new(MeterName);
}
