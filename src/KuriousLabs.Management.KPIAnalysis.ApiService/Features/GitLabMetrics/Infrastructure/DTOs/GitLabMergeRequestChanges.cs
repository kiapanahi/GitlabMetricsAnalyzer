using System.Text.Json.Serialization;

namespace KuriousLabs.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure.DTOs;

public sealed record GitLabMergeRequestChangesDto(
    [property: JsonPropertyName("changes")] List<GitLabChangeDto>? Changes
);

public sealed record GitLabChangeDto(
    [property: JsonPropertyName("old_path")] string? OldPath,
    [property: JsonPropertyName("new_path")] string? NewPath,
    [property: JsonPropertyName("new_file")] bool NewFile,
    [property: JsonPropertyName("renamed_file")] bool RenamedFile,
    [property: JsonPropertyName("deleted_file")] bool DeletedFile
);
