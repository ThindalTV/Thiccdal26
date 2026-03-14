using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Thiccdal.Infrastructure.Bot;
using Thiccdal.Infrastructure.Bot.Models;

namespace Thiccdal.Modules.ChatBot;

public class BotCommandWorker : BackgroundService
{
    private readonly IServiceScope _serviceScope;
    private IChatService? _chatService;
    private readonly ILogger<BotCommandWorker> _logger;

    public BotCommandWorker(IServiceProvider sp, ILogger<BotCommandWorker> logger)
    {
        _serviceScope = sp.CreateScope();
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_chatService == null)
        {
            _chatService = _serviceScope.ServiceProvider.GetRequiredService<IChatService>();
        }

        _chatService.OnChatMessageRecieved += ChatService_OnChatMessageRecieved;

        await _chatService.Connect(stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            await Task.Delay(5000);
        }
    }

    protected void ChatService_OnChatMessageRecieved(object? sender, ChatEvent msg)
    {
        if (_chatService == null)
        {
            _logger.LogError("Chat service is not initialized.");
            return;
        }

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
        _serviceScope.Dispose();
        if (_chatService != null)
        {
            _chatService.OnChatMessageRecieved -= ChatService_OnChatMessageRecieved;
        }
        base.Dispose();
    }
}
