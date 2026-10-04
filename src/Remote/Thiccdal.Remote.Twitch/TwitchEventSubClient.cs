using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Infrastructure.Twitch;

namespace Thiccdal.Remote.Twitch;

public sealed class TwitchEventSubClient : ITwitchEventSubClient, IAsyncDisposable, IDisposable
{
    private readonly TwitchOptions _options;
    private readonly ITwitchHelixClient _helixClient;
    private readonly ITwitchTokenManager _tokenManager;
    private readonly TwitchEventSubNotificationMapper _mapper;
    private readonly ILogger<TwitchEventSubClient> _logger;
    private readonly SemaphoreSlim _connectionGate;
    private readonly Queue<string> _recentMessageIds;
    private readonly HashSet<string> _recentMessageIdSet;

    // Twitch binds a WebSocket session to the single account that created its subscriptions, so each
    // authorized account gets its own session: the bot carries chat, the broadcaster carries the
    // channel-level events only the channel owner may read.
    private readonly Dictionary<TwitchTokenRole, EventSubSession> _sessions = [];

    private TwitchChatConnectionProfile? _profile;

    public TwitchEventSubClient(
        IOptions<TwitchOptions> options,
        ITwitchHelixClient helixClient,
        ITwitchTokenManager tokenManager,
        TwitchEventSubNotificationMapper mapper,
        ILogger<TwitchEventSubClient> logger)
    {
        _options = options.Value;
        _helixClient = helixClient;
        _tokenManager = tokenManager;
        _mapper = mapper;
        _logger = logger;
        _connectionGate = new SemaphoreSlim(1, 1);
        _recentMessageIds = new Queue<string>();
        _recentMessageIdSet = [];
    }

    public bool Connected => _sessions.Count > 0;

    public event EventHandler<PlatformEvent>? OnEventReceived;
    public event EventHandler<ChatEvent>? ChatMessageReceived;
    public event EventHandler<PlatformEvent>? PlatformEventReceived;
    public event EventHandler? Disconnected;
    public event EventHandler<Exception>? Faulted;

    public async Task Connect(TwitchChatConnectionProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        await _connectionGate.WaitAsync(cancellationToken);
        try
        {
            _profile = profile;
            await ConnectSessions(profile, cancellationToken);
        }
        finally
        {
            _connectionGate.Release();
        }
    }

    public async Task RefreshSubscriptions(CancellationToken cancellationToken = default)
    {
        await _connectionGate.WaitAsync(cancellationToken);
        try
        {
            if (_profile is null)
            {
                _logger.LogInformation("Skipping EventSub subscription refresh because no session is connected.");
                return;
            }

            // A session cannot take on subscriptions from another account, so a newly authorized
            // account needs a session of its own.
            await ConnectSessions(_profile, cancellationToken);
        }
        finally
        {
            _connectionGate.Release();
        }
    }

