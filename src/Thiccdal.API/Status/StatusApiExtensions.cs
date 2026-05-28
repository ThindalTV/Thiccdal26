using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Thiccdal.API.Status;

/// <summary>
/// Registers the public status API services and endpoints.
/// </summary>
public static class StatusApiExtensions
{
    /// <summary>
    /// The CORS policy used by the public status endpoints.
    /// </summary>
    public const string StatusCorsPolicyName = "StatusApi";

    /// <summary>
    /// Registers the services required by the public status endpoints.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddStatusApi(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddCors(
            static options =>
            {
                options.AddPolicy(
                    StatusCorsPolicyName,
                    static policy =>
                    {
                        policy.AllowAnyOrigin()
                            .AllowAnyHeader()
                            .AllowAnyMethod();
                    });
            });

        services.AddSingleton<IStreamStatusService, StreamStatusService>();
        return services;
    }

    /// <summary>
    /// Maps the public status endpoints.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The updated endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapStatusEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/status")
            .RequireCors(StatusCorsPolicyName);

        group.MapGet(
                string.Empty,
                static async (IStreamStatusService streamStatusService, CancellationToken cancellationToken) =>
                {
                    StreamStatusResponse response = await streamStatusService.GetStatus(cancellationToken);
                    return Results.Ok(response);
                })
            .WithName("GetStreamStatus")
            .Produces<StreamStatusResponse>(StatusCodes.Status200OK, "application/json");

        group.MapGet(
                "/badge.svg",
                static async (
                    HttpContext httpContext,
                    IWebHostEnvironment environment,
                    IStreamStatusService streamStatusService,
                    CancellationToken cancellationToken) =>
                {
                    StreamStatusResponse response = await streamStatusService.GetStatus(cancellationToken);
                    string fileName = string.Equals(response.State, StreamStatusStates.Online, StringComparison.Ordinal)
                        ? "badge-online.svg"
                        : "badge-offline.svg";

                    httpContext.Response.Headers.CacheControl = "no-cache, no-store";
                    httpContext.Response.Headers.Pragma = "no-cache";

                    string path = Path.Combine(environment.WebRootPath, fileName);
                    return Results.File(path, "image/svg+xml");
                })
            .WithName("GetStreamStatusBadge")
            .Produces(StatusCodes.Status200OK, contentType: "image/svg+xml");

        return endpoints;
    }
}
