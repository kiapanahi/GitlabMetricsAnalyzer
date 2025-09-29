namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models;

/// <summary>
/// Schema version constants for metrics data
/// </summary>
public static class SchemaVersion
{
    /// <summary>
    /// Current schema version for metrics data
    /// </summary>
    public const string Current = "1.0.0";

    /// <summary>
    /// Supported schema versions for backward compatibility
    /// </summary>
    public static readonly IReadOnlyList<string> SupportedVersions = new[]
    {
        Current
    };

    /// <summary>
    /// Check if a schema version is supported
    /// </summary>
    public static bool IsSupported(string version) => SupportedVersions.Contains(version);
}
