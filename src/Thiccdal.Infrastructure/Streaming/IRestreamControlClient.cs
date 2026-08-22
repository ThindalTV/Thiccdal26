namespace Thiccdal.Infrastructure.Streaming;

/// <summary>
/// Calls the restream control API used by operator-facing Blazor components.
/// </summary>
public interface IRestreamControlClient
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
    /// Pushes the currently persisted control-plane configuration to the restream runtime host.
    /// </summary>
    Task<RestreamControlState> PushConfiguration(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates whether a platform should participate in RTMP fanout.
    /// </summary>
    Task<RestreamControlState> UpdateDestination(RestreamDestinationUpdateRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts the restream ingest and fanout runtime.
    /// </summary>
    Task<RestreamControlState> Start(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the restream ingest and fanout runtime.
    /// </summary>
    Task<RestreamControlState> Stop(CancellationToken cancellationToken = default);
}
