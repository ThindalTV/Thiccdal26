using Thiccdal.Infrastructure.Bot.Models;

namespace Thiccdal.Infrastructure.Remotes;

/// <summary>
/// Represents a source of chat messages from a remote platform.
/// </summary>
public interface IChatSource
{
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
    /// Raised when a chat message is received from the platform.
    /// </summary>
    public event EventHandler<ChatMessage>? OnChatMessageRecieved;
}
