using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Infrastructure.Facebook;
using Thiccdal.Infrastructure.Remotes;

namespace Thiccdal.Remote.Facebook;

public class FacebookService : IFacebookService, IAsyncDisposable, IDisposable
{
    private readonly FacebookOptions _options;
    private readonly IFacebookGraphClient _graphClient;
    private readonly IEventBus _eventBus;
    private readonly ILogger<FacebookService> _logger;
    private readonly IServiceScopeFactory? _serviceScopeFactory;

    private readonly object _stateLock = new();
    private readonly HashSet<string> _seenCommentIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _seenReactionIds = new(StringComparer.Ordinal);

    private FacebookConnectionState _connectionState = FacebookConnectionState.NotAuthorized;
    private bool _isStreamLive;
    private CancellationTokenSource? _pollingCts;
    private Task? _pollingTask;
    private DateTimeOffset? _lastCommentTimestamp;
    private string? _liveVideoId;
    private bool _followEventWarningLogged;

    public string PlatformName => "Facebook";
    public FacebookConnectionState ConnectionState => _connectionState;
    public PlatformConnectionState State => MapState(_connectionState);
    public string? LastError { get; private set; }
    public bool IsStreamLive => _isStreamLive;
    public bool Connected => _connectionState == FacebookConnectionState.Connected;
    public string? LiveVideoId => _liveVideoId;

    public event EventHandler<FacebookConnectionState>? ConnectionStateChanged;
    public event EventHandler<bool>? StreamLiveStateChanged;
    public event EventHandler<ChatEvent>? OnChatMessageReceived;
    public event EventHandler<PlatformEvent>? OnPlatformEventReceived;

    public FacebookService(
        IOptions<FacebookOptions> options,
        IFacebookGraphClient graphClient,
        IEventBus eventBus,
        ILogger<FacebookService> logger,
        IServiceScopeFactory? serviceScopeFactory = null)
    {
        _options = options.Value;
        _graphClient = graphClient;
        _eventBus = eventBus;
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
    }

    public Task RefreshConnectionState(CancellationToken cancellationToken = default)
    {
        if (_connectionState == FacebookConnectionState.Connected || _connectionState == FacebookConnectionState.Connecting)
        {
            return Task.CompletedTask;
        }

        bool hasToken = !string.IsNullOrWhiteSpace(_options.PageAccessToken) &&
            !string.IsNullOrWhiteSpace(_options.PageId);

        SetState(hasToken ? FacebookConnectionState.Authorized : FacebookConnectionState.NotAuthorized);
        return Task.CompletedTask;
    }

    public async Task RefreshStreamState(CancellationToken cancellationToken = default)
    {
        if (!HasRequiredConfiguration())
        {
            SetStreamLive(false);
            return;
        }

        try
        {
            FacebookLiveVideo? liveVideo = await _graphClient.GetActiveLiveVideo(
                _options.PageId,
                _options.PageAccessToken,
                cancellationToken);

            if (liveVideo is null)
            {
                SetStreamLive(false);
                return;
            }

            _liveVideoId = liveVideo.Id;
            SetStreamLive(true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to refresh Facebook stream state");
        }
    }

    public async Task Connect(CancellationToken cancellationToken = default)
    {
        if (Connected)
        {
            _logger.LogInformation("Facebook is already connected");
            return;
        }

        if (!HasRequiredConfiguration())
        {
            LogMissingConfiguration();
            SetState(FacebookConnectionState.NotAuthorized);
            return;
        }

        SetState(FacebookConnectionState.Connecting);

        try
        {
            if (string.IsNullOrWhiteSpace(_liveVideoId))
            {
                FacebookLiveVideo? liveVideo = await _graphClient.GetActiveLiveVideo(
                    _options.PageId,
                    _options.PageAccessToken,
                    cancellationToken);

                if (liveVideo is null)
                {
                    _logger.LogWarning(
                        "No active Facebook live video was found for page {PageId}; chat polling cannot start",
                        _options.PageId);
                    SetStreamLive(false);
                    LastError = "No active Facebook live video was found.";
                    SetState(FacebookConnectionState.Error);
                    return;
                }

                _liveVideoId = liveVideo.Id;
            }

            SetStreamLive(true);
            await StartPolling(cancellationToken);
            LogFollowerEventBlockOnce();
            SetState(FacebookConnectionState.Connected);
            _logger.LogInformation("Facebook connected successfully to live video {LiveVideoId}", _liveVideoId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to connect to Facebook");
            LastError = ex.Message;
            SetState(FacebookConnectionState.Error);
            throw;
        }
    }

    public async Task Disconnect(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Disconnecting from Facebook");

        await StopPolling();
        SetState(FacebookConnectionState.Disconnected);
    }

    public async Task<string> StartRelay(string title, string description, CancellationToken cancellationToken = default)
    {
        EnsureGraphConfiguration();

        try
        {
            FacebookLiveVideo liveVideo = await _graphClient.CreateLiveVideo(
                _options.PageId,
                _options.PageAccessToken,
                title,
                description,
                _options.DefaultPrivacy,
                cancellationToken);

            string relayUrl = string.IsNullOrWhiteSpace(liveVideo.SecureStreamUrl)
                ? liveVideo.StreamUrl
                : liveVideo.SecureStreamUrl;

            if (string.IsNullOrWhiteSpace(liveVideo.Id) || string.IsNullOrWhiteSpace(relayUrl))
            {
                throw new InvalidOperationException("Facebook Graph API did not return a live video id and relay URL.");
            }

            _liveVideoId = liveVideo.Id;
            _lastCommentTimestamp = null;
            _seenCommentIds.Clear();
            _seenReactionIds.Clear();
            SetStreamLive(true);

            _logger.LogInformation(
                "Facebook Live relay created for page {PageId}. Relay URL: {RelayUrl}",
                _options.PageId,
                RedactRelayUrl(relayUrl));

            return relayUrl;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to create Facebook Live video");
            throw new PlatformOperationException("Facebook Live relay creation failed.", ex);
        }
    }

    public async Task StopRelay(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_liveVideoId))
        {
            _logger.LogWarning("Facebook StopRelay ignored because no active LiveVideoId is available");
            return;
        }

        try
        {
            await _graphClient.EndLiveVideo(_liveVideoId, _options.PageAccessToken, cancellationToken);
            await StopPolling();

            _liveVideoId = null;
            _lastCommentTimestamp = null;
            _seenCommentIds.Clear();
            _seenReactionIds.Clear();

            SetStreamLive(false);
            SetState(FacebookConnectionState.Disconnected);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to stop Facebook Live video {LiveVideoId}", _liveVideoId);
            throw new PlatformOperationException("Facebook Live relay shutdown failed.", ex);
        }
    }

