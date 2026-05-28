using Thiccdal.Infrastructure.Bot.Models;

namespace Thiccdal.Infrastructure.Remotes;

/// <summary>
/// Represents a source of chat messages from a remote platform.
/// </summary>
public interface IChatSource : IPlatformEventSource
{
    /// <summary>
    /// Gets a value indicating whether the chat source is currently connected.
    /// </summary>
    bool Connected { get; }

    /// <summary>
    /// Establishes a connection to the chat source.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public Task Connect(CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes the connection to the chat source.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public Task Disconnect(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a message to the chat source.
    /// </summary>
    /// <param name="message">The message content to send.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public Task SendMessage(string message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a message to a specific platform channel or target when the adapter supports targeted routing.
    /// </summary>
    /// <param name="message">The message content to send.</param>
    /// <param name="channelId">The platform-specific channel identifier. <see langword="null"/> uses the primary configured channel.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public Task SendMessage(string message, string? channelId, CancellationToken cancellationToken = default)
    {
        return SendMessage(message, cancellationToken);
    }

    /// <summary>
    /// Raised when a chat message is received from the platform.
    /// </summary>
    public event EventHandler<ChatEvent>? OnChatMessageRecieved;
}
