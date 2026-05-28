using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Infrastructure.X;

namespace Thiccdal.Remote.X;

public class XService : IXService, IAsyncDisposable, IDisposable
{
    private readonly XOptions _options;
    private readonly IXApiClient _apiClient;
    private readonly IEventBus _eventBus;
    private readonly ILogger<XService> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly bool _startPollingLoop;

    private readonly object _stateLock = new();
    private readonly HashSet<string> _seenLikeUserIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _seenRepostUserIds = new(StringComparer.Ordinal);

    private XConnectionState _connectionState = XConnectionState.NotAuthorized;
    private bool _isStreamLive;
    private CancellationTokenSource? _pollingCts;
    private Task? _pollingTask;
    private string? _lastSeenReplyId;
    private DateTimeOffset _nextReplyPollAt = DateTimeOffset.MinValue;
    private DateTimeOffset _nextEngagementPollAt = DateTimeOffset.MinValue;
    private bool _likesInitialized;
    private bool _repostsInitialized;

    public string PlatformName => "X";

    public XConnectionState ConnectionState => _connectionState;

    public PlatformConnectionState State => MapState(_connectionState);

    public string? LastError { get; private set; }

    public bool IsStreamLive => _isStreamLive;

    public bool Connected => _connectionState == XConnectionState.Connected;

    public event EventHandler<XConnectionState>? ConnectionStateChanged;

    public event EventHandler<bool>? StreamLiveStateChanged;

    public event EventHandler<ChatEvent>? OnChatMessageRecieved;

    public event EventHandler<PlatformEvent>? OnPlatformEventReceived;

    internal DateTimeOffset NextReplyPollAt => _nextReplyPollAt;

    internal DateTimeOffset NextEngagementPollAt => _nextEngagementPollAt;

    public XService(
        IOptions<XOptions> options,
        IXApiClient apiClient,
        IEventBus eventBus,
        ILogger<XService> logger,
        TimeProvider? timeProvider = null)
        : this(options, apiClient, eventBus, logger, timeProvider, true)
    {
    }

    internal XService(
        IOptions<XOptions> options,
        IXApiClient apiClient,
        IEventBus eventBus,
        ILogger<XService> logger,
        TimeProvider? timeProvider,
        bool startPollingLoop)
    {
        _options = options.Value;
        _apiClient = apiClient;
        _eventBus = eventBus;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _startPollingLoop = startPollingLoop;
    }

    public Task RefreshConnectionState(CancellationToken cancellationToken = default)
    {
        if (_connectionState == XConnectionState.Connected || _connectionState == XConnectionState.Connecting)
        {
            return Task.CompletedTask;
        }

        SetState(HasCredentials() ? XConnectionState.Authorized : XConnectionState.NotAuthorized);
        return Task.CompletedTask;
    }

    public Task RefreshStreamState(CancellationToken cancellationToken = default)
    {
        SetStreamLive(false);
        return Task.CompletedTask;
    }

    public Task Connect(CancellationToken cancellationToken = default)
    {
        if (Connected)
        {
            _logger.LogInformation("X is already connected");
            return Task.CompletedTask;
        }

        if (!HasCredentials())
        {
            _logger.LogWarning("X credentials are not configured; cannot connect");
            SetState(XConnectionState.NotAuthorized);
            return Task.CompletedTask;
        }

        SetState(XConnectionState.Connecting);

        try
        {
            ResetPollingState();
            if (_startPollingLoop)
            {
                _pollingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _pollingTask = PollLoop(_pollingCts.Token);
            }

            if (string.IsNullOrWhiteSpace(_options.BroadcastTweetId))
            {
                _logger.LogWarning(
                    "X connected without a tracked BroadcastTweetId. Reply, like, and repost polling stay blocked until an operator supplies an existing X post ID. Automatic X Live broadcast creation is not available.");
            }

            SetState(XConnectionState.Connected);
            _logger.LogInformation("X connected successfully");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to connect to X");
            LastError = ex.Message;
            SetState(XConnectionState.Error);
            throw;
        }

        return Task.CompletedTask;
    }

    public async Task Disconnect(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Disconnecting from X");

        if (_pollingCts is not null)
        {
            await _pollingCts.CancelAsync();
            _pollingCts.Dispose();
            _pollingCts = null;
        }

        if (_pollingTask is not null)
        {
            try
            {
                await _pollingTask.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }

            _pollingTask = null;
        }

        SetState(XConnectionState.Disconnected);
    }

