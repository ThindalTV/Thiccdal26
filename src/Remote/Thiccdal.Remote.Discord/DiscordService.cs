using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Infrastructure.Discord;
using Thiccdal.Infrastructure.Remotes;
using DiscordConnectionState = Thiccdal.Infrastructure.Discord.DiscordConnectionState;

namespace Thiccdal.Remote.Discord;

/// <summary>
/// Discord platform connection implementation using Discord.Net.
/// </summary>
public class DiscordService : IDiscordService, IAsyncDisposable, IDisposable
{
    private readonly DiscordOptions _options;
    private readonly IEventBus _eventBus;
    private readonly ILogger<DiscordService> _logger;

    private DiscordSocketClient? _client;
    private DiscordConnectionState _connectionState = DiscordConnectionState.NotAuthorized;
    private TaskCompletionSource _readyTcs = new();
    private CancellationTokenSource? _reconnectCts;
    private bool _relayWarningLogged;

    public string PlatformName => "Discord";
    public DiscordConnectionState ConnectionState => _connectionState;
    public PlatformConnectionState State => MapState(_connectionState);
    public string? LastError { get; private set; }
    public DiscordRelayStatus RelayStatus => DiscordRelaySupport.BlockedStatus;
    public bool Connected => _client?.ConnectionState == global::Discord.ConnectionState.Connected && 
                            _connectionState == DiscordConnectionState.Connected;

    public event EventHandler<DiscordConnectionState>? ConnectionStateChanged;
    public event EventHandler<ChatEvent>? OnChatMessageReceived;
    public event EventHandler<PlatformEvent>? OnPlatformEventReceived;

