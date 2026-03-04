using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Thiccdal.Infrastructure.Bot;
using Thiccdal.Infrastructure.Bot.Models;

namespace Thiccdal.Modules.ChatBot;

public class BotCommandWorker
{
    private readonly IChatService _chatService;
    private readonly ILogger<IHostedService> _logger;

    public BotCommandWorker(IChatService chatService, ILogger<IHostedService> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _chatService.OnChatMessageRecieved += ChatService_OnChatMessageRecieved;

        while (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            await _chatService.Connect(cancellationToken);
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

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _chatService.OnChatMessageRecieved -= ChatService_OnChatMessageRecieved;
        return Task.CompletedTask;
    }
}
