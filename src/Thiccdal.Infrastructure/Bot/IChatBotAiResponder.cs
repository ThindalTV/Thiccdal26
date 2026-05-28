using Thiccdal.Infrastructure.Bot.Models;

namespace Thiccdal.Infrastructure.Bot;

/// <summary>
/// Produces mention-triggered AI replies for chat messages when no explicit command handled the message.
/// </summary>
public interface IChatBotAiResponder
{
    /// <summary>
    /// Attempts to generate a short reply for a chat message.
    /// </summary>
    /// <param name="chatEvent">The normalized chat message to inspect.</param>
    /// <param name="cancellationToken">Cancels reply generation.</param>
    /// <returns>The reply to send, or <see langword="null"/> when no reply should be sent.</returns>
    Task<string?> TryRespond(ChatEvent chatEvent, CancellationToken cancellationToken = default);
}
