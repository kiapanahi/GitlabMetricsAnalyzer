using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models;

/// <summary>
/// Data quality information for a specific metric
/// </summary>
public sealed record MetricQuality
{
    public required string MetricName { get; init; }
    public required object Value { get; init; }
    public required string Quality { get; init; } // Excellent, Good, Fair, Poor
    public required double Confidence { get; init; } // 0.0 to 1.0
    public required int DataPoints { get; init; }
    public required bool IsApproximation { get; init; }
    public string? MethodologyNote { get; init; }
}

/// <summary>
/// Generic wrapper for metrics with quality indicators
/// </summary>
/// <typeparam name="T">The type of the metric value</typeparam>
public sealed record MetricWithQuality<T>
{
    public required T Value { get; init; }
    public required string Quality { get; init; } // Excellent, Good, Fair, Poor
    public required double Confidence { get; init; } // 0.0 to 1.0
    public required bool IsApproximation { get; init; }
    public string? MethodologyNote { get; init; }
    public int? DataPoints { get; init; }
}

/// <summary>
/// Quality score categories and thresholds
/// </summary>
public static class DataQualityCategories
{
    public const string Excellent = "Excellent";
    public const string Good = "Good";
    public const string Fair = "Fair";
    public const string Poor = "Poor";
    
    /// <summary>
    /// Determine quality category based on data points and period
    /// </summary>
    /// <param name="dataPoints">Number of data points</param>
    /// <param name="periodDays">Period in days</param>
    /// <returns>Quality category</returns>
    public static string DetermineQuality(int dataPoints, int periodDays)
    {
        if (periodDays <= 0) return Poor;
        
        var dataPointsPerDay = (double)dataPoints / periodDays;
        
        return dataPointsPerDay switch
        {
            >= 5 => Excellent, // >150 data points for 30 days
            >= 3 => Good,      // 50-150 data points for 30 days  
            >= 1 => Fair,      // 20-50 data points for 30 days
            _ => Poor          // <20 data points for 30 days
        };
    }
    
    /// <summary>
    /// Calculate confidence score based on data points and approximation status
    /// </summary>
    /// <param name="dataPoints">Number of data points</param>
    /// <param name="periodDays">Period in days</param>
    /// <param name="isApproximation">Whether the metric is approximated</param>
    /// <returns>Confidence score between 0.0 and 1.0</returns>
    public static double CalculateConfidence(int dataPoints, int periodDays, bool isApproximation = false)
    {
        if (periodDays <= 0) return 0.0;
        
        var dataPointsPerDay = (double)dataPoints / periodDays;
        
        // Base confidence from data density
        var baseConfidence = dataPointsPerDay switch
        {
            >= 5 => 0.95,
            >= 3 => 0.80,
            >= 1 => 0.60,
            _ => 0.30
        };
        
        // Reduce confidence for approximations
        if (isApproximation)
        {
            baseConfidence *= 0.8; // 20% reduction for approximations
        }
        
        return Math.Min(baseConfidence, 1.0);
    }
    
    /// <summary>
    /// Generate data quality warnings based on metrics
    /// </summary>
    /// <param name="dataPoints">Number of data points</param>
    /// <param name="periodDays">Period in days</param>
    /// <param name="approximationCount">Number of approximated metrics</param>
    /// <param name="totalMetrics">Total number of metrics</param>
    /// <returns>List of warnings</returns>
    public static List<string> GenerateWarnings(int dataPoints, int periodDays, int approximationCount, int totalMetrics)
    {
        var warnings = new List<string>();
        
        var quality = DetermineQuality(dataPoints, periodDays);
        
        if (quality == Poor)
        {
            warnings.Add("Insufficient data points may affect metric reliability. Consider extending the analysis period.");
        }
        else if (quality == Fair)
        {
            warnings.Add("Limited data points detected. Metrics should be interpreted with caution.");
        }
        
        if (approximationCount > 0)
        {
            var approximationRate = (double)approximationCount / totalMetrics;
            if (approximationRate >= 0.5)
            {
                warnings.Add($"High approximation rate ({approximationRate:P0}). {approximationCount} of {totalMetrics} key metrics are approximated.");
            }
            else if (approximationRate >= 0.25)
            {
                warnings.Add($"Several key metrics are approximated ({approximationCount} of {totalMetrics}). Review methodology for decision-making context.");
            }
        }
        
        if (periodDays < 7)
        {
            warnings.Add("Analysis period is less than one week. Short-term fluctuations may not represent typical performance.");
        }
        
        return warnings;
    }
}

/// <summary>
/// Enhanced metrics metadata with comprehensive quality information
/// </summary>
public sealed record MetricsMetadataWithQuality(
    DateTimeOffset CalculatedAt,
    string DataSource,
    int TotalDataPoints,
    DateTimeOffset? LastDataUpdate,
    string DataQuality,
    double OverallConfidenceScore,
    List<string> DataQualityWarnings,
    Dictionary<string, MetricQuality>? MetricQualities = null
) ;