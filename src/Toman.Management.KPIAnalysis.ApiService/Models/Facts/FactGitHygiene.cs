namespace Toman.Management.KPIAnalysis.ApiService.Models.Facts;

public sealed class FactGitHygiene
{
    public required int ProjectId { get; init; }
    public DateOnly Day { get; init; }
    public int DirectPushesDefault { get; init; }
    public int ForcePushesProtected { get; init; }
    public int UnsignedCommitCount { get; init; }
}
