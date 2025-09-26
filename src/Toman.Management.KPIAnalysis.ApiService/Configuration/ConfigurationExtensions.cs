using System.Text.Json;
using System.Text.RegularExpressions;

namespace Toman.Management.KPIAnalysis.ApiService.Configuration;

/// <summary>
/// Extension methods for configuration loading and environment variable expansion
/// </summary>
public static class ConfigurationExtensions
{
    private static readonly Regex EnvironmentVariableRegex = new(@"\$\{([A-Z_][A-Z0-9_]*)\}", RegexOptions.Compiled);

    /// <summary>
    /// Adds config.json with environment variable substitution support
    /// </summary>
    /// <param name="builder">Configuration builder</param>
    /// <returns>Configuration builder for chaining</returns>
    public static IConfigurationBuilder AddConfigJson(this IConfigurationBuilder builder)
    {
        const string configFileName = "config.json";
        
        if (!File.Exists(configFileName))
        {
            return builder;
        }

        try
        {
            var configContent = File.ReadAllText(configFileName);
            var expandedContent = ExpandEnvironmentVariables(configContent);
            
            var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(expandedContent));
            return builder.AddJsonStream(stream);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to load {configFileName}: {ex.Message}");
            return builder;
        }
    }

    /// <summary>
    /// Expands environment variables in the format ${VAR_NAME} within configuration content
    /// </summary>
    /// <param name="content">Configuration content with potential environment variable references</param>
    /// <returns>Content with environment variables expanded</returns>
    private static string ExpandEnvironmentVariables(string content)
    {
        return EnvironmentVariableRegex.Replace(content, match =>
        {
            var variableName = match.Groups[1].Value;
            var value = Environment.GetEnvironmentVariable(variableName);
            
            if (value is null)
            {
                Console.WriteLine($"Warning: Environment variable {variableName} not found");
                return match.Value; // Keep original if not found
            }
            
            return value;
        });
    }
}