using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Configuration;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Infrastructure;

namespace Toman.Management.KPIAnalysis.Tests.Integration;

/// <summary>
/// Integration tests for GitLabHttpClient to validate API connectivity and resilience.
/// These tests use mock HTTP responses to avoid requiring actual GitLab API access.
/// </summary>
public sealed class GitLabHttpClientIntegrationTests
{
    [Fact]
    public async Task TestConnectionAsync_WithValidConfiguration_ReturnsTrue()
    {
        // Arrange
        using var httpClient = new HttpClient(new MockHttpMessageHandler(
            "/api/v4/version", 
            """{"version":"16.0.0","revision":"abc123"}"""
        ))
        {
            BaseAddress = new Uri("https://gitlab.example.com/api/v4/")
        };
        
        var logger = Mock.Of<ILogger<GitLabHttpClient>>();
        var gitLabClient = new GitLabHttpClient(httpClient, logger);

        // Act
        var result = await gitLabClient.TestConnectionAsync();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task TestConnectionAsync_WithInvalidConfiguration_ReturnsFalse()
    {
        // Arrange
        using var httpClient = new HttpClient(new MockHttpMessageHandler(
            "/api/v4/version", 
            string.Empty,
            System.Net.HttpStatusCode.Unauthorized
        ))
        {
            BaseAddress = new Uri("https://gitlab.example.com/api/v4/")
        };
        
        var logger = Mock.Of<ILogger<GitLabHttpClient>>();
        var gitLabClient = new GitLabHttpClient(httpClient, logger);

        // Act
        var result = await gitLabClient.TestConnectionAsync();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetUsersAsync_WithValidResponse_ReturnsUsers()
    {
        // Arrange
        var usersJson = """
            [
                {
                    "id": 1,
                    "username": "testuser",
                    "name": "Test User",
                    "state": "active",
                    "email": "test@example.com",
                    "bot": false
                }
            ]
            """;

        using var httpClient = new HttpClient(new MockHttpMessageHandler(
            "/api/v4/users", 
            usersJson
        ))
        {
            BaseAddress = new Uri("https://gitlab.example.com/api/v4/")
        };
        
        var logger = Mock.Of<ILogger<GitLabHttpClient>>();
        var gitLabClient = new GitLabHttpClient(httpClient, logger);

        // Act
        var users = await gitLabClient.GetUsersAsync();

        // Assert
        Assert.NotNull(users);
        Assert.Single(users);
        Assert.Equal("testuser", users[0].Username);
        Assert.Equal("Test User", users[0].Name);
        Assert.Equal("active", users[0].State);
    }

    [Fact]
    public async Task GetProjectsAsync_WithValidResponse_ReturnsProjects()
    {
        // Arrange
        var projectsJson = """
            [
                {
                    "id": 1,
                    "path_with_namespace": "group/project",
                    "default_branch": "main",
                    "visibility": "private",
                    "last_activity_at": "2024-01-01T10:00:00Z"
                }
            ]
            """;

        using var httpClient = new HttpClient(new MockHttpMessageHandler(
            "/api/v4/projects", 
            projectsJson
        ))
        {
            BaseAddress = new Uri("https://gitlab.example.com/api/v4/")
        };
        
        var logger = Mock.Of<ILogger<GitLabHttpClient>>();
        var gitLabClient = new GitLabHttpClient(httpClient, logger);

        // Act
        var projects = await gitLabClient.GetProjectsAsync();

        // Assert
        Assert.NotNull(projects);
        Assert.Single(projects);
        Assert.Equal("group/project", projects[0].NameWithNamespace);
        Assert.Equal("main", projects[0].DefaultBranch);
        Assert.Equal("private", projects[0].Visibility);
    }

    [Fact]
    public async Task GetUserByIdAsync_WithNotFound_ReturnsNull()
    {
        // Arrange
        using var httpClient = new HttpClient(new MockHttpMessageHandler(
            "/api/v4/users/999", 
            string.Empty,
            System.Net.HttpStatusCode.NotFound
        ))
        {
            BaseAddress = new Uri("https://gitlab.example.com/api/v4/")
        };
        
        var logger = Mock.Of<ILogger<GitLabHttpClient>>();
        var gitLabClient = new GitLabHttpClient(httpClient, logger);

        // Act
        var user = await gitLabClient.GetUserByIdAsync(999);

        // Assert
        Assert.Null(user);
    }

    [Fact]
    public async Task GetCommitsAsync_WithValidResponse_ReturnsCommits()
    {
        // Arrange
        var commitsJson = """
            [
                {
                    "id": "abc123def456",
                    "author_name": "Test Author",
                    "author_email": "author@example.com",
                    "committed_date": "2024-01-01T12:00:00Z",
                    "stats": {
                        "additions": 10,
                        "deletions": 5
                    }
                }
            ]
            """;

        using var httpClient = new HttpClient(new MockHttpMessageHandler(
            "/api/v4/projects/1/repository/commits", 
            commitsJson
        ))
        {
            BaseAddress = new Uri("https://gitlab.example.com/api/v4/")
        };
        
        var logger = Mock.Of<ILogger<GitLabHttpClient>>();
        var gitLabClient = new GitLabHttpClient(httpClient, logger);

        // Act
        var commits = await gitLabClient.GetCommitsAsync(1);

        // Assert
        Assert.NotNull(commits);
        Assert.Single(commits);
        Assert.Equal("abc123def456", commits[0].Id);
        Assert.Equal("Test Author", commits[0].AuthorName);
        Assert.Equal("author@example.com", commits[0].AuthorEmail);
        Assert.NotNull(commits[0].Stats);
        Assert.Equal(10, commits[0].Stats.Additions);
        Assert.Equal(5, commits[0].Stats.Deletions);
        Assert.Equal(15, commits[0].Stats.Total);
    }

    /// <summary>
    /// Mock HTTP message handler for testing HTTP client interactions
    /// </summary>
    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _expectedPath;
        private readonly string _responseContent;
        private readonly System.Net.HttpStatusCode _statusCode;

        public MockHttpMessageHandler(string expectedPath, string responseContent, System.Net.HttpStatusCode statusCode = System.Net.HttpStatusCode.OK)
        {
            _expectedPath = expectedPath;
            _responseContent = responseContent;
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode);
            
            // Add rate limit headers to simulate GitLab API
            response.Headers.Add("RateLimit-Limit", "2000");
            response.Headers.Add("RateLimit-Remaining", "1999");
            response.Headers.Add("RateLimit-Reset", DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds().ToString());
            
            if (_statusCode == System.Net.HttpStatusCode.OK)
            {
                response.Content = new StringContent(_responseContent, System.Text.Encoding.UTF8, "application/json");
            }

            return Task.FromResult(response);
        }
    }
}