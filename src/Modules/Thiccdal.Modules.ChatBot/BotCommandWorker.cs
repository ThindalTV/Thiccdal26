using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Thiccdal.Infrastructure.Bot;
using Thiccdal.Infrastructure.Bot.Models;

namespace Thiccdal.Modules.ChatBot;

public class BotCommandWorker : BackgroundService
{
    private readonly Lazy<IChatService> _chatServiceLazy;
    private IChatService? _chatService;
    private readonly ILogger<IHostedService> _logger;

    public BotCommandWorker(Lazy<IChatService> chatServiceLazy, ILogger<IHostedService> logger)
    {
        _chatServiceLazy = chatServiceLazy;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _chatService = _chatServiceLazy.Value;

        _chatService.OnChatMessageRecieved += ChatService_OnChatMessageRecieved;

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            await _chatService.Connect(stoppingToken);
        }
    }

    protected void ChatService_OnChatMessageRecieved(object? sender, ChatEvent msg)
    {
        if(_chatService == null)
        {
            _logger.LogError("Chat service is not initialized.");
            return;
        }

        _chatService.SendMessage($"You said: {msg.Content}"); // For testing
        if (msg.Content.StartsWith("!"))
        {
            _chatService.SendMessage($"You issued the command: {msg.Content}"); // For testing
        }
    }

    public override void Dispose()
    {
        if (_chatService != null)
        {
            _chatService.OnChatMessageRecieved -= ChatService_OnChatMessageRecieved;
        }
        base.Dispose();
    }
}
