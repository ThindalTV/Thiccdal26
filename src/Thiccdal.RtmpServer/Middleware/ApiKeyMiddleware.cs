using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.Streaming;

namespace Thiccdal.RtmpServer.Middleware;

/// <summary>
/// Rejects requests that do not carry the correct shared API key in the X-Api-Key header.
/// Health check and SignalR hub paths are exempted so they can be reached without authentication.
/// </summary>
public sealed class ApiKeyMiddleware
{
    private const string ApiKeyHeader = "X-Api-Key";
    private static readonly string[] ExemptPaths = ["/healthz", "/hubs/events"];

    private readonly RequestDelegate _next;
    private readonly IOptions<RtmpServerOptions> _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiKeyMiddleware"/> class.
    /// </summary>
    public ApiKeyMiddleware(RequestDelegate next, IOptions<RtmpServerOptions> options)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(options);

        _next = next;
        _options = options;
    }

    /// <summary>
    /// Invokes the middleware.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        string path = context.Request.Path.Value ?? string.Empty;

        foreach (string exemptPath in ExemptPaths)
        {
            if (path.StartsWith(exemptPath, StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }
        }

        string configuredKey = _options.Value.ApiKey;
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("API key is not configured on this server.");
            return;
        }

        if (!context.Request.Headers.TryGetValue(ApiKeyHeader, out Microsoft.Extensions.Primitives.StringValues headerValues) ||
            !string.Equals(headerValues.ToString(), configuredKey, StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Invalid or missing API key.");
            return;
        }

        await _next(context);
    }
}