    public async Task Disconnect(CancellationToken cancellationToken = default)
    {
        await _connectionGate.WaitAsync(cancellationToken);
        try
        {
            foreach (TwitchTokenRole role in _sessions.Keys.ToArray())
            {
                await DisconnectSession(role, cancellationToken);
            }
        }
        finally
        {
            _connectionGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        // Use a generous timeout so callers are not blocked indefinitely if a reconnect is in progress.
        using CancellationTokenSource disposeCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            await Disconnect(disposeCts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("TwitchEventSubClient disposal timed out waiting to acquire the connection gate");
        }

        _connectionGate.Dispose();
        GC.SuppressFinalize(this);
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private async Task ConnectSessions(TwitchChatConnectionProfile profile, CancellationToken cancellationToken)
    {
        bool hasBot = await _tokenManager.HasToken(TwitchTokenRole.Bot, cancellationToken);
        bool hasBroadcaster = await _tokenManager.HasToken(TwitchTokenRole.Broadcaster, cancellationToken);

        if (!hasBot && !hasBroadcaster)
        {
            _logger.LogWarning("No Twitch account is authorized, so no EventSub session was opened.");
            return;
        }

        if (!hasBroadcaster)
        {
            _logger.LogWarning(
                "The Twitch broadcaster account is not authorized, so follows, subscriptions, cheers, and redemptions will not arrive.");
        }

        if (!hasBot)
        {
            _logger.LogWarning("The Twitch bot account is not authorized, so chat is read by the broadcaster account.");
        }

        foreach (TwitchTokenRole role in Enum.GetValues<TwitchTokenRole>())
        {
            bool isAuthorized = role == TwitchTokenRole.Broadcaster ? hasBroadcaster : hasBot;
            if (!isAuthorized)
            {
                await DisconnectSession(role, cancellationToken);
                continue;
            }

            await ConnectCore(_options.EventSub.WebSocketUrl, profile, role, subscribe: true, cancellationToken);
        }
    }

    private async Task ConnectCore(
        string webSocketUrl,
        TwitchChatConnectionProfile profile,
        TwitchTokenRole role,
        bool subscribe,
        CancellationToken cancellationToken,
        bool calledFromListenTask = false)
    {
        await DisconnectSession(role, cancellationToken, awaitListenTask: !calledFromListenTask);

        ClientWebSocket socket = new();
        await socket.ConnectAsync(new Uri(webSocketUrl, UriKind.Absolute), cancellationToken);

        string? welcomePayload = await ReceiveTextMessage(socket, cancellationToken);
        if (string.IsNullOrWhiteSpace(welcomePayload))
        {
            throw new InvalidOperationException("Twitch EventSub did not send a session_welcome payload.");
        }

        string sessionId = GetSessionId(welcomePayload);
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new InvalidOperationException("Twitch EventSub session_welcome payload did not include a session id.");
        }

        if (subscribe)
        {
            await EnsureSubscriptions(profile, sessionId, role, cancellationToken);
        }

        _profile = profile;
        EventSubSession session = new EventSubSession(role, socket, sessionId);
        _sessions[role] = session;
        session.ListenTask = Listen(profile, role, socket, session.ListenCancellation.Token);

        _logger.LogInformation(
            "Connected Twitch EventSub session {SessionId} owned by the {Role} account for broadcaster {BroadcasterId}",
            sessionId,
            role,
            profile.BroadcasterId);
    }

    private async Task Listen(
        TwitchChatConnectionProfile profile,
        TwitchTokenRole role,
        ClientWebSocket socket,
        CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                string? payload = await ReceiveTextMessage(socket, cancellationToken);
                if (string.IsNullOrWhiteSpace(payload))
                {
                    break;
                }

                string messageType = GetMessageType(payload);
                switch (messageType)
                {
                    case "session_keepalive":
                        continue;

                    case "notification":
                        if (ShouldHandle(payload))
                        {
                            PlatformEvent platformEvent = _mapper.Map(payload);
                            OnEventReceived?.Invoke(this, platformEvent);
                            if (platformEvent is ChatEvent chatEvent)
                            {
                                ChatMessageReceived?.Invoke(this, chatEvent);
                            }
                            else
                            {
                                PlatformEventReceived?.Invoke(this, platformEvent);
                            }
                        }
                        continue;

                    case "session_reconnect":
                        string reconnectUrl = GetReconnectUrl(payload);
                        if (!string.IsNullOrWhiteSpace(reconnectUrl))
                        {
                            _logger.LogInformation("Reconnecting Twitch EventSub session using server-provided reconnect URL");
                            await _connectionGate.WaitAsync(cancellationToken);
                            try
                            {
                                // CancellationToken.None would be cancelled by DisconnectCore (which cancels
                                // _listenCancellation, the same source as `cancellationToken`). Use a fresh
                                // timeout-bounded token so the reconnect attempt can be cancelled independently.
                                using CancellationTokenSource reconnectCts = new CancellationTokenSource(
                                    TimeSpan.FromSeconds(60));
                                await ConnectCore(
                                    reconnectUrl,
                                    profile,
                                    role,
                                    subscribe: false,
                                    reconnectCts.Token,
                                    calledFromListenTask: true);
                            }
                            finally
                            {
                                _connectionGate.Release();
                            }
                        }
                        return;

                    case "revocation":
                        _logger.LogWarning("Twitch EventSub subscription was revoked: {Payload}", payload);
                        continue;

                    default:
                        _logger.LogDebug("Ignoring Twitch EventSub message type {MessageType}", messageType);
                        continue;
                }
            }

            _sessions.Remove(role);
            if (!cancellationToken.IsCancellationRequested)
            {
                Disconnected?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Stopped the Twitch EventSub {Role} listener due to cancellation", role);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected Twitch EventSub listener failure on the {Role} session", role);
            _sessions.Remove(role);
            Faulted?.Invoke(this, ex);
        }
    }

    private async Task DisconnectSession(
        TwitchTokenRole role,
        CancellationToken cancellationToken,
        bool awaitListenTask = true)
    {
        if (!_sessions.Remove(role, out EventSubSession? session))
        {
            return;
        }

        Task? listenTask = session.ListenTask;
        ClientWebSocket socket = session.Socket;

        await session.ListenCancellation.CancelAsync();
        session.ListenCancellation.Dispose();

        if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing Twitch EventSub session", cancellationToken);
        }

        socket.Dispose();

        // Skip awaiting the listen task when DisconnectSession is called from within the listen task
        // itself (e.g. during session_reconnect handling). Task.CurrentId is unreliable in async
        // continuations, so callers must pass awaitListenTask=false to signal this.
        if (awaitListenTask && listenTask != null)
        {
            try
            {
                await listenTask;
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private async Task EnsureSubscriptions(
        TwitchChatConnectionProfile profile,
        string sessionId,
        TwitchTokenRole sessionRole,
        CancellationToken cancellationToken)
    {
        bool hasBotSession = await _tokenManager.HasToken(TwitchTokenRole.Bot, cancellationToken);
        bool hasBroadcasterSession = await _tokenManager.HasToken(TwitchTokenRole.Broadcaster, cancellationToken);

        IReadOnlyList<TwitchEventSubSubscription> existingSubscriptions =
            await _helixClient.GetEventSubscriptions(sessionRole, cancellationToken);

        foreach (TwitchEventSubSubscriptionRequest request in BuildSubscriptionRequests(
            profile,
            sessionId,
            sessionRole,
            hasBotSession,
            hasBroadcasterSession))
        {
            TwitchEventSubSubscription? stale = existingSubscriptions.FirstOrDefault(
                subscription => SubscriptionMatchesRequest(subscription, request) &&
                                !string.Equals(subscription.SessionId, sessionId, StringComparison.Ordinal));

            if (stale is not null)
            {
                _logger.LogInformation(
                    "Deleting stale EventSub subscription {SubscriptionId} ({Type}) bound to previous session.",
                    stale.Id,
                    stale.Type);

                try
                {
                    await _helixClient.DeleteEventSubscription(stale.Id, sessionRole, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Failed to delete stale Twitch EventSub subscription {SubscriptionId}.", stale.Id);
                }
            }

            bool alreadyCurrent = existingSubscriptions.Any(
                subscription => SubscriptionMatchesRequest(subscription, request) &&
                                string.Equals(subscription.SessionId, sessionId, StringComparison.Ordinal));

            if (alreadyCurrent)
            {
                continue;
            }

            try
            {
                await _helixClient.CreateEventSubscription(request, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Failed to create Twitch EventSub subscription {SubscriptionType}. No events of this kind will arrive.", request.Type);
            }
        }
    }

    internal static IEnumerable<TwitchEventSubSubscriptionRequest> BuildSubscriptionRequests(
        TwitchChatConnectionProfile profile,
        string sessionId,
        TwitchTokenRole sessionRole,
        bool hasBotSession,
        bool hasBroadcasterSession)
    {
        if (string.IsNullOrWhiteSpace(profile.BroadcasterId))
        {
            yield break;
        }

        if (sessionRole == TwitchTokenRole.Bot)
        {
            // Chat belongs to the bot session: the condition's user is the account that reads it.
            yield return CreateRequest(
                "channel.chat.message",
                "1",
                sessionId,
                new Dictionary<string, string>
                {
                    ["broadcaster_user_id"] = profile.BroadcasterId,
                    ["user_id"] = profile.BotUserId
                },
                TwitchTokenRole.Bot);

            // Raids need no scope, so the broadcaster session takes them when there is one.
            if (!hasBroadcasterSession)
            {
                yield return CreateRequest(
                    "channel.raid",
                    "1",
                    sessionId,
                    new Dictionary<string, string>
                    {
                        ["to_broadcaster_user_id"] = profile.BroadcasterId
                    },
                    TwitchTokenRole.Bot);
            }

            yield break;
        }

        if (!hasBotSession)
        {
            // No bot account, so the broadcaster reads its own chat.
            yield return CreateRequest(
                "channel.chat.message",
                "1",
                sessionId,
                new Dictionary<string, string>
                {
                    ["broadcaster_user_id"] = profile.BroadcasterId,
                    ["user_id"] = profile.BroadcasterId
                },
                TwitchTokenRole.Broadcaster);
        }

        yield return CreateRequest(
            "channel.raid",
            "1",
            sessionId,
            new Dictionary<string, string>
            {
                ["to_broadcaster_user_id"] = profile.BroadcasterId
            },
            TwitchTokenRole.Broadcaster);

        // Follower reads are a moderator permission and the broadcaster moderates their own channel,
        // so the broadcaster authorization covers this without the bot needing moderator status.
        yield return CreateRequest(
            "channel.follow",
            "2",
            sessionId,
            new Dictionary<string, string>
            {
                ["broadcaster_user_id"] = profile.BroadcasterId,
                ["moderator_user_id"] = profile.BroadcasterId
            },
            TwitchTokenRole.Broadcaster);

        yield return CreateRequest(
            "channel.subscribe",
            "1",
            sessionId,
            new Dictionary<string, string>
            {
                ["broadcaster_user_id"] = profile.BroadcasterId
            },
            TwitchTokenRole.Broadcaster);

        // channel.subscribe only fires for new subscriptions, so resubs and gift batches need their own topics.
        yield return CreateRequest(
            "channel.subscription.message",
            "1",
            sessionId,
            new Dictionary<string, string>
            {
                ["broadcaster_user_id"] = profile.BroadcasterId
            },
            TwitchTokenRole.Broadcaster);

        yield return CreateRequest(
            "channel.subscription.gift",
            "1",
            sessionId,
            new Dictionary<string, string>
            {
                ["broadcaster_user_id"] = profile.BroadcasterId
            },
            TwitchTokenRole.Broadcaster);

        yield return CreateRequest(
            "channel.cheer",
            "1",
            sessionId,
            new Dictionary<string, string>
            {
                ["broadcaster_user_id"] = profile.BroadcasterId
            },
            TwitchTokenRole.Broadcaster);

        yield return CreateRequest(
            "channel.channel_points_custom_reward_redemption.add",
            "1",
            sessionId,
            new Dictionary<string, string>
            {
                ["broadcaster_user_id"] = profile.BroadcasterId
            },
            TwitchTokenRole.Broadcaster);
    }

    private static TwitchEventSubSubscriptionRequest CreateRequest(
        string type,
        string version,
        string sessionId,
        IReadOnlyDictionary<string, string> condition,
        TwitchTokenRole role)
    {
        return new TwitchEventSubSubscriptionRequest
        {
            Type = type,
            Version = version,
            SessionId = sessionId,
            Condition = condition,
            Role = role
        };
    }

    private static bool SubscriptionMatchesRequest(
        TwitchEventSubSubscription existing,
        TwitchEventSubSubscriptionRequest request)
    {
        if (!string.Equals(existing.Type, request.Type, StringComparison.Ordinal) ||
            !string.Equals(existing.Version, request.Version, StringComparison.Ordinal) ||
            existing.Condition.Count != request.Condition.Count)
        {
            return false;
        }

        foreach ((string key, string value) in request.Condition)
        {
            if (!existing.Condition.TryGetValue(key, out string? existingValue) ||
                !string.Equals(existingValue, value, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private bool ShouldHandle(string payload)
    {
        string messageId = GetMetadataValue(payload, "message_id");
        if (string.IsNullOrWhiteSpace(messageId))
        {
            return true;
        }

        lock (_recentMessageIdSet)
        {
            if (_recentMessageIdSet.Contains(messageId))
            {
                return false;
            }

            _recentMessageIdSet.Add(messageId);
            _recentMessageIds.Enqueue(messageId);
            while (_recentMessageIds.Count > 256)
            {
                string removedMessageId = _recentMessageIds.Dequeue();
                _recentMessageIdSet.Remove(removedMessageId);
            }
        }

        return true;
    }

    private static async Task<string?> ReceiveTextMessage(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        ArraySegment<byte> buffer = new(new byte[4096]);
        using MemoryStream stream = new();

        while (true)
        {
            WebSocketReceiveResult result = await socket.ReceiveAsync(buffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            stream.Write(buffer.Array!, buffer.Offset, result.Count);
            if (result.EndOfMessage)
            {
                break;
            }
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string GetMessageType(string payload) => GetMetadataValue(payload, "message_type");

    private static string GetSessionId(string payload)
    {
        using JsonDocument document = JsonDocument.Parse(payload);
        return document.RootElement
            .GetProperty("payload")
            .GetProperty("session")
            .GetProperty("id")
            .GetString() ?? string.Empty;
    }

    private static string GetReconnectUrl(string payload)
    {
        using JsonDocument document = JsonDocument.Parse(payload);
        return document.RootElement
            .GetProperty("payload")
            .GetProperty("session")
            .GetProperty("reconnect_url")
            .GetString() ?? string.Empty;
    }

    private static string GetMetadataValue(string payload, string propertyName)
    {
        using JsonDocument document = JsonDocument.Parse(payload);
        if (!document.RootElement.TryGetProperty("metadata", out JsonElement metadataElement) ||
            !metadataElement.TryGetProperty(propertyName, out JsonElement propertyElement))
        {
            return string.Empty;
        }

        return propertyElement.GetString() ?? string.Empty;
    }

    private sealed class EventSubSession
    {
        public EventSubSession(TwitchTokenRole role, ClientWebSocket socket, string sessionId)
        {
            Role = role;
            Socket = socket;
            SessionId = sessionId;
            ListenCancellation = new CancellationTokenSource();
        }

        public TwitchTokenRole Role { get; }

        public ClientWebSocket Socket { get; }

        public string SessionId { get; }

        public CancellationTokenSource ListenCancellation { get; }

        public Task? ListenTask { get; set; }
    }
}
