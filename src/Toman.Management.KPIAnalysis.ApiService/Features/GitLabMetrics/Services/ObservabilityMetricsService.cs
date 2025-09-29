using System.Diagnostics.Metrics;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

/// <summary>
/// Service for emitting observability metrics for GitLab collection operations
/// </summary>
public interface IObservabilityMetricsService
{
    /// <summary>
    /// Record the duration of a collection run
    /// </summary>
    void RecordRunDuration(string runType, string status, TimeSpan duration, Guid runId);

    /// <summary>
    /// Increment GitLab API call counter
    /// </summary>
    void RecordGitLabApiCall(string endpoint, string method, int responseCode);

    /// <summary>
    /// Record developer coverage ratio
    /// </summary>
    void RecordDeveloperCoverageRatio(double ratio);

    /// <summary>
    /// Record data collection statistics
    /// </summary>
    void RecordCollectionStats(int projectsProcessed, int commitsCollected, int mergeRequestsCollected, int pipelinesCollected, int reviewEventsCollected, Guid runId);

    /// <summary>
    /// Record API error count
    /// </summary>
    void RecordApiError(string errorType, int responseCode, Guid? runId = null);

    /// <summary>
    /// Record data quality check results
    /// </summary>
    void RecordDataQualityCheck(string checkType, string status, double? score = null);
}

/// <summary>
/// Implementation of observability metrics service using OpenTelemetry metrics
/// </summary>
public sealed class ObservabilityMetricsService : IObservabilityMetricsService, IDisposable
{
    private readonly Meter _meter;
    private readonly Histogram<double> _runDurationHistogram;
    private readonly Counter<long> _gitLabApiCallsCounter;
    private readonly Gauge<double> _developerCoverageGauge;
    private readonly Counter<long> _collectionsCounter;
    private readonly Counter<long> _apiErrorsCounter;
    private readonly Counter<long> _dataQualityChecksCounter;

    public ObservabilityMetricsService()
    {
        _meter = new Meter("Toman.Management.KPIAnalysis.GitLabMetrics", Constants.ServiceVersion);

        // Collection run duration in seconds
        _runDurationHistogram = _meter.CreateHistogram<double>(
            "metrics_run_duration_seconds",
            description: "Duration of GitLab collection runs in seconds");

        // GitLab API calls counter
        _gitLabApiCallsCounter = _meter.CreateCounter<long>(
            "gitlab_api_calls_total",
            description: "Total number of GitLab API calls made");

        // Developer coverage ratio (active/total developers)
        _developerCoverageGauge = _meter.CreateGauge<double>(
            "developer_coverage_ratio",
            description: "Ratio of active developers to total developers");

        // Data collections counter
        _collectionsCounter = _meter.CreateCounter<long>(
            "data_collections_total",
            description: "Total number of data items collected by type");

        // API errors counter
        _apiErrorsCounter = _meter.CreateCounter<long>(
            "api_errors_total",
            description: "Total number of API errors by type and response code");

        // Data quality checks counter
        _dataQualityChecksCounter = _meter.CreateCounter<long>(
            "data_quality_checks_total",
            description: "Total number of data quality checks performed");
    }

    public void RecordRunDuration(string runType, string status, TimeSpan duration, Guid runId)
    {
        _runDurationHistogram.Record(duration.TotalSeconds, new KeyValuePair<string, object?>("run_type", runType),
                                                           new KeyValuePair<string, object?>("status", status),
                                                           new KeyValuePair<string, object?>("run_id", runId.ToString()));
    }

    public void RecordGitLabApiCall(string endpoint, string method, int responseCode)
    {
        _gitLabApiCallsCounter.Add(1, new KeyValuePair<string, object?>("endpoint", endpoint),
                                     new KeyValuePair<string, object?>("method", method),
                                     new KeyValuePair<string, object?>("response_code", responseCode.ToString()));
    }

    public void RecordDeveloperCoverageRatio(double ratio)
    {
        _developerCoverageGauge.Record(ratio);
    }

    public void RecordCollectionStats(int projectsProcessed, int commitsCollected, int mergeRequestsCollected, int pipelinesCollected, int reviewEventsCollected, Guid runId)
    {
        var runIdTag = new KeyValuePair<string, object?>("run_id", runId.ToString());

        _collectionsCounter.Add(projectsProcessed, new KeyValuePair<string, object?>("type", "projects"), runIdTag);
        _collectionsCounter.Add(commitsCollected, new KeyValuePair<string, object?>("type", "commits"), runIdTag);
        _collectionsCounter.Add(mergeRequestsCollected, new KeyValuePair<string, object?>("type", "merge_requests"), runIdTag);
        _collectionsCounter.Add(pipelinesCollected, new KeyValuePair<string, object?>("type", "pipelines"), runIdTag);
        _collectionsCounter.Add(reviewEventsCollected, new KeyValuePair<string, object?>("type", "review_events"), runIdTag);
    }

    public void RecordApiError(string errorType, int responseCode, Guid? runId = null)
    {
        var tags = new List<KeyValuePair<string, object?>>
        {
            new("error_type", errorType),
            new("response_code", responseCode.ToString())
        };

        if (runId.HasValue)
        {
            tags.Add(new KeyValuePair<string, object?>("run_id", runId.Value.ToString()));
        }

        _apiErrorsCounter.Add(1, tags.ToArray());
    }

    public void RecordDataQualityCheck(string checkType, string status, double? score = null)
    {
        var tags = new List<KeyValuePair<string, object?>>
        {
            new("check_type", checkType),
            new("status", status)
        };

        if (score.HasValue)
        {
            tags.Add(new KeyValuePair<string, object?>("score", score.Value.ToString("F2")));
        }

        _dataQualityChecksCounter.Add(1, tags.ToArray());
    }

    public void Dispose()
    {
        _meter.Dispose();
    }
}
