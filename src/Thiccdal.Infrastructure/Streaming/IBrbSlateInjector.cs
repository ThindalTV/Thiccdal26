namespace Thiccdal.Infrastructure.Streaming;

/// <summary>
/// Publishes a BRB slate to the currently armed fanout destinations when ingest disappears.
/// </summary>
public interface IBrbSlateInjector
{
    /// <summary>
    /// Gets a value indicating whether BRB injection is currently active.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Starts BRB injection for the provided destinations.
    /// </summary>
    Task Start(IReadOnlyList<RtmpRelayDestination> destinations, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops any active BRB injection sessions.
    /// </summary>
    Task Stop(CancellationToken cancellationToken = default);
}
