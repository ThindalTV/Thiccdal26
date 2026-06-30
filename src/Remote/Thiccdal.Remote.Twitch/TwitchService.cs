using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Infrastructure.Twitch;

namespace Thiccdal.Remote.Twitch;

public class TwitchService : ITwitchService, IStreamInfoProvider, IChatSource, IAsyncDisposable, IDisposable
{
    private readonly TwitchOptions _options;
    private readonly ITwitchTokenManager _tokenManager;
    private readonly ITwitchTargetChannelService _targetChannelService;
    private readonly ITwitchHelixClient _helixClient;
    private readonly ITwitchEventSubClient _eventSubClient;
    private readonly IEventBus _eventBus;
    private readonly ILogger<TwitchService> _logger;

    private TwitchConnectionState _connectionState = TwitchConnectionState.NotAuthorized;
    private TwitchStreamState _streamState = new();

    public string PlatformName => "Twitch";
    public TwitchConnectionState ConnectionState => _connectionState;
    public PlatformConnectionState State => MapState(_connectionState);
    public string? LastError { get; private set; }
    public bool IsStreamLive => _streamState.IsLive;
    public TwitchStreamState StreamState => _streamState;

    public event EventHandler<TwitchConnectionState>? ConnectionStateChanged;
    public event EventHandler<bool>? StreamLiveStateChanged;
    public event EventHandler<ChatEvent>? OnChatMessageRecieved;
    public event EventHandler<PlatformEvent>? OnPlatformEventReceived;

    public bool Connected => _eventSubClient.Connected && _connectionState == TwitchConnectionState.Connected;

    public TwitchService(
        IOptions<TwitchOptions> options,
        ITwitchTokenManager tokenManager,
        ITwitchTargetChannelService targetChannelService,
        ITwitchHelixClient helixClient,
        ITwitchEventSubClient eventSubClient,
        IEventBus eventBus,
        ILogger<TwitchService> logger)
    {
        _options = options.Value;
        _tokenManager = tokenManager;
        _targetChannelService = targetChannelService;
        _helixClient = helixClient;
        _eventSubClient = eventSubClient;
        _eventBus = eventBus;
        _logger = logger;
        _targetChannelService.ConnectionProfileChanged += OnConnectionProfileChanged;
        _eventSubClient.OnEventReceived += OnEventReceived;
    }

    public async Task RefreshConnectionState(CancellationToken cancellationToken = default)
    {
        if (_connectionState == TwitchConnectionState.Connected || _connectionState == TwitchConnectionState.Connecting)
        {
            return;
        }

        bool hasToken = await _tokenManager.HasToken(cancellationToken);
        SetState(hasToken ? TwitchConnectionState.Authorized : TwitchConnectionState.NotAuthorized);
    }

    public async Task RefreshStreamState(CancellationToken cancellationToken = default)
    {
        TwitchChatConnectionProfile profile = await _targetChannelService.GetConnectionProfile(cancellationToken);
        if (string.IsNullOrWhiteSpace(profile.BroadcasterId))
        {
            SetStreamState(new TwitchStreamState());
            return;
        }

        if (!await _tokenManager.HasToken(cancellationToken))
        {
            SetStreamState(new TwitchStreamState());
            SetState(TwitchConnectionState.NotAuthorized);
            return;
        }

        try
        {
            string? token = await _tokenManager.GetToken(cancellationToken);
            if (string.IsNullOrWhiteSpace(token))
            {
                _logger.LogInformation("Skipping Twitch stream state refresh because no token is stored yet");
                SetState(TwitchConnectionState.NotAuthorized);
                SetStreamState(new TwitchStreamState());
                return;
            }

            TwitchStreamState streamState = await _helixClient.GetStreamState(profile, cancellationToken);
            SetStreamState(streamState);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to refresh Twitch stream state");
        }
    }

