using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Thiccdal.Infrastructure.Streaming;

namespace Thiccdal.API.Restream;

/// <summary>
/// Maps operator-facing restream configuration and runtime endpoints.
/// </summary>
public static class RestreamApiExtensions
{
    /// <summary>
    /// Maps the restream endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapRestreamEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/api/restream");

        group.MapGet(
                string.Empty,
                static (IRestreamRuntimeService restreamRuntimeService, CancellationToken cancellationToken) =>
                    restreamRuntimeService.GetState(cancellationToken))
            .WithName("GetRestreamState")
            .Produces<RestreamControlState>(StatusCodes.Status200OK, "application/json");

        group.MapPut(
                "/configuration",
                static (
                    RestreamConfigurationUpdateRequest request,
                    IRestreamRuntimeService restreamRuntimeService,
                    CancellationToken cancellationToken) =>
                    restreamRuntimeService.UpdateConfiguration(request, cancellationToken))
            .WithName("UpdateRestreamConfiguration")
            .Produces<RestreamControlState>(StatusCodes.Status200OK, "application/json");

        group.MapPut(
                "/destinations/{platformName}",
                static (
                    string platformName,
                    RestreamDestinationUpdateRequest request,
                    IRestreamRuntimeService restreamRuntimeService,
                    CancellationToken cancellationToken) =>
                {
                    RestreamDestinationUpdateRequest normalizedRequest = request with
                    {
                        PlatformName = platformName
                    };

                    return restreamRuntimeService.UpdateDestination(normalizedRequest, cancellationToken);
                })
            .WithName("UpdateRestreamDestination")
            .Produces<RestreamControlState>(StatusCodes.Status200OK, "application/json");

        group.MapPost(
                "/start",
                static (IRestreamRuntimeService restreamRuntimeService, CancellationToken cancellationToken) =>
                    restreamRuntimeService.Start(cancellationToken))
            .WithName("StartRestream")
            .Produces<RestreamControlState>(StatusCodes.Status200OK, "application/json");

        group.MapPost(
                "/stop",
                static (IRestreamRuntimeService restreamRuntimeService, CancellationToken cancellationToken) =>
                    restreamRuntimeService.Stop(cancellationToken))
            .WithName("StopRestream")
            .Produces<RestreamControlState>(StatusCodes.Status200OK, "application/json");

        return endpoints;
    }
}
