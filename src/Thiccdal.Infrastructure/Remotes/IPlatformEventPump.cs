namespace Thiccdal.Infrastructure.Remotes;

/// <summary>
/// Bridges platform event sources into the shared event bus.
/// </summary>
public interface IPlatformEventPump
{
    /// <summary>
    /// Runs the event pump for the supplied platform connection until cancellation is requested.
    /// </summary>
    /// <param name="connection">The connection whose events should be forwarded.</param>
    /// <param name="cancellationToken">The cancellation token that stops the pump.</param>
    /// <returns>A task that completes when the pump stops.</returns>
    Task Run(IPlatformConnection connection, CancellationToken cancellationToken = default);
}
