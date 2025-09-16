using Microsoft.EntityFrameworkCore;

using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Data;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

internal sealed class MigratorBackgroundService(IHostEnvironment hostEnvironment, IServiceProvider serviceProvider) : BackgroundService
{

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var scope = serviceProvider.CreateScope();
        using var dbContext = scope.ServiceProvider.GetRequiredService<GitLabMetricsDbContext>();
        if (hostEnvironment.IsDevelopment())
        {
            // Apply migrations on startup
            await dbContext.Database.MigrateAsync().ConfigureAwait(false);
        }
    }
}
