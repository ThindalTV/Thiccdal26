namespace Thiccdal.Infrastructure.Streaming;

/// <summary>
/// Persists operator-selected restream destinations and coordinates the current runtime seam.
/// </summary>
public interface IRestreamRuntimeService
{
    /// <summary>
    /// Returns the current restream runtime and destination state.
    /// </summary>
    Task<RestreamControlState> GetState(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the ingest and recording configuration used by the restream runtime.
    /// </summary>
    Task<RestreamControlState> UpdateConfiguration(
        RestreamConfigurationUpdateRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates whether a platform should participate in RTMP fanout.
    /// </summary>
    Task<RestreamControlState> UpdateDestination(RestreamDestinationUpdateRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts the current ingest and fanout seam if at least one connected destination is enabled.
    /// </summary>
    Task<RestreamControlState> Start(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the current ingest and fanout seam.
    /// </summary>
    Task<RestreamControlState> Stop(CancellationToken cancellationToken = default);
}
