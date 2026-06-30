using System.Collections.Concurrent;
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
    private readonly TwitchEventSubNotificationMapper _mapper;
    private readonly ILogger<TwitchEventSubClient> _logger;
    private readonly SemaphoreSlim _connectionGate;
    private readonly ConcurrentQueue<string> _recentMessageIds;
    private readonly HashSet<string> _recentMessageIdSet;

    private ClientWebSocket? _socket;
    private CancellationTokenSource? _listenCancellation;
    private Task? _listenTask;
    private TwitchChatConnectionProfile? _profile;

    public TwitchEventSubClient(
        IOptions<TwitchOptions> options,
        ITwitchHelixClient helixClient,
        TwitchEventSubNotificationMapper mapper,
        ILogger<TwitchEventSubClient> logger)
    {
        _options = options.Value;
        _helixClient = helixClient;
        _mapper = mapper;
        _logger = logger;
        _connectionGate = new SemaphoreSlim(1, 1);
        _recentMessageIds = new ConcurrentQueue<string>();
        _recentMessageIdSet = [];
    }

    public bool Connected { get; private set; }

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
            await ConnectCore(_options.EventSub.WebSocketUrl, profile, subscribe: true, cancellationToken);
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
            await DisconnectCore(cancellationToken);
        }
        finally
        {
            _connectionGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await Disconnect();
        _connectionGate.Dispose();
        GC.SuppressFinalize(this);
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private async Task ConnectCore(string webSocketUrl, TwitchChatConnectionProfile profile, bool subscribe, CancellationToken cancellationToken)
    {
        await DisconnectCore(cancellationToken);

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
            await EnsureSubscriptions(profile, sessionId, cancellationToken);
        }

        _socket = socket;
        Connected = true;
        _listenCancellation = new CancellationTokenSource();
        _listenTask = Listen(profile, socket, _listenCancellation.Token);

        _logger.LogInformation(
            "Connected Twitch EventSub session {SessionId} for broadcaster {BroadcasterId}",
            sessionId,
            profile.BroadcasterId);
    }

    private async Task Listen(TwitchChatConnectionProfile profile, ClientWebSocket socket, CancellationToken cancellationToken)
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
                                // CancellationToken.None: DisconnectCore cancels _listenCancellation (this task's token),
                                // so the caller's token must not be passed or the socket close and new ConnectAsync would
                                // be pre-cancelled by the same source they're trying to shut down.
                                await ConnectCore(reconnectUrl, profile, subscribe: false, CancellationToken.None);
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

            Connected = false;
            if (!cancellationToken.IsCancellationRequested)
            {
                Disconnected?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Stopped Twitch EventSub listener due to cancellation");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected Twitch EventSub listener failure");
            Connected = false;
            Faulted?.Invoke(this, ex);
        }
    }

    private async Task DisconnectCore(CancellationToken cancellationToken)
    {
        CancellationTokenSource? listenCancellation = _listenCancellation;
        Task? listenTask = _listenTask;
        ClientWebSocket? socket = _socket;

        _listenCancellation = null;
        _listenTask = null;
        _socket = null;
        Connected = false;

        if (listenCancellation != null)
        {
            await listenCancellation.CancelAsync();
            listenCancellation.Dispose();
        }

        if (socket != null)
        {
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing Twitch EventSub session", cancellationToken);
            }

            socket.Dispose();
        }

        if (listenTask != null && listenTask.Id != Task.CurrentId)
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
        CancellationToken cancellationToken)
    {
        IReadOnlyList<TwitchEventSubSubscription> existingSubscriptions = await _helixClient.GetEventSubscriptions(cancellationToken);
        foreach (TwitchEventSubSubscriptionRequest request in BuildSubscriptionRequests(profile, sessionId))
        {
            bool exists = existingSubscriptions.Any(subscription => SubscriptionMatches(subscription, request));
            if (exists)
            {
                continue;
            }

            try
            {
                await _helixClient.CreateEventSubscription(request, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Failed to create Twitch EventSub subscription {SubscriptionType}", request.Type);
            }
        }
    }

    private IEnumerable<TwitchEventSubSubscriptionRequest> BuildSubscriptionRequests(
        TwitchChatConnectionProfile profile,
        string sessionId)
    {
        if (string.IsNullOrWhiteSpace(profile.BroadcasterId))
        {
            yield break;
        }

        yield return CreateRequest(
            "channel.chat.message",
            "1",
            sessionId,
            new Dictionary<string, string>
            {
                ["broadcaster_user_id"] = profile.BroadcasterId,
                ["user_id"] = profile.BotUserId
            });

        if (!string.IsNullOrWhiteSpace(profile.BotUserId))
        {
            yield return CreateRequest(
                "channel.follow",
                "2",
                sessionId,
                new Dictionary<string, string>
                {
                    ["broadcaster_user_id"] = profile.BroadcasterId,
                    ["moderator_user_id"] = profile.BotUserId
                });
        }

        yield return CreateRequest(
            "channel.subscribe",
            "1",
            sessionId,
            new Dictionary<string, string>
            {
                ["broadcaster_user_id"] = profile.BroadcasterId
            });

        yield return CreateRequest(
            "channel.cheer",
            "1",
            sessionId,
            new Dictionary<string, string>
            {
                ["broadcaster_user_id"] = profile.BroadcasterId
            });

        yield return CreateRequest(
            "channel.raid",
            "1",
            sessionId,
            new Dictionary<string, string>
            {
                ["to_broadcaster_user_id"] = profile.BroadcasterId
            });

        yield return CreateRequest(
            "channel.channel_points_custom_reward_redemption.add",
            "1",
            sessionId,
            new Dictionary<string, string>
            {
                ["broadcaster_user_id"] = profile.BroadcasterId
            });
    }

    private static TwitchEventSubSubscriptionRequest CreateRequest(
        string type,
        string version,
        string sessionId,
        IReadOnlyDictionary<string, string> condition)
    {
        return new TwitchEventSubSubscriptionRequest
        {
            Type = type,
            Version = version,
            SessionId = sessionId,
            Condition = condition
        };
    }

    private static bool SubscriptionMatches(
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
            while (_recentMessageIds.Count > 256 && _recentMessageIds.TryDequeue(out string? removedMessageId))
            {
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
}
