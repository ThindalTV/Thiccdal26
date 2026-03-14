using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Thiccdal.Infrastructure.Bot;
using Thiccdal.Infrastructure.Bot.Models;

namespace Thiccdal.Modules.ChatBot;

public class BotCommandWorker : BackgroundService, IDisposable
{
    private readonly IChatService _chatService;
    private readonly ILogger<BotCommandWorker> _logger;

    public BotCommandWorker(IServiceProvider sp, ILogger<BotCommandWorker> logger)
    {
        using var scope = sp.CreateScope();
        _chatService = scope.ServiceProvider.GetRequiredService<IChatService>();
        _chatService.OnChatMessageRecieved += ChatService_OnChatMessageRecieved;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _chatService.Connect(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            // TODO: Add scheduled messaging here
            await Task.Delay(1000);
        }
    }

    protected void ChatService_OnChatMessageRecieved(object? sender, ChatEvent msg)
    {
        if (_chatService == null)
        {
            _logger.LogError("Chat service is not initialized.");
            return;
        }

        // TODO: Implement timed message sending here. Below is for example purposes only.
        /*if (msg.Content.StartsWith("!"))
        {
            _chatService.SendMessage($"You issued the command: {msg.Content}"); // For testing
        }
        else
        {
            _chatService.SendMessage($"You said: {msg.Content}"); // For testing
        }*/
    }

    public override void Dispose()
    {
        _chatService.OnChatMessageRecieved -= ChatService_OnChatMessageRecieved;
        base.Dispose();
    }
}
