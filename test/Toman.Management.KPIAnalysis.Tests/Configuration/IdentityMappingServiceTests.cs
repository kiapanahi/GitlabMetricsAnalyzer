using Microsoft.Extensions.Options;
using Toman.Management.KPIAnalysis.ApiService.Configuration;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Services;

namespace Toman.Management.KPIAnalysis.Tests.Configuration;

public sealed class IdentityMappingServiceTests
{
    private readonly IdentityMappingService _service;

    public IdentityMappingServiceTests()
    {
        var configuration = new MetricsConfiguration
        {
            ProjectScope = null, // Optional
            Excludes = new ExclusionConfiguration(),
            Identity = new IdentityConfiguration
            {
                BotRegexPatterns = new List<string>
                {
                    "^.*bot$",
                    "^.*\\[bot\\]$",
                    "^gitlab-ci$",
                    "^dependabot.*",
                    "^renovate.*"
                }
            }
        };

        var options = Options.Create(configuration);
        _service = new IdentityMappingService(options);
    }

    [Theory]
    [InlineData("testbot", true)]
    [InlineData("deployment-bot", true)]
    [InlineData("dependabot", true)]
    [InlineData("renovate-bot", true)]
    [InlineData("gitlab-ci", true)]
    [InlineData("github-actions[bot]", true)]
    [InlineData("regular-user", false)]
    [InlineData("john.doe", false)]
    [InlineData("", false)]
    public void IsBot_DetectsBotUsers_Correctly(string username, bool expectedIsBot)
    {
        // Act
        var result = _service.IsBot(username);

        // Assert
        Assert.Equal(expectedIsBot, result);
    }

    [Theory]
    [InlineData("automated-bot", "bot@company.com", "Automation Bot", true)]
    [InlineData("user", "automated-service@company.com", "User", false)]
    [InlineData("user", "normal@company.com", "Bot User", false)]
    public void IsBot_ChecksEmailAndDisplayName(string username, string email, string displayName, bool expectedIsBot)
    {
        // Act
        var result = _service.IsBot(username, email, displayName);

        // Assert
        Assert.Equal(expectedIsBot, result);
    }
}