    public DiscordService(
        IOptions<DiscordOptions> options,
        IEventBus eventBus,
        ILogger<DiscordService> logger)
    {
        _options = options.Value;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task RefreshConnectionState(CancellationToken cancellationToken = default)
    {
        if (_connectionState == DiscordConnectionState.Connected || _connectionState == DiscordConnectionState.Connecting)
        {
            return;
        }

        bool hasToken = !string.IsNullOrWhiteSpace(_options.BotToken) &&
                       !string.IsNullOrWhiteSpace(_options.GuildId) &&
                       !string.IsNullOrWhiteSpace(_options.StreamChannelId);

        SetState(hasToken ? DiscordConnectionState.NotAuthorized : DiscordConnectionState.NotAuthorized);
    }

    public async Task Connect(CancellationToken cancellationToken = default)
    {
        if (Connected)
        {
            _logger.LogInformation("Discord bot is already connected");
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.BotToken))
        {
            _logger.LogWarning("Discord bot token is not configured; cannot connect");
            SetState(DiscordConnectionState.NotAuthorized);
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.GuildId))
        {
            _logger.LogWarning("Discord guild ID is not configured; cannot connect");
            SetState(DiscordConnectionState.NotAuthorized);
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.StreamChannelId))
        {
            _logger.LogWarning("Discord stream channel ID is not configured; cannot connect");
            SetState(DiscordConnectionState.NotAuthorized);
            return;
        }

        LogRelayBlockedWarningIfConfigured();

        SetState(DiscordConnectionState.Connecting);

        try
        {
            _readyTcs = new TaskCompletionSource();

            var config = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.Guilds |
                                GatewayIntents.GuildMessages |
                                GatewayIntents.MessageContent |
                                GatewayIntents.GuildMembers |
                                GatewayIntents.GuildMessageReactions
            };

            _client = new DiscordSocketClient(config);

            _client.Ready += OnClientReady;
            _client.Disconnected += OnClientDisconnected;
            _client.Log += OnClientLog;
            _client.MessageReceived += OnMessageReceived;
            _client.ReactionAdded += OnReactionAdded;
            _client.UserJoined += OnUserJoined;
            _client.UserLeft += OnUserLeft;
            _client.MessageDeleted += OnMessageDeleted;

            await _client.LoginAsync(TokenType.Bot, _options.BotToken);
            await _client.StartAsync();

            await _readyTcs.Task.WaitAsync(cancellationToken);

            SetState(DiscordConnectionState.Connected);
            _logger.LogInformation("Discord bot connected successfully");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to connect Discord bot");
            LastError = ex.Message;
            SetState(DiscordConnectionState.Error);
            throw;
        }
    }

    public async Task Disconnect(CancellationToken cancellationToken = default)
    {
        _reconnectCts?.Cancel();

        if (_client is not null)
        {
            _logger.LogInformation("Disconnecting Discord bot");

            _client.Ready -= OnClientReady;
            _client.Disconnected -= OnClientDisconnected;
            _client.Log -= OnClientLog;
            _client.MessageReceived -= OnMessageReceived;
            _client.ReactionAdded -= OnReactionAdded;
            _client.UserJoined -= OnUserJoined;
            _client.UserLeft -= OnUserLeft;
            _client.MessageDeleted -= OnMessageDeleted;

            await _client.StopAsync();
            await _client.DisposeAsync();
            _client = null;
        }

        SetState(DiscordConnectionState.Disconnected);
    }

    public async Task SendMessage(string message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        if (_client is null || !Connected)
        {
            _logger.LogWarning("Cannot send Discord message: bot is not connected");
            return;
        }

        try
        {
            if (!ulong.TryParse(_options.GuildId, out ulong guildId))
            {
                _logger.LogError("Invalid Discord guild ID: {GuildId}", _options.GuildId);
                return;
            }

            if (!ulong.TryParse(_options.StreamChannelId, out ulong channelId))
            {
                _logger.LogError("Invalid Discord stream channel ID: {ChannelId}", _options.StreamChannelId);
                return;
            }

            var guild = _client.GetGuild(guildId);
            if (guild is null)
            {
                _logger.LogError("Discord guild not found: {GuildId}", guildId);
                return;
            }

            var channel = guild.GetTextChannel(channelId);
            if (channel is null)
            {
                _logger.LogError("Discord text channel not found: {ChannelId}", channelId);
                return;
            }

            await channel.SendMessageAsync(message);
            _logger.LogDebug("Sent Discord message to channel {ChannelId}", channelId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to send Discord message");
        }
    }

    public Task StartRelay(string rtmpUrl, string streamKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(_options.VoiceChannelId))
        {
            const string message = "Discord voice channel ID is not configured; cannot start Discord relay.";
            _logger.LogError(message);
            throw new PlatformOperationException(message);
        }

        if (!ulong.TryParse(_options.VoiceChannelId, out _))
        {
            string message = $"Discord voice channel ID '{_options.VoiceChannelId}' is invalid; cannot start Discord relay.";
            _logger.LogError(message);
            throw new PlatformOperationException(message);
        }

        // Discord.Net exposes audio voice connectivity, but it does not offer a supported video transport for bots.
        // Starting an audio-only voice session here would falsely report RTMP relay success even though Thiccdal cannot publish Discord Go Live video.
        _logger.LogError(
            "Discord RTMP relay cannot start for voice channel {ChannelId}: {Reason}",
            _options.VoiceChannelId,
            RelayStatus.StatusMessage);

        throw new PlatformOperationException(RelayStatus.StatusMessage);
    }

    public Task StopRelay(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation(
            "Discord RTMP relay stop requested, but no relay session can exist: {Reason}",
            RelayStatus.StatusMessage);

        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await Disconnect();
        GC.SuppressFinalize(this);
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private Task OnClientReady()
    {
        _logger.LogInformation("Discord client ready");

        if (ulong.TryParse(_options.GuildId, out ulong guildId) &&
            ulong.TryParse(_options.StreamChannelId, out ulong channelId))
        {
            var guild = _client?.GetGuild(guildId);
            if (guild is not null)
            {
                var channel = guild.GetTextChannel(channelId);
                if (channel is null)
                {
                    _logger.LogWarning(
                        "Discord stream channel {ChannelId} not found in guild {GuildId}; it may be created later",
                        channelId,
                        guildId);
                }
            }
        }

        _readyTcs.TrySetResult();
        return Task.CompletedTask;
    }

    private Task OnClientDisconnected(Exception exception)
    {
        _logger.LogWarning(exception, "Discord client disconnected unexpectedly");
        LastError = exception.Message;
        SetState(DiscordConnectionState.Error);

        _ = AttemptReconnect();
        return Task.CompletedTask;
    }

    private async Task AttemptReconnect()
    {
        _reconnectCts?.Cancel();
        _reconnectCts = new CancellationTokenSource();

        await Task.Delay(TimeSpan.FromSeconds(_options.ReconnectDelaySeconds), _reconnectCts.Token);

        if (!_reconnectCts.Token.IsCancellationRequested)
        {
            _logger.LogInformation("Attempting to reconnect Discord bot");
            try
            {
                await Connect(_reconnectCts.Token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reconnect Discord bot");
            }
        }
    }

    private Task OnClientLog(LogMessage log)
    {
        var logLevel = log.Severity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Info => LogLevel.Information,
            LogSeverity.Verbose => LogLevel.Debug,
            LogSeverity.Debug => LogLevel.Trace,
            _ => LogLevel.Information
        };

        _logger.Log(logLevel, log.Exception, "[Discord.Net] {Source}: {Message}", log.Source, log.Message);
        return Task.CompletedTask;
    }

    private async Task OnMessageReceived(SocketMessage message)
    {
        if (!ulong.TryParse(_options.StreamChannelId, out ulong channelId) || message.Channel.Id != channelId)
        {
            return;
        }

        if (message.Author.IsBot)
        {
            return;
        }

        try
        {
            string channelName = message.Channel.Name;
            string channelIdText = message.Channel.Id.ToString();
            var chatEvent = DiscordEventMapper.ToChatEvent(message, channelIdText, channelName);
            await PersistAndDispatchEvent(chatEvent);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to process Discord message event");
        }
    }

    private async Task OnReactionAdded(
        Cacheable<IUserMessage, ulong> message,
        Cacheable<IMessageChannel, ulong> channel,
        SocketReaction reaction)
    {
        if (!ulong.TryParse(_options.StreamChannelId, out ulong channelId) || channel.Id != channelId)
        {
            return;
        }

        try
        {
            string channelName = channel.HasValue ? channel.Value.Name : "Unknown";
            string? userName = reaction.User.IsSpecified ? reaction.User.Value.GlobalName ?? reaction.User.Value.Username : null;
            var reactionEvent = DiscordEventMapper.ToReactionEvent(reaction, channelName, userName);
            await PersistAndDispatchEvent(reactionEvent);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to process Discord reaction event");
        }
    }

    private async Task OnUserJoined(SocketGuildUser user)
    {
        try
        {
            if (ulong.TryParse(_options.GuildId, out ulong guildId))
            {
                var guild = _client?.GetGuild(guildId);
                if (guild is not null)
                {
                    var joinEvent = DiscordEventMapper.ToUserJoinedEvent(user, guild.Name);
                    await PersistAndDispatchEvent(joinEvent);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to process Discord user joined event");
        }
    }

    private async Task OnUserLeft(SocketGuild guild, SocketUser user)
    {
        try
        {
            var leftEvent = DiscordEventMapper.ToUserLeftEvent(user, guild);
            await PersistAndDispatchEvent(leftEvent);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to process Discord user left event");
        }
    }

    private async Task OnMessageDeleted(
        Cacheable<IMessage, ulong> message,
        Cacheable<IMessageChannel, ulong> channel)
    {
        try
        {
            var deleteEvent = DiscordEventMapper.ToMessageDeletedEvent(message.Id, message, channel);
            await PersistAndDispatchEvent(deleteEvent);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to process Discord message deleted event");
        }
    }

    private async Task PersistAndDispatchEvent(PlatformEvent platformEvent)
    {
        try
        {
            await _eventBus.Publish(platformEvent);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to persist Discord platform event {EventType}", platformEvent.Type);
        }

        OnPlatformEventReceived?.Invoke(this, platformEvent);

        if (platformEvent is ChatEvent chatEvent)
        {
            OnChatMessageReceived?.Invoke(this, chatEvent);
        }
    }

    private void SetState(DiscordConnectionState state)
    {
        if (_connectionState == state)
        {
            return;
        }

        _connectionState = state;
        if (state != DiscordConnectionState.Error)
        {
            LastError = null;
        }

        _logger.LogInformation("Discord connection state: {State}", state);
        ConnectionStateChanged?.Invoke(this, state);
    }

    private void LogRelayBlockedWarningIfConfigured()
    {
        if (_relayWarningLogged || string.IsNullOrWhiteSpace(_options.VoiceChannelId))
        {
            return;
        }

        _relayWarningLogged = true;
        _logger.LogWarning(
            "Discord voice channel {ChannelId} is configured, but relay remains blocked: {Reason}",
            _options.VoiceChannelId,
            RelayStatus.StatusMessage);
    }

    private static PlatformConnectionState MapState(DiscordConnectionState state)
    {
        return state switch
        {
            DiscordConnectionState.Connected => PlatformConnectionState.Connected,
            DiscordConnectionState.Connecting => PlatformConnectionState.Connecting,
            DiscordConnectionState.Error => PlatformConnectionState.Error,
            _ => PlatformConnectionState.Disconnected
        };
    }
}
