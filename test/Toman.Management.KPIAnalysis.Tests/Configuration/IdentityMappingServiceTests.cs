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
            ProjectScope = new ProjectScopeConfiguration(),
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
                },
                IdentityOverrides = new Dictionary<string, CanonicalDeveloperConfiguration>
                {
                    ["john.doe"] = new CanonicalDeveloperConfiguration
                    {
                        DisplayName = "John Doe",
                        PrimaryEmail = "john.doe@company.com",
                        PrimaryUsername = "john.doe",
                        AliasEmails = new List<string> { "j.doe@company.com", "johndoe@oldcompany.com" },
                        AliasUsernames = new List<string> { "jdoe", "john_doe" }
                    }
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

    [Theory]
    [InlineData("john.doe@company.com")]
    [InlineData("j.doe@company.com")]
    [InlineData("johndoe@oldcompany.com")]
    [InlineData("jdoe")]
    [InlineData("john_doe")]
    [InlineData("john.doe")]
    public void GetCanonicalDeveloper_ReturnsCorrectMapping_ForAllAliases(string emailOrUsername)
    {
        // Act
        var result = _service.GetCanonicalDeveloper(emailOrUsername);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("John Doe", result.DisplayName);
        Assert.Equal("john.doe@company.com", result.PrimaryEmail);
        Assert.Equal("john.doe", result.PrimaryUsername);
    }

    [Theory]
    [InlineData("unknown@company.com")]
    [InlineData("random-user")]
    [InlineData("")]
    [InlineData(null)]
    public void GetCanonicalDeveloper_ReturnsNull_ForUnknownIdentities(string? emailOrUsername)
    {
        // Act
        var result = _service.GetCanonicalDeveloper(emailOrUsername!);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ConsolidateAliases_ReturnsAllAliases_WhenCanonicalDeveloperExists()
    {
        // Act
        var result = _service.ConsolidateAliases("j.doe@company.com", "jdoe");

        // Assert
        Assert.NotEmpty(result);
        var emailAliases = result.Where(a => a.AliasType == "email").ToList();
        var usernameAliases = result.Where(a => a.AliasType == "username").ToList();
        
        // Should contain all configured alias emails plus the input email (if different from primary)
        Assert.Contains(emailAliases, a => a.AliasValue == "j.doe@company.com");
        Assert.Contains(emailAliases, a => a.AliasValue == "johndoe@oldcompany.com");
        
        // Should contain all configured alias usernames plus the input username (if different from primary)  
        Assert.Contains(usernameAliases, a => a.AliasValue == "jdoe");
        Assert.Contains(usernameAliases, a => a.AliasValue == "john_doe");
    }

    [Fact]
    public void ConsolidateAliases_ReturnsEmpty_WhenNoCanonicalDeveloperExists()
    {
        // Act
        var result = _service.ConsolidateAliases("unknown@company.com", "unknown-user");

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ConsolidateAliases_DoesNotDuplicatePrimaryIdentities()
    {
        // Act - using primary email and username
        var result = _service.ConsolidateAliases("john.doe@company.com", "john.doe");

        // Assert
        Assert.NotEmpty(result);
        
        // Primary email should not appear in aliases
        Assert.DoesNotContain(result, a => a.AliasValue == "john.doe@company.com");
        
        // Primary username should not appear in aliases  
        Assert.DoesNotContain(result, a => a.AliasValue == "john.doe");
        
        // But other aliases should still be there
        Assert.Contains(result, a => a.AliasValue == "j.doe@company.com");
        Assert.Contains(result, a => a.AliasValue == "jdoe");
    }
}