    public async Task SendMessage(string message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        string liveVideoId = EnsureLiveVideoId();

        try
        {
            await _graphClient.PostComment(liveVideoId, _options.PageAccessToken, message, cancellationToken);
            _logger.LogInformation("Posted outbound Facebook comment to live video {LiveVideoId}", liveVideoId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to post Facebook Live comment");
            throw new PlatformOperationException("Facebook comment posting failed.", ex);
        }
    }

    public async Task SetTitle(string title, CancellationToken cancellationToken = default)
    {
        string liveVideoId = EnsureLiveVideoId();

        try
        {
            await _graphClient.UpdateLiveVideo(liveVideoId, _options.PageAccessToken, title, null, cancellationToken);
            _logger.LogInformation("Updated Facebook live video {LiveVideoId} title", liveVideoId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to update Facebook live video title");
            throw new PlatformOperationException("Facebook title update failed.", ex);
        }
    }

    public async Task SetDescription(string description, CancellationToken cancellationToken = default)
    {
        string liveVideoId = EnsureLiveVideoId();

        try
        {
            await _graphClient.UpdateLiveVideo(liveVideoId, _options.PageAccessToken, null, description, cancellationToken);
            _logger.LogInformation("Updated Facebook live video {LiveVideoId} description", liveVideoId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to update Facebook live video description");
            throw new PlatformOperationException("Facebook description update failed.", ex);
        }
    }

    public Task SetCategory(string category, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "Facebook does not expose a live-video category field through the current integration; requested category {Category} was ignored",
            category);
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await StopPolling();
        GC.SuppressFinalize(this);
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private async Task StartPolling(CancellationToken cancellationToken)
    {
        await StopPolling();

        _pollingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _pollingTask = PollLoop(_pollingCts.Token);
    }

    private async Task StopPolling()
    {
        CancellationTokenSource? pollingCts = _pollingCts;
        Task? pollingTask = _pollingTask;

        _pollingCts = null;
        _pollingTask = null;

        if (pollingCts is not null)
        {
            await pollingCts.CancelAsync();
            pollingCts.Dispose();
        }

        if (pollingTask is null)
        {
            return;
        }

        try
        {
            await pollingTask;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task PollLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && !string.IsNullOrWhiteSpace(_liveVideoId))
        {
            try
            {
                await PollComments(cancellationToken);
                await PollReactions(cancellationToken);
                await Task.Delay(_options.PollIntervalMs, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to poll Facebook live activity");
                await Task.Delay(_options.PollIntervalMs, cancellationToken);
            }
        }
    }

    private async Task PollComments(CancellationToken cancellationToken)
    {
        string liveVideoId = EnsureLiveVideoId();
        IReadOnlyList<FacebookComment> comments = await _graphClient.GetComments(
            liveVideoId,
            _options.PageAccessToken,
            _lastCommentTimestamp,
            cancellationToken);

        foreach (FacebookComment comment in comments
            .OrderBy(static comment => FacebookEventMapper.ParseCreatedTime(comment.CreatedTime)))
        {
            if (!_seenCommentIds.Add(comment.Id))
            {
                continue;
            }

            DateTimeOffset occurredAt = FacebookEventMapper.ParseCreatedTime(comment.CreatedTime);
            _lastCommentTimestamp = !_lastCommentTimestamp.HasValue || occurredAt > _lastCommentTimestamp.Value
                ? occurredAt
                : _lastCommentTimestamp;

            await UpsertPlatformUser(comment, occurredAt.UtcDateTime, cancellationToken);
            await PersistAndDispatchEvent(FacebookEventMapper.ToChatEvent(comment, liveVideoId));
        }
    }

    private async Task PollReactions(CancellationToken cancellationToken)
    {
        string liveVideoId = EnsureLiveVideoId();
        IReadOnlyList<FacebookReaction> reactions = await _graphClient.GetReactions(
            liveVideoId,
            _options.PageAccessToken,
            cancellationToken);

        foreach (FacebookReaction reaction in reactions)
        {
            string reactionKey = $"{reaction.Id}:{reaction.Type}";
            if (!_seenReactionIds.Add(reactionKey))
            {
                continue;
            }

            await PersistAndDispatchEvent(FacebookEventMapper.ToReactionEvent(reaction, liveVideoId));
        }
    }

    private async Task UpsertPlatformUser(
        FacebookComment comment,
        DateTime lastSeen,
        CancellationToken cancellationToken)
    {
        if (_serviceScopeFactory is null ||
            string.IsNullOrWhiteSpace(comment.From.Id) ||
            string.IsNullOrWhiteSpace(comment.From.Name))
        {
            return;
        }

        try
        {
            using IServiceScope scope = _serviceScopeFactory.CreateScope();
            IPlatformUserService platformUserService = scope.ServiceProvider.GetRequiredService<IPlatformUserService>();

            await platformUserService.Upsert(
                PlatformEventSource.Facebook,
                comment.From.Id,
                comment.From.Name,
                lastSeen,
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to upsert Facebook platform user {PlatformUserId}", comment.From.Id);
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
            _logger.LogWarning(ex, "Failed to persist Facebook platform event {EventType}", platformEvent.Type);
        }

        OnPlatformEventReceived?.Invoke(this, platformEvent);

        if (platformEvent is ChatEvent chatEvent)
        {
            OnChatMessageReceived?.Invoke(this, chatEvent);
        }
    }

    private bool HasRequiredConfiguration()
    {
        return !string.IsNullOrWhiteSpace(_options.PageAccessToken) &&
            !string.IsNullOrWhiteSpace(_options.PageId);
    }

    private void EnsureGraphConfiguration()
    {
        if (HasRequiredConfiguration())
        {
            return;
        }

        throw new InvalidOperationException(
            "Facebook PageId and PageAccessToken must be configured before creating or updating a live video.");
    }

    private string EnsureLiveVideoId()
    {
        EnsureGraphConfiguration();

        if (!string.IsNullOrWhiteSpace(_liveVideoId))
        {
            return _liveVideoId;
        }

        throw new InvalidOperationException(
            "Facebook LiveVideoId is not set. StartRelay or Connect to an active live video before performing this operation.");
    }

    private void LogMissingConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_options.PageAccessToken))
        {
            _logger.LogWarning("Facebook page access token is not configured; cannot connect");
        }

        if (string.IsNullOrWhiteSpace(_options.PageId))
        {
            _logger.LogWarning("Facebook page ID is not configured; cannot connect");
        }
    }

    private void LogFollowerEventBlockOnce()
    {
        if (_followEventWarningLogged)
        {
            return;
        }

        _followEventWarningLogged = true;
        _logger.LogWarning(
            "Facebook follower events are not emitted during live polling because the Graph API requires Page webhook subscriptions that are not wired in this phase");
    }

    private static string RedactRelayUrl(string relayUrl)
    {
        int lastSlashIndex = relayUrl.LastIndexOf('/');
        return lastSlashIndex < 0
            ? relayUrl
            : relayUrl[..(lastSlashIndex + 1)];
    }

    private void SetState(FacebookConnectionState state)
    {
        lock (_stateLock)
        {
            if (_connectionState == state)
            {
                return;
            }

            _connectionState = state;
            if (state != FacebookConnectionState.Error)
            {
                LastError = null;
            }

            _logger.LogInformation("Facebook connection state: {State}", state);
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
        _logger.LogInformation("Facebook stream live state: {State}", isLive);
        StreamLiveStateChanged?.Invoke(this, isLive);
    }

    private static PlatformConnectionState MapState(FacebookConnectionState state)
    {
        return state switch
        {
            FacebookConnectionState.Connected => PlatformConnectionState.Connected,
            FacebookConnectionState.Connecting => PlatformConnectionState.Connecting,
            FacebookConnectionState.Error => PlatformConnectionState.Error,
            _ => PlatformConnectionState.Disconnected
        };
    }
}
