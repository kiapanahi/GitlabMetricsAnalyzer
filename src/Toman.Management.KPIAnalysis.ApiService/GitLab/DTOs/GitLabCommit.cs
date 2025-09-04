using System.Text.Json.Serialization;

namespace Toman.Management.KPIAnalysis.ApiService.GitLab.DTOs;

public sealed record GitLabCommit(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("author_name")] string AuthorName,
    [property: JsonPropertyName("author_email")] string AuthorEmail,
    [property: JsonPropertyName("committed_date")] DateTimeOffset CommittedDate,
    [property: JsonPropertyName("stats")] GitLabCommitStats? Stats
);

public sealed record GitLabCommitStats(
    [property: JsonPropertyName("additions")] int Additions,
    [property: JsonPropertyName("deletions")] int Deletions
);
