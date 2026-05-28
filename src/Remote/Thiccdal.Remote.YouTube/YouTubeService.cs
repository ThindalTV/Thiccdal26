using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Infrastructure.YouTube;

namespace Thiccdal.Remote.YouTube;

public sealed class YouTubeService : IYouTubeService, IStreamInfoProvider, IChatSource, IAsyncDisposable, IDisposable
{
    private readonly YouTubeOptions _options;
    private readonly IYouTubeTokenManager _tokenManager;
    private readonly IYouTubeApiClient _apiClient;
    private readonly YouTubeLiveChatMessageMapper _messageMapper;
    private readonly IEventBus _eventBus;
    private readonly ILogger<YouTubeService> _logger;

    private YouTubeConnectionState _connectionState = YouTubeConnectionState.NotAuthorized;
    private bool _isStreamLive;
    private CancellationTokenSource? _pollingCts;
    private Task? _pollingTask;
    private string? _currentLiveChatId;
    private string? _currentPageToken;
    private YouTubeBroadcastInfo? _activeBroadcast;

    public string PlatformName => "YouTube";
    public YouTubeConnectionState ConnectionState => _connectionState;
    public PlatformConnectionState State => MapState(_connectionState);
    public string? LastError { get; private set; }
    public bool IsStreamLive => _isStreamLive;
    public YouTubeBroadcastInfo? ActiveBroadcast => _activeBroadcast;

    public event EventHandler<YouTubeConnectionState>? ConnectionStateChanged;
    public event EventHandler<bool>? StreamLiveStateChanged;
    public event EventHandler<ChatEvent>? OnChatMessageRecieved;
    public event EventHandler<PlatformEvent>? OnPlatformEventReceived;

    public bool Connected => _connectionState == YouTubeConnectionState.Connected;

    public YouTubeService(
        IOptions<YouTubeOptions> options,
        IYouTubeTokenManager tokenManager,
        IYouTubeApiClient apiClient,
        YouTubeLiveChatMessageMapper messageMapper,
        IEventBus eventBus,
        ILogger<YouTubeService> logger)
    {
        _options = options.Value;
        _tokenManager = tokenManager;
        _apiClient = apiClient;
        _messageMapper = messageMapper;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task RefreshConnectionState(CancellationToken cancellationToken = default)
    {
        if (_connectionState == YouTubeConnectionState.Connected || _connectionState == YouTubeConnectionState.Connecting)
        {
            return;
        }

        bool hasToken = await _tokenManager.HasToken(cancellationToken);
        SetState(hasToken ? YouTubeConnectionState.Authorized : YouTubeConnectionState.NotAuthorized);
    }

    public async Task RefreshStreamState(CancellationToken cancellationToken = default)
    {
        if (!await _tokenManager.HasToken(cancellationToken))
        {
            SetActiveBroadcast(null);
            SetState(YouTubeConnectionState.NotAuthorized);
            return;
        }

        try
        {
            YouTubeBroadcastInfo? broadcast = await _apiClient.GetActiveBroadcast(cancellationToken);
            SetActiveBroadcast(broadcast);
            if (broadcast is null)
            {
                LastError = "No active YouTube broadcast with live chat was found.";
                SetState(YouTubeConnectionState.Error);
                return;
            }

            SetState(YouTubeConnectionState.Authorized);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to refresh YouTube stream state");
            LastError = ex.Message;
            SetState(YouTubeConnectionState.Error);
        }
    }

    public async Task Connect(CancellationToken cancellationToken = default)
    {
        if (Connected)
        {
            return;
        }

        SetState(YouTubeConnectionState.Connecting);

        try
        {
            if (!await _tokenManager.HasToken(cancellationToken))
            {
                _logger.LogInformation("YouTube is not authorized yet; skipping live chat connection");
                SetState(YouTubeConnectionState.NotAuthorized);
                return;
            }

            var broadcast = await _apiClient.GetActiveBroadcast(cancellationToken);
            if (broadcast is null || string.IsNullOrWhiteSpace(broadcast.LiveChatId))
            {
                _logger.LogWarning("No active YouTube broadcast with live chat found");
                SetActiveBroadcast(null);
                LastError = "No active YouTube broadcast with live chat was found.";
                SetState(YouTubeConnectionState.Error);
                return;
            }

            SetActiveBroadcast(broadcast);
            _currentLiveChatId = broadcast.LiveChatId;
            _currentPageToken = null;

            SetState(YouTubeConnectionState.Connected);
            _pollingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _pollingTask = PollLiveChatLoop(_pollingCts.Token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to connect to YouTube live chat");
            LastError = ex.Message;
            SetState(YouTubeConnectionState.Error);
            throw;
        }
    }

    public async Task Disconnect(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Disconnecting from YouTube live chat");

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
                await _pollingTask;
            }
            catch (OperationCanceledException)
            {
            }
            _pollingTask = null;
        }

        _currentLiveChatId = null;
        _currentPageToken = null;
        SetState(YouTubeConnectionState.Disconnected);
    }

