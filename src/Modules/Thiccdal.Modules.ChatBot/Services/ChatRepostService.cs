using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Thiccdal.Infrastructure.Bot;
using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Infrastructure.Remotes;

namespace Thiccdal.Modules.ChatBot.Services;

/// <summary>
/// Listens to incoming chat messages and reposts them to other connected platforms.
/// </summary>
public sealed class ChatRepostService : IChatRepostService, IHostedService, IDisposable
{
    private static readonly HashSet<PlatformEventSource> SupportedTargetPlatforms = new HashSet<PlatformEventSource>
    {
        PlatformEventSource.Facebook,
        PlatformEventSource.Discord,
        PlatformEventSource.Twitch,
        PlatformEventSource.X,
        PlatformEventSource.YouTube
    };

    private readonly IReadOnlyList<IPlatformConnection> _platformConnections;
    private readonly IChatService _chatService;
    private readonly ILogger<ChatRepostService> _logger;
    private readonly ConcurrentDictionary<string, byte> _repostedMessageIds = new();
    private readonly ConcurrentDictionary<IPlatformConnection, PlatformEventSource> _platformSourceMap = new();

    private bool _disposed;

    public ChatRepostService(
        IEnumerable<IPlatformConnection> platformConnections,
        IChatService chatService,
        ILogger<ChatRepostService> logger)
    {
        _platformConnections = platformConnections.ToList();
        _chatService = chatService;
        _logger = logger;
        BuildPlatformSourceMap();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _chatService.OnChatMessageRecieved += HandleChatMessageReceived;
        _logger.LogInformation("Chat repost service started");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _chatService.OnChatMessageRecieved -= HandleChatMessageReceived;
        _logger.LogInformation("Chat repost service stopped");
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _chatService.OnChatMessageRecieved -= HandleChatMessageReceived;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void BuildPlatformSourceMap()
    {
        foreach (IPlatformConnection platformConnection in _platformConnections)
        {
            PlatformEventSource source = DeterminePlatformSource(platformConnection);
            _platformSourceMap.TryAdd(platformConnection, source);
            _logger.LogDebug("Mapped platform {Platform} to {Source}", platformConnection.GetType().Name, source);
        }
    }

    private void HandleChatMessageReceived(object? sender, ChatEvent chatEvent)
    {
        _ = RepostChatMessage(chatEvent);
    }

    private async Task RepostChatMessage(ChatEvent chatEvent)
    {
        try
        {
            if (ShouldSkipRepost(chatEvent))
            {
                return;
            }

            string repostKey = BuildRepostKey(chatEvent);
            if (!_repostedMessageIds.TryAdd(repostKey, 0))
            {
                _logger.LogDebug(
                    "Skipping already-reposted message {ExternalId} from {Source}",
                    chatEvent.ExternalId,
                    chatEvent.Source);
                return;
            }

            TrimRepostCache();

            string formattedMessage = FormatRepostMessage(chatEvent);

            await RepostToOtherPlatforms(chatEvent.Source, formattedMessage, CancellationToken.None);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(
                exception,
                "Failed to repost chat message {ExternalId} from {Source}",
                chatEvent.ExternalId,
                chatEvent.Source);
        }
    }

    private bool ShouldSkipRepost(ChatEvent chatEvent)
    {
        if (string.IsNullOrWhiteSpace(chatEvent.Content))
        {
            return true;
        }

        if (IsRepostedMessage(chatEvent.Content))
        {
            _logger.LogDebug(
                "Skipping message that appears to be a repost: {ExternalId} from {Source}",
                chatEvent.ExternalId,
                chatEvent.Source);
            return true;
        }

        return false;
    }

    private static bool IsRepostedMessage(string content)
    {
        return content.StartsWith('[') && content.Contains(']') && content.Contains(':');
    }

    private static string BuildRepostKey(ChatEvent chatEvent)
    {
        return $"{chatEvent.Source}:{chatEvent.ExternalId}:{chatEvent.OccurredAt:O}";
    }

    private void TrimRepostCache()
    {
        if (_repostedMessageIds.Count > 10000)
        {
            int removeCount = _repostedMessageIds.Count - 5000;
            foreach (string key in _repostedMessageIds.Keys.Take(removeCount))
            {
                _repostedMessageIds.TryRemove(key, out _);
            }
        }
    }

    private static string FormatRepostMessage(ChatEvent chatEvent)
    {
        return $"[{chatEvent.Source}] {chatEvent.Author}: {chatEvent.Content}";
    }

    private async Task RepostToOtherPlatforms(
        PlatformEventSource originPlatform,
        string message,
        CancellationToken cancellationToken)
    {
        List<Task> repostTasks = [];

        foreach (IPlatformConnection platformConnection in _platformConnections)
        {
            if (!platformConnection.Connected)
            {
                continue;
            }

            if (!_platformSourceMap.TryGetValue(platformConnection, out PlatformEventSource targetPlatform))
            {
                _logger.LogWarning(
                    "Platform connection {Platform} not found in source map",
                    platformConnection.GetType().Name);
                continue;
            }

            if (targetPlatform == originPlatform)
            {
                _logger.LogDebug(
                    "Skipping repost to origin platform {Platform}",
                    targetPlatform);
                continue;
            }

            if (!SupportedTargetPlatforms.Contains(targetPlatform))
            {
                _logger.LogDebug(
                    "Skipping repost to unsupported target platform {Platform}",
                    targetPlatform);
                continue;
            }

            repostTasks.Add(SendToTargetPlatform(platformConnection, targetPlatform, message, cancellationToken));
        }

        await Task.WhenAll(repostTasks);
    }

    private async Task SendToTargetPlatform(
        IPlatformConnection platformConnection,
        PlatformEventSource targetPlatform,
        string message,
        CancellationToken cancellationToken)
    {
        try
        {
            await platformConnection.SendMessage(message, cancellationToken);
            _logger.LogDebug("Reposted message to {TargetPlatform}", targetPlatform);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(
                exception,
                "Failed to repost message to {TargetPlatform}",
                targetPlatform);
        }
    }

    private static PlatformEventSource DeterminePlatformSource(IPlatformConnection platformConnection)
    {
        string className = platformConnection.GetType().Name;
        string classString = platformConnection.ToString() ?? className;

        return classString switch
        {
            string s when s.Contains("Twitch", StringComparison.OrdinalIgnoreCase) => PlatformEventSource.Twitch,
            string s when s.Contains("YouTube", StringComparison.OrdinalIgnoreCase) => PlatformEventSource.YouTube,
            string s when s.Contains("Discord", StringComparison.OrdinalIgnoreCase) => PlatformEventSource.Discord,
            string s when s.Contains("Facebook", StringComparison.OrdinalIgnoreCase) => PlatformEventSource.Facebook,
            string s when s.Contains("X", StringComparison.OrdinalIgnoreCase) && s.Contains("Service", StringComparison.OrdinalIgnoreCase) => PlatformEventSource.X,
            string s when s.Contains("LinkedIn", StringComparison.OrdinalIgnoreCase) => PlatformEventSource.LinkedIn,
            string s when s.Contains("TikTok", StringComparison.OrdinalIgnoreCase) => PlatformEventSource.TikTok,
            _ => PlatformEventSource.Null
        };
    }
}
