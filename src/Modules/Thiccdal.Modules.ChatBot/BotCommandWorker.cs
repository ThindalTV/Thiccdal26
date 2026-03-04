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
    private readonly ILogger<IHostedService> _logger;

    public BotCommandWorker(IServiceProvider sp, ILogger<IHostedService> logger)
    {
        _serviceScope = sp.CreateScope();
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_chatService == null)
        {
            _chatService = _serviceScope.ServiceProvider.GetRequiredService<IChatService>();
            return;
        }

        _chatService.OnChatMessageRecieved += ChatService_OnChatMessageRecieved;

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            await _chatService.Connect(stoppingToken);
        }
    }

    protected void ChatService_OnChatMessageRecieved(object? sender, ChatEvent msg)
    {
        if (_chatService == null)
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
        _serviceScope.Dispose();
        if (_chatService != null)
        {
            _chatService.OnChatMessageRecieved -= ChatService_OnChatMessageRecieved;
        }
        base.Dispose();
    }
}
