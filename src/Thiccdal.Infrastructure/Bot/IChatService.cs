using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Infrastructure.Remotes;

namespace Thiccdal.Infrastructure.Bot;

public interface IChatService : IPlatformEventSource
{
    event EventHandler<ChatEvent>? OnChatMessageRecieved;
    Task SendMessage(string message, CancellationToken cancellationToken = default);
    Task Connect(CancellationToken ct);
    Task Disconnect(CancellationToken ct);
}