    public async Task Connect(CancellationToken cancellationToken = default)
    {
        if (Connected)
        {
            return;
        }

        TwitchChatConnectionProfile profile = await _targetChannelService.GetConnectionProfile(cancellationToken);
        SetState(TwitchConnectionState.Connecting);

        try
        {
            if (!await _tokenManager.HasToken(cancellationToken))
            {
                _logger.LogInformation("Twitch is not authorized yet; skipping EventSub connection");
                SetState(TwitchConnectionState.NotAuthorized);
                return;
            }

            if (string.IsNullOrWhiteSpace(profile.BotUserId))
            {
                _logger.LogWarning("Twitch bot user id is not configured; skipping EventSub connection");
                await RefreshConnectionState(cancellationToken);
                return;
            }

            if (string.IsNullOrWhiteSpace(profile.BroadcasterId) || string.IsNullOrWhiteSpace(profile.TargetChannel))
            {
                _logger.LogWarning("Twitch target broadcaster is not configured; skipping EventSub connection");
                await RefreshConnectionState(cancellationToken);
                return;
            }

            string? token = await _tokenManager.GetToken(cancellationToken);
            if (string.IsNullOrWhiteSpace(token))
            {
                SetState(TwitchConnectionState.NotAuthorized);
                return;
            }

            await _eventSubClient.Connect(profile, cancellationToken);
            SetState(TwitchConnectionState.Connected);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to connect to Twitch EventSub");
            LastError = ex.Message;
            SetState(TwitchConnectionState.Error);
            throw;
        }
    }

    public async Task Disconnect(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Disconnecting from Twitch EventSub");
        await _eventSubClient.Disconnect(cancellationToken);
        SetState(TwitchConnectionState.Disconnected);
    }

    public async Task SendMessage(string message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        TwitchChatConnectionProfile profile = await _targetChannelService.GetConnectionProfile(cancellationToken);
        if (_options.Helix.SendChatMessagesViaHelix && CanSendViaHelix(profile))
        {
            try
            {
                TwitchSendMessageResult helixResult = await _helixClient.SendChatMessage(profile, message, cancellationToken);
                if (helixResult.IsSuccessful)
                {
                    return;
                }

                _logger.LogWarning(
                    "Twitch Helix send did not succeed. Code: {FailureCode}; Message: {FailureMessage}",
                    helixResult.FailureCode,
                    helixResult.FailureMessage);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Failed to send Twitch chat message through Helix");
            }
        }
    }