    public Task SendMessage(string message, CancellationToken cancellationToken = default)
    {
        return SendMessageCore(message, cancellationToken);
    }

    public async Task SetTitle(string title, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return;
        }

        YouTubeBroadcastInfo broadcast = await EnsureActiveBroadcast(cancellationToken);

        try
        {
            await _apiClient.UpdateBroadcastInfo(
                broadcast.BroadcastId,
                title,
                broadcast.Description,
                cancellationToken);
            SetActiveBroadcast(broadcast with { Title = title });
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not PlatformOperationException)
        {
            _logger.LogError(ex, "Failed to update YouTube broadcast title");
            throw new PlatformOperationException("YouTube title update failed.", ex);
        }
    }

    public async Task SetDescription(string description, CancellationToken cancellationToken = default)
    {
        YouTubeBroadcastInfo broadcast = await EnsureActiveBroadcast(cancellationToken);

        try
        {
            await _apiClient.UpdateBroadcastInfo(
                broadcast.BroadcastId,
                broadcast.Title,
                description,
                cancellationToken);
            SetActiveBroadcast(broadcast with { Description = description });
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not PlatformOperationException)
        {
            _logger.LogError(ex, "Failed to update YouTube broadcast description");
            throw new PlatformOperationException("YouTube description update failed.", ex);
        }
    }

    public Task SetCategory(string category, CancellationToken cancellationToken = default)
    {
        string message = "YouTube category updates are not supported by the current live broadcast API.";
        _logger.LogWarning(
            "YouTube does not support category updates through the current live broadcast API; requested category {Category} was rejected",
            category);
        return Task.FromException(new PlatformOperationException(message));
    }

    public async Task<StreamInfoUpdateResult> UpdateStreamInfo(
        StreamInfoUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        List<string> notices = [];
        bool updatedTitle = false;

        if (string.IsNullOrWhiteSpace(request.Title) &&
            string.IsNullOrWhiteSpace(request.Category) &&
            request.Tags.Count == 0)
        {
            return CreateUpdateResult(StreamInfoUpdateStatus.Succeeded, "No YouTube stream info changes were requested.");
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(request.Title))
            {
                await SetTitle(request.Title, cancellationToken);
                updatedTitle = true;
            }

            if (!string.IsNullOrWhiteSpace(request.Category))
            {
                notices.Add("Category updates are not supported by the current YouTube live broadcast API.");
            }

            if (request.Tags.Count > 0)
            {
                notices.Add("Tag updates are not supported by the current YouTube live broadcast API.");
            }

            if (updatedTitle && notices.Count == 0)
            {
                return CreateUpdateResult(StreamInfoUpdateStatus.Succeeded, "Updated YouTube title.");
            }

            if (updatedTitle)
            {
                notices.Insert(0, "Updated YouTube title.");
                return CreateUpdateResult(StreamInfoUpdateStatus.PartiallySucceeded, string.Join(' ', notices));
            }

            return CreateUpdateResult(StreamInfoUpdateStatus.Unsupported, string.Join(' ', notices));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to update YouTube stream info");
            return CreateUpdateResult(StreamInfoUpdateStatus.Failed, ex.Message);
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

    private async Task PollLiveChatLoop(CancellationToken cancellationToken)
    {
        int pollingIntervalMillis = _options.LiveChatPollingIntervalSeconds * 1000;

        while (!cancellationToken.IsCancellationRequested && _currentLiveChatId is not null)
        {
            try
            {
                var result = await _apiClient.PollLiveChat(_currentLiveChatId, _currentPageToken, cancellationToken);
                _currentPageToken = result.NextPageToken;
                SetState(YouTubeConnectionState.Connected);

                if (result.PollingIntervalMillis > 0)
                {
                    pollingIntervalMillis = result.PollingIntervalMillis;
                }

                var events = _messageMapper.MapMessages(result.RawJson, _options.DefaultChannelId);
                foreach (var platformEvent in events)
                {
                    await PersistAndDispatchEvent(platformEvent);
                }

                await Task.Delay(pollingIntervalMillis, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "YouTube live chat polling encountered an error");
                LastError = ex.Message;
                SetState(YouTubeConnectionState.Error);
                await Task.Delay(pollingIntervalMillis, cancellationToken);
            }
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
            _logger.LogWarning(ex, "Failed to persist YouTube platform event {EventType}", platformEvent.Type);
        }

        OnPlatformEventReceived?.Invoke(this, platformEvent);
        if (platformEvent is ChatEvent chatEvent)
        {
            OnChatMessageRecieved?.Invoke(this, chatEvent);
        }
    }

    private void SetState(YouTubeConnectionState state)
    {
        if (_connectionState == state)
        {
            return;
        }

        _connectionState = state;
        if (state != YouTubeConnectionState.Error)
        {
            LastError = null;
        }

        _logger.LogInformation("YouTube connection state: {State}", state);
        ConnectionStateChanged?.Invoke(this, state);
    }

    private void SetStreamLive(bool isLive)
    {
        if (_isStreamLive == isLive)
        {
            return;
        }

        _isStreamLive = isLive;
        _logger.LogInformation("YouTube stream live state: {State}", isLive);
        StreamLiveStateChanged?.Invoke(this, isLive);
    }

    private async Task SendMessageCore(string message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        if (!Connected)
        {
            _logger.LogWarning("Cannot send YouTube message: live chat is not connected");
            return;
        }

        string liveChatId = await ResolveLiveChatId(cancellationToken);
        if (string.IsNullOrWhiteSpace(liveChatId))
        {
            throw new InvalidOperationException("Cannot send a YouTube message without an active live chat.");
        }

        await _apiClient.SendLiveChatMessage(liveChatId, message, cancellationToken);
        _logger.LogDebug("Sent YouTube message to live chat {LiveChatId}", liveChatId);
    }

    private async Task<YouTubeBroadcastInfo> EnsureActiveBroadcast(CancellationToken cancellationToken)
    {
        YouTubeBroadcastInfo? broadcast = _activeBroadcast ?? await _apiClient.GetActiveBroadcast(cancellationToken);
        if (broadcast is null || string.IsNullOrWhiteSpace(broadcast.BroadcastId))
        {
            throw new InvalidOperationException("No active YouTube broadcast is available.");
        }

        SetActiveBroadcast(broadcast);
        return broadcast;
    }

    private static PlatformConnectionState MapState(YouTubeConnectionState state)
    {
        return state switch
        {
            YouTubeConnectionState.Connected => PlatformConnectionState.Connected,
            YouTubeConnectionState.Connecting => PlatformConnectionState.Connecting,
            YouTubeConnectionState.Error => PlatformConnectionState.Error,
            _ => PlatformConnectionState.Disconnected
        };
    }

    private async Task<string> ResolveLiveChatId(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_currentLiveChatId))
        {
            return _currentLiveChatId;
        }

        YouTubeBroadcastInfo? broadcast = await _apiClient.GetActiveBroadcast(cancellationToken);
        if (broadcast is null)
        {
            return string.Empty;
        }

        _currentLiveChatId = broadcast.LiveChatId;
        SetActiveBroadcast(broadcast);
        return _currentLiveChatId ?? string.Empty;
    }

    private void SetActiveBroadcast(YouTubeBroadcastInfo? broadcast)
    {
        _activeBroadcast = broadcast is null
            ? null
            : broadcast with
            {
                Tags = broadcast.Tags
                    .Where(static tag => !string.IsNullOrWhiteSpace(tag))
                    .ToArray()
            };
        SetStreamLive(_activeBroadcast?.IsLive ?? false);
    }

    private StreamInfoUpdateResult CreateUpdateResult(StreamInfoUpdateStatus status, string message)
    {
        return new StreamInfoUpdateResult
        {
            PlatformName = PlatformName,
            Status = status,
            Message = message
        };
    }
}
