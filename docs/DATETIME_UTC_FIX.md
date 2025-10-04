# DateTime UTC Fix for PostgreSQL

## Problem

PostgreSQL with Entity Framework Core requires all `DateTime` values stored in `timestamp with time zone` (timestamptz) columns to have `DateTimeKind.Utc`. When attempting to save DateTime values with `DateTimeKind.Unspecified` or `DateTimeKind.Local`, the following error occurs:

```
System.ArgumentException: Cannot write DateTime with Kind=Unspecified to PostgreSQL type 'timestamp with time zone', only UTC is supported.
```

## Solution

Instead of changing the PostgreSQL schema (which is the correct approach for a metrics system), we configured Entity Framework Core to **automatically convert all DateTime values to UTC** at the DbContext level.

### Implementation

Added a global UTC conversion method in `GitLabMetricsDbContext.cs`:

```csharp
private static void ConfigureDateTimeUtcConversion(ModelBuilder modelBuilder)
{
    // Apply UTC conversion to all DateTime properties across all entities
    // Assumes all DateTime values are in local time and converts them to UTC
    foreach (var entityType in modelBuilder.Model.GetEntityTypes())
    {
        foreach (var property in entityType.GetProperties())
        {
            if (property.ClrType == typeof(DateTime))
            {
                property.SetValueConverter(
                    new ValueConverter<DateTime, DateTime>(
                        v => v.Kind == DateTimeKind.Unspecified 
                            ? DateTime.SpecifyKind(v, DateTimeKind.Local).ToUniversalTime()
                            : v.ToUniversalTime(),
                        v => DateTime.SpecifyKind(v, DateTimeKind.Utc)));
            }
            else if (property.ClrType == typeof(DateTime?))
            {
                property.SetValueConverter(
                    new ValueConverter<DateTime?, DateTime?>(
                        v => v.HasValue
                            ? v.Value.Kind == DateTimeKind.Unspecified
                                ? DateTime.SpecifyKind(v.Value, DateTimeKind.Local).ToUniversalTime()
                                : v.Value.ToUniversalTime()
                            : v,
                        v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v));
            }
        }
    }
}
```

This method is called in `OnModelCreating` after all entity configurations are complete.

### How It Works

1. **On Write (to database)**:
   - If DateTime has `Kind = Unspecified`, it's first marked as `Local` then converted to UTC using `ToUniversalTime()`
   - If DateTime has `Kind = Local` or `Utc`, it's converted to UTC using `ToUniversalTime()`
   - This ensures actual timezone conversion happens, not just Kind changes

2. **On Read (from database)**:
   - All DateTime values read from PostgreSQL are marked as UTC using `DateTime.SpecifyKind()`

### Benefits

- ✅ **Automatic**: No manual conversion needed in service code
- ✅ **Global**: Applies to all DateTime properties across all entities
- ✅ **Type-safe**: Works with both `DateTime` and `DateTime?`
- ✅ **Transparent**: Existing code continues to work without changes
- ✅ **Database-agnostic**: Follows PostgreSQL best practices for timestamp storage

### Why Keep `timestamp with time zone`?

Using `timestamp with time zone` (timestamptz) in PostgreSQL is the **recommended best practice** because:

1. **Unambiguous**: No confusion about what timezone a timestamp represents
2. **GitLab Integration**: GitLab API returns timestamps in UTC
3. **Metrics Accuracy**: Critical for accurate time-based metrics and aggregations
4. **Multi-timezone Support**: Essential for teams working across different timezones
5. **Industry Standard**: Common practice for audit logs and time-series data

### Testing

The fix has been applied globally and will automatically handle:
- All `CollectionRun` timestamp fields (`StartedAt`, `CompletedAt`, `WindowStart`, `WindowEnd`)
- All `IngestionState` timestamp fields
- All entity timestamp fields (`CreatedAt`, `UpdatedAt`, etc.)
- All raw data timestamp fields from GitLab API
- All metrics aggregate timestamp fields

No migration is needed as this is a code-level conversion that doesn't change the database schema.