    public async Task<StreamInfoUpdateResult> UpdateStreamInfo(
        StreamInfoUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        List<string> notices = [];
        bool hasSupportedField = !string.IsNullOrWhiteSpace(request.Title) || !string.IsNullOrWhiteSpace(request.Category);
        bool hasUnsupportedField = request.Tags.Count > 0;

        if (!hasSupportedField && !hasUnsupportedField)
        {
            return CreateUpdateResult(StreamInfoUpdateStatus.Succeeded, "No Twitch stream info changes were requested.");
        }

        if (hasUnsupportedField)
        {
            notices.Add("Tags are not supported by Twitch Helix channel updates.");
        }

        if (!hasSupportedField)
        {
            return CreateUpdateResult(StreamInfoUpdateStatus.Unsupported, string.Join(' ', notices));
        }

        try
        {
            TwitchChatConnectionProfile profile = await _targetChannelService.GetConnectionProfile(cancellationToken);
            await _helixClient.UpdateChannelInfo(profile, request.Title, request.Category, cancellationToken);
            UpdateCachedStreamState(request.Title, request.Category);

            string successMessage = BuildSuccessMessage(
                !string.IsNullOrWhiteSpace(request.Title),
                !string.IsNullOrWhiteSpace(request.Category));

            if (!hasUnsupportedField)
            {
                return CreateUpdateResult(StreamInfoUpdateStatus.Succeeded, successMessage);
            }

            notices.Insert(0, successMessage);
            return CreateUpdateResult(StreamInfoUpdateStatus.PartiallySucceeded, string.Join(' ', notices));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to update Twitch stream info");

            if (notices.Count > 0)
            {
                notices.Insert(0, ex.Message);
                return CreateUpdateResult(StreamInfoUpdateStatus.Failed, string.Join(' ', notices));
            }

            return CreateUpdateResult(StreamInfoUpdateStatus.Failed, ex.Message);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _targetChannelService.ConnectionProfileChanged -= OnConnectionProfileChanged;
        _eventSubClient.OnEventReceived -= OnEventReceived;
        await Disconnect();
        GC.SuppressFinalize(this);
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private void OnConnectionProfileChanged(object? sender, TwitchChatConnectionProfile profile)
    {
        _ = ApplyConnectionProfileChange(profile);
    }

    private void OnEventReceived(object? sender, PlatformEvent platformEvent)
    {
        _ = PersistAndDispatchEvent(platformEvent);
    }

    private async Task PersistAndDispatchEvent(PlatformEvent platformEvent)
    {
        try
        {
            await _eventBus.Publish(platformEvent);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to persist Twitch platform event {EventType}", platformEvent.Type);
        }

        OnPlatformEventReceived?.Invoke(this, platformEvent);
        if (platformEvent is ChatEvent chatEvent)
        {
            OnChatMessageRecieved?.Invoke(this, chatEvent);
        }
    }

    private async Task ApplyConnectionProfileChange(TwitchChatConnectionProfile profile)
    {
        try
        {
            if (!Connected)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(profile.TargetChannel) || string.IsNullOrWhiteSpace(profile.BroadcasterId))
            {
                _logger.LogWarning("Twitch target broadcaster was cleared while connected; disconnecting EventSub session");
                await Disconnect();
                return;
            }

            await _eventSubClient.Connect(profile);
            _logger.LogInformation(
                "Switched Twitch EventSub session to target channel {TargetChannel} ({BroadcasterId})",
                profile.TargetChannel,
                profile.BroadcasterId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to apply Twitch target channel change");
            LastError = ex.Message;
            SetState(TwitchConnectionState.Error);
        }
    }

    private void SetState(TwitchConnectionState state)
    {
        if (_connectionState == state)
        {
            return;
        }

        _connectionState = state;
        if (state != TwitchConnectionState.Error)
        {
            LastError = null;
        }

        _logger.LogInformation("Twitch connection state: {State}", state);
        ConnectionStateChanged?.Invoke(this, state);
    }

    private void SetStreamState(TwitchStreamState streamState)
    {
        ArgumentNullException.ThrowIfNull(streamState);

        if (StreamStateEquals(_streamState, streamState))
        {
            return;
        }

        _streamState = streamState with
        {
            Tags = streamState.Tags
                .Where(static tag => !string.IsNullOrWhiteSpace(tag))
                .ToArray()
        };
        _logger.LogInformation("Twitch stream live state: {State}", _streamState.IsLive);
        StreamLiveStateChanged?.Invoke(this, _streamState.IsLive);
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

    private void UpdateCachedStreamState(string title, string category)
    {
        _streamState = _streamState with
        {
            Title = string.IsNullOrWhiteSpace(title) ? _streamState.Title : title,
            Category = string.IsNullOrWhiteSpace(category) ? _streamState.Category : category
        };
    }

    private static string BuildSuccessMessage(bool updatedTitle, bool updatedCategory)
    {
        return (updatedTitle, updatedCategory) switch
        {
            (true, true) => "Updated Twitch title and category.",
            (true, false) => "Updated Twitch title.",
            (false, true) => "Updated Twitch category.",
            _ => "No Twitch stream info changes were requested."
        };
    }

    private static bool CanSendViaHelix(TwitchChatConnectionProfile? profile)
    {
        return profile is not null &&
               !string.IsNullOrWhiteSpace(profile.BroadcasterId) &&
               !string.IsNullOrWhiteSpace(profile.BotUserId);
    }

    private static bool StreamStateEquals(TwitchStreamState left, TwitchStreamState right)
    {
        return left.IsLive == right.IsLive &&
               string.Equals(left.Title, right.Title, StringComparison.Ordinal) &&
               string.Equals(left.Category, right.Category, StringComparison.Ordinal) &&
               Nullable.Equals(left.StartedAt, right.StartedAt) &&
               left.Tags.SequenceEqual(right.Tags, StringComparer.Ordinal);
    }

    private static PlatformConnectionState MapState(TwitchConnectionState state)
    {
        return state switch
        {
            TwitchConnectionState.Connected => PlatformConnectionState.Connected,
            TwitchConnectionState.Connecting => PlatformConnectionState.Connecting,
            TwitchConnectionState.Error => PlatformConnectionState.Error,
            _ => PlatformConnectionState.Disconnected
        };
    }
}