    public async Task SendMessage(string message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        if (!Connected)
        {
            _logger.LogWarning("Cannot send X reply: not connected");
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.BroadcastTweetId))
        {
            _logger.LogWarning(
                "Cannot send X reply because BroadcastTweetId is not configured. Thiccdal cannot create the X conversation root automatically.");
            return;
        }

        try
        {
            await _apiClient.SendReply(_options.BroadcastTweetId, message, cancellationToken);
            _logger.LogInformation("Posted X reply to tracked conversation {BroadcastTweetId}", _options.BroadcastTweetId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to send X reply to tracked conversation {BroadcastTweetId}", _options.BroadcastTweetId);
            throw new PlatformOperationException("Failed to send X reply.", ex);
        }
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

    internal async Task PollReplies(CancellationToken cancellationToken = default)
    {
        if (!Connected || string.IsNullOrWhiteSpace(_options.BroadcastTweetId))
        {
            return;
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        if (now < _nextReplyPollAt)
        {
            return;
        }

        XReplyPollResult result = await _apiClient.PollReplies(_options.BroadcastTweetId, _lastSeenReplyId, cancellationToken);

        foreach (XTweetReply reply in result.Replies
            .OrderBy(static reply => reply.CreatedAt)
            .ThenBy(static reply => reply.Id, StringComparer.Ordinal))
        {
            ChatEvent chatEvent = XEventMapper.ToChatEvent(reply, GetChannel());
            await PersistAndDispatchEvent(chatEvent, cancellationToken);
            _lastSeenReplyId = GetNewestTweetId(_lastSeenReplyId, reply.Id);
        }

        if (!string.IsNullOrWhiteSpace(result.NewestReplyId))
        {
            _lastSeenReplyId = GetNewestTweetId(_lastSeenReplyId, result.NewestReplyId);
        }

        _nextReplyPollAt = GetNextPollAt(result.RateLimit, now, TimeSpan.FromMilliseconds(_options.PollIntervalMs), "reply polling");
    }

    internal async Task PollEngagements(CancellationToken cancellationToken = default)
    {
        if (!Connected || string.IsNullOrWhiteSpace(_options.BroadcastTweetId))
        {
            return;
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        if (now < _nextEngagementPollAt)
        {
            return;
        }

        XEngagementPollResult likingUsers = await _apiClient.GetLikingUsers(_options.BroadcastTweetId, cancellationToken);
        _likesInitialized = await PublishNewUsers(
            likingUsers.Users,
            _seenLikeUserIds,
            _likesInitialized,
            static (tweetId, user, occurredAt, channel) => XEventMapper.ToLikeEvent(tweetId, user, occurredAt, channel),
            cancellationToken);

        XEngagementPollResult repostingUsers = await _apiClient.GetRepostedUsers(_options.BroadcastTweetId, cancellationToken);
        _repostsInitialized = await PublishNewUsers(
            repostingUsers.Users,
            _seenRepostUserIds,
            _repostsInitialized,
            static (tweetId, user, occurredAt, channel) => XEventMapper.ToRepostEvent(tweetId, user, occurredAt, channel),
            cancellationToken);

        DateTimeOffset likeNextPollAt = GetNextPollAt(
            likingUsers.RateLimit,
            now,
            TimeSpan.FromMilliseconds(_options.LikesPollIntervalMs),
            "like polling");

        DateTimeOffset repostNextPollAt = GetNextPollAt(
            repostingUsers.RateLimit,
            now,
            TimeSpan.FromMilliseconds(_options.LikesPollIntervalMs),
            "repost polling");

        _nextEngagementPollAt = likeNextPollAt > repostNextPollAt ? likeNextPollAt : repostNextPollAt;
    }

    private async Task PollLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await PollReplies(cancellationToken);
                await PollEngagements(cancellationToken);

                TimeSpan delay = GetNextLoopDelay();
                await Task.Delay(delay, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Failed while polling X activity");
                await Task.Delay(TimeSpan.FromMilliseconds(_options.PollIntervalMs), cancellationToken);
            }
        }
    }

    private async Task<bool> PublishNewUsers(
        IReadOnlyList<XUserProfile> users,
        HashSet<string> seenUserIds,
        bool initialized,
        Func<string, XUserProfile, DateTimeOffset, string, PlatformEvent> eventFactory,
        CancellationToken cancellationToken)
    {
        if (!initialized)
        {
            foreach (XUserProfile user in users)
            {
                seenUserIds.Add(user.Id);
            }

            return true;
        }

        DateTimeOffset occurredAt = _timeProvider.GetUtcNow();
        foreach (XUserProfile user in users)
        {
            if (!seenUserIds.Add(user.Id))
            {
                continue;
            }

            PlatformEvent platformEvent = eventFactory(_options.BroadcastTweetId, user, occurredAt, GetChannel());
            await PersistAndDispatchEvent(platformEvent, cancellationToken);
        }

        return true;
    }

    private async Task PersistAndDispatchEvent(PlatformEvent platformEvent, CancellationToken cancellationToken)
    {
        try
        {
            await _eventBus.Publish(platformEvent, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to persist X platform event {EventType}", platformEvent.Type);
        }

        OnPlatformEventReceived?.Invoke(this, platformEvent);

        if (platformEvent is ChatEvent chatEvent)
        {
            OnChatMessageRecieved?.Invoke(this, chatEvent);
        }
    }

    private bool HasCredentials()
    {
        return !string.IsNullOrWhiteSpace(_options.BearerToken)
            || (!string.IsNullOrWhiteSpace(_options.ApiKey)
                && !string.IsNullOrWhiteSpace(_options.ApiKeySecret)
                && !string.IsNullOrWhiteSpace(_options.AccessToken)
                && !string.IsNullOrWhiteSpace(_options.AccessTokenSecret));
    }

    private string GetChannel()
    {
        return string.IsNullOrWhiteSpace(_options.Channel) ? "x" : _options.Channel;
    }

    private TimeSpan GetNextLoopDelay()
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        DateTimeOffset nextPollAt = _nextReplyPollAt < _nextEngagementPollAt ? _nextReplyPollAt : _nextEngagementPollAt;

        if (nextPollAt <= now)
        {
            return TimeSpan.FromMilliseconds(250);
        }

        TimeSpan delay = nextPollAt - now;
        return delay <= TimeSpan.Zero ? TimeSpan.FromMilliseconds(250) : delay;
    }

    private DateTimeOffset GetNextPollAt(XApiRateLimit rateLimit, DateTimeOffset now, TimeSpan defaultInterval, string operationName)
    {
        if (rateLimit.Remaining == 0 && rateLimit.ResetAt is DateTimeOffset resetAt && resetAt > now)
        {
            _logger.LogWarning("X {OperationName} is rate-limited until {ResetAt}", operationName, resetAt);
            return resetAt;
        }

        return now.Add(defaultInterval);
    }

    private void ResetPollingState()
    {
        _lastSeenReplyId = null;
        _nextReplyPollAt = DateTimeOffset.MinValue;
        _nextEngagementPollAt = DateTimeOffset.MinValue;
        _likesInitialized = false;
        _repostsInitialized = false;
        _seenLikeUserIds.Clear();
        _seenRepostUserIds.Clear();
    }

    private void SetState(XConnectionState state)
    {
        lock (_stateLock)
        {
            if (_connectionState == state)
            {
                return;
            }

            _connectionState = state;
            if (state != XConnectionState.Error)
            {
                LastError = null;
            }

            _logger.LogInformation("X connection state: {State}", state);
        }

        ConnectionStateChanged?.Invoke(this, state);
    }

    private void SetStreamLive(bool isLive)
    {
        if (_isStreamLive == isLive)
        {
            return;
        }

        _isStreamLive = isLive;
        _logger.LogInformation("X stream live state: {State}", isLive);
        StreamLiveStateChanged?.Invoke(this, isLive);
    }

    private static string? GetNewestTweetId(string? currentId, string candidateId)
    {
        if (string.IsNullOrWhiteSpace(candidateId))
        {
            return currentId;
        }

        if (string.IsNullOrWhiteSpace(currentId))
        {
            return candidateId;
        }

        if (ulong.TryParse(currentId, out ulong currentValue) && ulong.TryParse(candidateId, out ulong candidateValue))
        {
            return candidateValue > currentValue ? candidateId : currentId;
        }

        return string.CompareOrdinal(candidateId, currentId) > 0 ? candidateId : currentId;
    }

    private static PlatformConnectionState MapState(XConnectionState state)
    {
        return state switch
        {
            XConnectionState.Connected => PlatformConnectionState.Connected,
            XConnectionState.Connecting => PlatformConnectionState.Connecting,
            XConnectionState.Error => PlatformConnectionState.Error,
            _ => PlatformConnectionState.Disconnected
        };
    }
}
