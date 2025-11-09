using KuriousLabs.Management.KPIAnalysis.ApiService.Features.CrossSystemMetrics.Configuration;
using KuriousLabs.Management.KPIAnalysis.ApiService.Features.CrossSystemMetrics.Infrastructure;
using KuriousLabs.Management.KPIAnalysis.ApiService.Features.CrossSystemMetrics.Services;

namespace KuriousLabs.Management.KPIAnalysis.ApiService.Features.CrossSystemMetrics;

/// <summary>
/// Extension methods for registering cross-system metrics services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add cross-system metrics services to dependency injection
    /// </summary>
    public static IServiceCollection AddCrossSystemMetricsServices(this IServiceCollection services)
    {
        // Register configuration
        services.AddOptions<CrossSystemConfiguration>()
            .BindConfiguration(CrossSystemConfiguration.SectionName)
            .ValidateOnStart();

        // Register infrastructure services
        services.AddSingleton<IJiraIssueKeyParser, JiraIssueKeyParser>();
        services.AddSingleton<IIdentityMappingService, IdentityMappingService>();

        // Register metrics service
        services.AddScoped<ICrossSystemMetricsService, CrossSystemMetricsService>();

        // Register telemetry
        services.AddOpenTelemetry()
            .WithTracing(builder => builder.AddSource(Diagnostics.ActivitySourceName))
            .WithMetrics(builder => builder.AddMeter(Diagnostics.MeterName));

        return services;
    }
}
