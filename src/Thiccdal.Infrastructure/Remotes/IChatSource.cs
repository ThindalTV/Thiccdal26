using Thiccdal.Infrastructure.Bot.Models;

namespace Thiccdal.Infrastructure.Remotes;

/// <summary>
/// Represents a source of chat messages from a remote platform.
/// </summary>
public interface IChatSource
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
    /// Raised when a chat message is received from the platform.
    /// </summary>
    public event EventHandler<ChatEvent>? OnChatMessageRecieved;
}
