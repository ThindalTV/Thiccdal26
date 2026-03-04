using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Thiccdal.Infrastructure.Bot;
using Thiccdal.Infrastructure.Bot.Models;

namespace Thiccdal.Modules.ChatBot;

public class BotCommandWorker : BackgroundService
{
    private readonly IChatService _chatService;
    private readonly ILogger<IHostedService> _logger;

    public BotCommandWorker(Lazy<IChatService> chatServiceLazy, ILogger<IHostedService> logger)
    {
        _chatService = chatServiceLazy.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _chatService.OnChatMessageRecieved += ChatService_OnChatMessageRecieved;

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            await _chatService.Connect(stoppingToken);
        }
    }

    protected void ChatService_OnChatMessageRecieved(object? sender, ChatEvent msg)
    {
        _chatService.SendMessage($"You said: {msg.Content}"); // For testing
        if (msg.Content.StartsWith("!"))
        {
            _chatService.SendMessage($"You issued the command: {msg.Content}"); // For testing
        }
    }

    public override void Dispose()
    {
        _chatService.OnChatMessageRecieved -= ChatService_OnChatMessageRecieved;
    }
}
