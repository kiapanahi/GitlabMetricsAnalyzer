using Microsoft.EntityFrameworkCore;

namespace Toman.Management.KPIAnalysis.ApiService.Data.Extensions;

public static class DbContextExtensions
{
    public static async Task<T> UpsertAsync<T>(this DbContext context, T entity, CancellationToken cancellationToken = default)
        where T : class
    {
        var entry = context.Entry(entity);

        if (entry.IsKeySet)
        {
            entry.State = EntityState.Modified;
        }
        else
        {
            entry.State = EntityState.Added;
        }

        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public static async Task UpsertRangeAsync<T>(this DbContext context, IEnumerable<T> entities, CancellationToken cancellationToken = default)
        where T : class
    {
        foreach (var entity in entities)
        {
            var entry = context.Entry(entity);

            if (entry.IsKeySet)
            {
                entry.State = EntityState.Modified;
            }
            else
            {
                entry.State = EntityState.Added;
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
