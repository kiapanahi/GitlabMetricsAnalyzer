using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

/// <summary>
/// Service implementation for aggregating all user metrics concurrently
/// </summary>
public sealed class UserMetricsAggregationService(
    ICommitTimeAnalysisService commitTimeAnalysisService,
    IPerDeveloperMetricsService perDeveloperMetricsService,
    ICollaborationMetricsService collaborationMetricsService,
    IQualityMetricsService qualityMetricsService,
    ICodeCharacteristicsService codeCharacteristicsService,
    IAdvancedMetricsService advancedMetricsService,
    IGitLabHttpClient gitLabHttpClient,
    ILogger<UserMetricsAggregationService> logger) : IUserMetricsAggregationService
{
    public async Task<AggregatedUserMetricsResult> GetAllUserMetricsAsync(
        long userId,
        int windowDays = 30,
        int revertDetectionDays = 30,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Starting concurrent aggregation of all metrics for user {UserId} with window of {WindowDays} days",
            userId, windowDays);

        // Fetch user info
        var user = await gitLabHttpClient.GetUserByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            throw new InvalidOperationException($"User with ID {userId} not found");
        }

        var windowStart = DateTime.UtcNow.AddDays(-windowDays);
        var windowEnd = DateTime.UtcNow;

        var errors = new Dictionary<string, string>();

        // Execute all metric calculations concurrently
        var commitTimeTask = ExecuteWithErrorHandlingAsync(
            "CommitTimeAnalysis",
            () => commitTimeAnalysisService.AnalyzeCommitTimeDistributionAsync(userId, windowDays, cancellationToken),
            errors,
            cancellationToken);

        var mrCycleTimeTask = ExecuteWithErrorHandlingAsync(
            "MrCycleTime",
            () => perDeveloperMetricsService.CalculateMrCycleTimeAsync(userId, windowDays, cancellationToken),
            errors,
            cancellationToken);

        var flowMetricsTask = ExecuteWithErrorHandlingAsync(
            "FlowMetrics",
            () => perDeveloperMetricsService.CalculateFlowMetricsAsync(userId, windowDays, cancellationToken),
            errors,
            cancellationToken);

        var collaborationMetricsTask = ExecuteWithErrorHandlingAsync(
            "CollaborationMetrics",
            () => collaborationMetricsService.CalculateCollaborationMetricsAsync(userId, windowDays, cancellationToken),
            errors,
            cancellationToken);

        var qualityMetricsTask = ExecuteWithErrorHandlingAsync(
            "QualityMetrics",
            () => qualityMetricsService.CalculateQualityMetricsAsync(userId, windowDays, revertDetectionDays, cancellationToken),
            errors,
            cancellationToken);

        var codeCharacteristicsTask = ExecuteWithErrorHandlingAsync(
            "CodeCharacteristics",
            () => codeCharacteristicsService.CalculateCodeCharacteristicsAsync(userId, windowDays, cancellationToken),
            errors,
            cancellationToken);

        var advancedMetricsTask = ExecuteWithErrorHandlingAsync(
            "AdvancedMetrics",
            () => advancedMetricsService.CalculateAdvancedMetricsAsync(userId, windowDays, cancellationToken),
            errors,
            cancellationToken);

        // Wait for all tasks to complete
        await Task.WhenAll(
            commitTimeTask,
            mrCycleTimeTask,
            flowMetricsTask,
            collaborationMetricsTask,
            qualityMetricsTask,
            codeCharacteristicsTask,
            advancedMetricsTask);

        var commitTimeResult = await commitTimeTask;
        var mrCycleTimeResult = await mrCycleTimeTask;
        var flowMetricsResult = await flowMetricsTask;
        var collaborationMetricsResult = await collaborationMetricsTask;
        var qualityMetricsResult = await qualityMetricsTask;
        var codeCharacteristicsResult = await codeCharacteristicsTask;
        var advancedMetricsResult = await advancedMetricsTask;

        logger.LogInformation(
            "Completed concurrent aggregation of all metrics for user {UserId}. Successful: {SuccessCount}, Failed: {FailureCount}",
            userId,
            7 - errors.Count,
            errors.Count);

        return new AggregatedUserMetricsResult
        {
            UserId = userId,
            Username = user.Username ?? "unknown",
            WindowDays = windowDays,
            WindowStart = windowStart,
            WindowEnd = windowEnd,
            CommitTimeAnalysis = commitTimeResult,
            MrCycleTime = mrCycleTimeResult,
            FlowMetrics = flowMetricsResult,
            CollaborationMetrics = collaborationMetricsResult,
            QualityMetrics = qualityMetricsResult,
            CodeCharacteristics = codeCharacteristicsResult,
            AdvancedMetrics = advancedMetricsResult,
            Errors = errors.Count > 0 ? errors : null
        };
    }

    private async Task<T?> ExecuteWithErrorHandlingAsync<T>(
        string metricName,
        Func<Task<T>> operation,
        Dictionary<string, string> errors,
        CancellationToken cancellationToken) where T : class
    {
        try
        {
            logger.LogDebug("Starting calculation of {MetricName}", metricName);
            var result = await operation();
            logger.LogDebug("Successfully calculated {MetricName}", metricName);
            return result;
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Failed to calculate {MetricName}: {Error}", metricName, ex.Message);
            errors[metricName] = ex.Message;
            return null;
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Invalid argument for {MetricName}: {Error}", metricName, ex.Message);
            errors[metricName] = ex.Message;
            return null;
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Calculation of {MetricName} was cancelled", metricName);
            errors[metricName] = "Operation was cancelled";
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error calculating {MetricName}: {Error}", metricName, ex.Message);
            errors[metricName] = $"Unexpected error: {ex.Message}";
            return null;
        }
    }
}
