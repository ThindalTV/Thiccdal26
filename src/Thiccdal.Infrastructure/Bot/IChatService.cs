using Thiccdal.Infrastructure.Bot.Models;

namespace Thiccdal.Infrastructure.Bot;

public interface IChatService
{
    event EventHandler<ChatEvent>? OnChatMessageRecieved;

    Task Connect(CancellationToken ct);
    Task Disconnect(CancellationToken ct);
}
