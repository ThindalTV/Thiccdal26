using Thiccdal.Infrastructure.Bot;
using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Infrastructure.Remotes;

namespace Thiccdal.Modules.ChatBot.Services;

public class ChatServiceAggregator : IChatService, IDisposable
{
    public event EventHandler<ChatEvent>? OnChatMessageRecieved;

    private readonly List<IChatSource> _chatSources;

    private bool _connected = false;

    public ChatServiceAggregator(IEnumerable<IChatSource> chatSources)
    {
        _chatSources = chatSources.ToList();

        foreach (var source in _chatSources)
        {
            source.OnChatMessageRecieved += MessageRecieved;
        }
    }

    public async Task Connect(CancellationToken ct)
    {
        if (_connected)
        {
            return;
        }
        _connected = true;

        foreach (var source in _chatSources.Where(cs => !cs.Connected))
        {
            await source.Connect(ct);
        }
    }

    public async Task Disconnect(CancellationToken ct)
    {
        foreach (var source in _chatSources.Where(cs => cs.Connected))
        {
            await source.Disconnect(ct);
        }
    }

    public async Task SendMessage(string message, CancellationToken cancellationToken)
    {
        foreach (var source in _chatSources.Where(cs => cs.Connected))
        {
            await source.SendMessage(message, cancellationToken);
        }
    }

    public void Dispose()
    {
        foreach (var source in _chatSources)
        {
            source.OnChatMessageRecieved -= MessageRecieved;
        }
    }

    protected void MessageRecieved(object? sender, ChatEvent msg)
    {
        OnChatMessageRecieved?.Invoke(this, msg);
    }
}
