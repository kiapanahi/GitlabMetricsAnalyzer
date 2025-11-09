using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using KuriousLabs.Management.KPIAnalysis.ApiService.Features.CrossSystemMetrics.Configuration;

namespace KuriousLabs.Management.KPIAnalysis.ApiService.Features.CrossSystemMetrics.Infrastructure;

/// <summary>
/// Service for parsing Jira issue keys from text (commit messages, MR descriptions)
/// </summary>
public interface IJiraIssueKeyParser
{
    /// <summary>
    /// Extract all Jira issue keys from text
    /// </summary>
    /// <param name="text">Text to parse (commit message, MR description, etc.)</param>
    /// <returns>List of unique issue keys found</returns>
    IReadOnlyList<string> ExtractIssueKeys(string text);
}

/// <summary>
/// Implementation of Jira issue key parser
/// </summary>
public sealed class JiraIssueKeyParser : IJiraIssueKeyParser
{
    private readonly Regex _issueKeyRegex;

    public JiraIssueKeyParser(IOptions<CrossSystemConfiguration> configuration)
    {
        var config = configuration.Value;
        // Build regex: {PROJECT_KEY_PATTERN}-\d+
        // Example: PROJECT-123, MAIN-456, ABC-7890
        var pattern = $@"\b({config.JiraProjectKeyPattern})-(\d+)\b";
        _issueKeyRegex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> ExtractIssueKeys(string text)
    {
        using var activity = Diagnostics.ActivitySource.StartActivity("ExtractIssueKeys");
        
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        var matches = _issueKeyRegex.Matches(text);
        var issueKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in matches)
        {
            if (match.Success)
            {
                // Normalize to uppercase
                issueKeys.Add(match.Value.ToUpperInvariant());
            }
        }

        activity?.SetTag("issue_keys_found", issueKeys.Count);
        return issueKeys.ToList();
    }
}
