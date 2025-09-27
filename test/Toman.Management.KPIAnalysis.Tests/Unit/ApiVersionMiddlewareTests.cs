using System.Text.Json;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Middleware;
using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models;

namespace Toman.Management.KPIAnalysis.Tests.Unit;

/// <summary>
/// Unit tests for API versioning middleware
/// </summary>
public sealed class ApiVersionMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_NonV1Path_CallsNextMiddleware()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/users";
        
        var nextCalled = false;
        var next = new RequestDelegate(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var logger = NullLogger<ApiVersionMiddleware>.Instance;
        var middleware = new ApiVersionMiddleware(next, logger);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(nextCalled);
        Assert.Equal(200, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_V1PathWithoutVersionHeader_Returns400()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/metrics/developers";
        context.Response.Body = new MemoryStream();
        
        var next = new RequestDelegate(_ => Task.CompletedTask);
        var logger = NullLogger<ApiVersionMiddleware>.Instance;
        var middleware = new ApiVersionMiddleware(next, logger);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(400, context.Response.StatusCode);
        Assert.Equal("application/json", context.Response.ContentType);
        
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseContent = await new StreamReader(context.Response.Body).ReadToEndAsync();
        var jsonDoc = JsonDocument.Parse(responseContent);
        
        Assert.Equal("API_VERSION_REQUIRED", jsonDoc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task InvokeAsync_V1PathWithInvalidVersionHeader_Returns400()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/metrics/developers";
        context.Request.Headers.Append("X-Api-Version", "999.0.0");
        context.Response.Body = new MemoryStream();
        
        var next = new RequestDelegate(_ => Task.CompletedTask);
        var logger = NullLogger<ApiVersionMiddleware>.Instance;
        var middleware = new ApiVersionMiddleware(next, logger);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(400, context.Response.StatusCode);
        Assert.Equal("application/json", context.Response.ContentType);
        
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseContent = await new StreamReader(context.Response.Body).ReadToEndAsync();
        var jsonDoc = JsonDocument.Parse(responseContent);
        
        Assert.Equal("INVALID_API_VERSION", jsonDoc.RootElement.GetProperty("error").GetString());
        Assert.Contains("999.0.0", jsonDoc.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task InvokeAsync_V1PathWithValidVersionHeader_CallsNext()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/metrics/developers";
        context.Request.Headers.Append("X-Api-Version", SchemaVersion.Current);
        
        var nextCalled = false;
        var next = new RequestDelegate(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var logger = NullLogger<ApiVersionMiddleware>.Instance;
        var middleware = new ApiVersionMiddleware(next, logger);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(nextCalled);
        Assert.Equal(SchemaVersion.Current, context.Items["ApiVersion"]?.ToString());
    }

    [Fact]
    public async Task InvokeAsync_V1PathWithAcceptHeaderVersion_CallsNext()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/catalog";
        context.Request.Headers.Append("Accept", "application/json;version=1.0.0");
        
        var nextCalled = false;
        var next = new RequestDelegate(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var logger = NullLogger<ApiVersionMiddleware>.Instance;
        var middleware = new ApiVersionMiddleware(next, logger);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(nextCalled);
        Assert.Equal("1.0.0", context.Items["ApiVersion"]?.ToString());
    }

    [Fact]
    public async Task InvokeAsync_V1PathWithBothHeaders_PrefersXApiVersion()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/catalog";
        context.Request.Headers.Append("X-Api-Version", SchemaVersion.Current);
        context.Request.Headers.Append("Accept", "application/json;version=0.5.0");
        
        var nextCalled = false;
        var next = new RequestDelegate(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var logger = NullLogger<ApiVersionMiddleware>.Instance;
        var middleware = new ApiVersionMiddleware(next, logger);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(nextCalled);
        Assert.Equal(SchemaVersion.Current, context.Items["ApiVersion"]?.ToString());
    }
}