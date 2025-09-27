using System.Text.Json;

using Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Models;

namespace Toman.Management.KPIAnalysis.ApiService.Features.GitLabMetrics.Middleware;

/// <summary>
/// Middleware to enforce API versioning for v1 endpoints
/// </summary>
public class ApiVersionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiVersionMiddleware> _logger;

    public ApiVersionMiddleware(RequestDelegate next, ILogger<ApiVersionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Check if this is a v1 API endpoint
        if (context.Request.Path.StartsWithSegments("/api/v1"))
        {
            var apiVersion = GetApiVersionFromHeaders(context.Request);
            
            if (string.IsNullOrEmpty(apiVersion))
            {
                await WriteVersionRequiredError(context);
                return;
            }

            if (!IsValidApiVersion(apiVersion))
            {
                await WriteInvalidVersionError(context, apiVersion);
                return;
            }

            // Add version to context for use by endpoints
            context.Items["ApiVersion"] = apiVersion;
        }

        await _next(context);
    }

    private static string? GetApiVersionFromHeaders(HttpRequest request)
    {
        // Check X-Api-Version header first
        if (request.Headers.TryGetValue("X-Api-Version", out var apiVersionHeader))
        {
            return apiVersionHeader.ToString();
        }

        // Check Accept header for versioning
        if (request.Headers.TryGetValue("Accept", out var acceptHeader))
        {
            var acceptValue = acceptHeader.ToString();
            if (acceptValue.Contains("application/json;version="))
            {
                var versionStart = acceptValue.IndexOf("version=") + 8;
                var versionEnd = acceptValue.IndexOf(',', versionStart);
                if (versionEnd == -1) versionEnd = acceptValue.Length;
                return acceptValue.Substring(versionStart, versionEnd - versionStart).Trim();
            }
        }

        return null;
    }

    private static bool IsValidApiVersion(string version)
    {
        return SchemaVersion.IsSupported(version);
    }

    private async Task WriteVersionRequiredError(HttpContext context)
    {
        _logger.LogWarning("API v1 request without version header from {RemoteIpAddress}", 
            context.Connection.RemoteIpAddress);

        context.Response.StatusCode = 400;
        context.Response.ContentType = "application/json";

        var error = new
        {
            error = "API_VERSION_REQUIRED",
            message = "API version must be specified for v1 endpoints",
            details = "Use X-Api-Version header or Accept header with version parameter",
            supportedVersions = SchemaVersion.SupportedVersions,
            examples = new
            {
                header = "X-Api-Version: 1.0.0",
                accept = "Accept: application/json;version=1.0.0"
            }
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(error, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));
    }

    private async Task WriteInvalidVersionError(HttpContext context, string version)
    {
        _logger.LogWarning("API v1 request with invalid version {Version} from {RemoteIpAddress}", 
            version, context.Connection.RemoteIpAddress);

        context.Response.StatusCode = 400;
        context.Response.ContentType = "application/json";

        var error = new
        {
            error = "INVALID_API_VERSION",
            message = $"API version '{version}' is not supported",
            supportedVersions = SchemaVersion.SupportedVersions,
            requestedVersion = version
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(error, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));
    }
}

/// <summary>
/// Extensions for adding API version middleware
/// </summary>
public static class ApiVersionMiddlewareExtensions
{
    public static IApplicationBuilder UseApiVersioning(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ApiVersionMiddleware>();
    }
}