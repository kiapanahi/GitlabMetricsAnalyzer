namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models.Entities;

/// <summary>
/// Track multiple emails/usernames for the same developer
/// </summary>
public sealed class DeveloperAlias
{
    public long Id { get; init; }
    public long DeveloperId { get; init; }
    public required string AliasType { get; init; } // 'email', 'username'
    public required string AliasValue { get; init; }
    public DateTimeOffset? VerifiedAt { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public Developer Developer { get; init; } = null!;
}