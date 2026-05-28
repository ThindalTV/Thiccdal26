using Thiccdal.Infrastructure.Bot.Models;

namespace Thiccdal.Infrastructure.Twitch;

/// <summary>
/// Manages the Twitch EventSub WebSocket lifecycle and emits normalized events from inbound notifications.
/// </summary>
public interface ITwitchEventSubClient : IAsyncDisposable
{
    /// <summary>
    /// Gets a value indicating whether the EventSub transport is currently connected.
    /// </summary>
    bool Connected { get; }

    /// <summary>
    /// Connects to Twitch EventSub for the specified chat connection profile.
    /// </summary>
    /// <param name="profile">The resolved bot and broadcaster profile.</param>
    /// <param name="cancellationToken">Cancels the connection workflow.</param>
    Task Connect(TwitchChatConnectionProfile profile, CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects the EventSub session if one is active.
    /// </summary>
    /// <param name="cancellationToken">Cancels the disconnect workflow.</param>
    Task Disconnect(CancellationToken cancellationToken = default);

    /// <summary>
    /// Raised when a normalized platform event is received from EventSub.
    /// </summary>
    event EventHandler<PlatformEvent>? OnEventReceived;

    /// <summary>
    /// Raised when a normalized chat event is received from EventSub.
    /// </summary>
    event EventHandler<ChatEvent>? ChatMessageReceived;

    /// <summary>
    /// Raised when a non-chat platform event is received from EventSub.
    /// </summary>
    event EventHandler<PlatformEvent>? PlatformEventReceived;

    /// <summary>
    /// Raised when the EventSub transport disconnects.
    /// </summary>
    event EventHandler? Disconnected;

    /// <summary>
    /// Raised when the EventSub transport faults unexpectedly.
    /// </summary>
    event EventHandler<Exception>? Faulted;
}
