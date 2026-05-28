namespace Thiccdal.Infrastructure.Streaming;

/// <summary>
/// Represents a single external relay process owned by the streaming data plane.
/// </summary>
public interface IStreamingRelaySession : IAsyncDisposable
{
    /// <summary>
    /// Gets the destination platform name.
    /// </summary>
    string PlatformName { get; }

    /// <summary>
    /// Stops the relay process.
    /// </summary>
    Task Stop(CancellationToken cancellationToken = default);
}
