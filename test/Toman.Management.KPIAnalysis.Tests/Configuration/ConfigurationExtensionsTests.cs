using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Toman.Management.KPIAnalysis.ApiService.Configuration;

namespace Toman.Management.KPIAnalysis.Tests.Configuration;

public sealed class ConfigurationExtensionsTests
{
    [Fact]
    public void AddConfigJson_DoesNotThrow_WhenFileDoesNotExist()
    {
        // Arrange
        var builder = new ConfigurationBuilder();

        // Act & Assert
        var exception = Record.Exception(() => builder.AddConfigJson());
        Assert.Null(exception);
    }

    [Fact]
    public void AddConfigJson_LoadsConfiguration_WhenValidJsonExists()
    {
        // Arrange
        var content = @"{
  ""GitLab"": {
    ""BaseUrl"": ""https://gitlab.example.com"",
    ""Token"": ""test-token""
  }
}";

        // Create config.json in current directory
        var originalDir = Directory.GetCurrentDirectory();
        var testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(testDir);
        var configPath = Path.Combine(testDir, "config.json");

        try
        {
            File.WriteAllText(configPath, content);
            Directory.SetCurrentDirectory(testDir);

            // Act
            var builder = new ConfigurationBuilder().AddConfigJson();
            var config = builder.Build();

            // Assert
            Assert.Equal("https://gitlab.example.com", config["GitLab:BaseUrl"]);
            Assert.Equal("test-token", config["GitLab:Token"]);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
            if (Directory.Exists(testDir))
            {
                Directory.Delete(testDir, true);
            }
        }
    }
}