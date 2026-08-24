using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.Streaming;

namespace Thiccdal.Remote.Obs;

/// <summary>
/// Keeps an obs-websocket v5 session open against OBS Studio and tracks stream output state.
/// </summary>
public sealed class ObsWebSocketConnection : IObsConnection, IAsyncDisposable
{
    private const int ReceiveBufferSize = 8192;
    private const string StreamStateChangedEvent = "StreamStateChanged";
    private const string GetStreamStatusRequest = "GetStreamStatus";

    private readonly ObsOptions _options;
    private readonly ILogger<ObsWebSocketConnection> _logger;
    private readonly Lock _stateLock = new();

    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _sessionCts;
    private Task? _sessionTask;
    private ObsState _state;
    private bool _disposed;

    public ObsWebSocketConnection(IOptions<ObsOptions> options, ILogger<ObsWebSocketConnection> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options.Value;
        _logger = logger;
        _state = new ObsState { IsEnabled = _options.Enabled };
    }

    public event EventHandler? StateChanged;

    public ObsState GetState()
    {
        lock (_stateLock)
        {
            return _state;
        }
    }

    public Task Connect(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_options.Enabled)
        {
            _logger.LogInformation("OBS integration is disabled; not connecting to obs-websocket.");
            return Task.CompletedTask;
        }

        if (_sessionTask is not null)
        {
            return Task.CompletedTask;
        }

        _sessionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _sessionTask = RunSession(_sessionCts.Token);

        return Task.CompletedTask;
    }

    public async Task Disconnect(CancellationToken cancellationToken = default)
    {
        if (_sessionCts is null)
        {
            return;
        }

        await _sessionCts.CancelAsync();

        if (_sessionTask is not null)
        {
            try
            {
                await _sessionTask.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // The session loop always ends in cancellation; nothing to surface.
            }
        }

        _sessionCts.Dispose();
        _sessionCts = null;
        _sessionTask = null;

        UpdateState(static state => state with { IsConnected = false, IsStreaming = false });
    }

    /// <summary>
    /// Owns the whole connection lifetime: connect, pump messages, back off, repeat until cancelled.
    /// </summary>
    private async Task RunSession(CancellationToken cancellationToken)
    {
        TimeSpan reconnectDelay = TimeSpan.FromSeconds(_options.InitialReconnectDelaySeconds);
        TimeSpan maxReconnectDelay = TimeSpan.FromSeconds(_options.MaxReconnectDelaySeconds);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await OpenSession(cancellationToken);
                reconnectDelay = TimeSpan.FromSeconds(_options.InitialReconnectDelaySeconds);

                await ReceiveLoop(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (ex is WebSocketException or InvalidOperationException or JsonException)
            {
                _logger.LogWarning(
                    ex,
                    "OBS connection to {Host}:{Port} failed; retrying in {DelaySeconds}s",
                    _options.Host,
                    _options.Port,
                    reconnectDelay.TotalSeconds);

                string error = ex.Message;
                UpdateState(state => state with { IsConnected = false, IsStreaming = false, LastError = error });
            }
            finally
            {
                CleanupWebSocket();
            }

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            UpdateState(static state => state with { IsConnected = false, IsStreaming = false });

            try
            {
                await Task.Delay(reconnectDelay, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            reconnectDelay = TimeSpan.FromTicks(Math.Min(reconnectDelay.Ticks * 2, maxReconnectDelay.Ticks));
        }

        UpdateState(static state => state with { IsConnected = false, IsStreaming = false });
    }

    private async Task OpenSession(CancellationToken cancellationToken)
    {
        _webSocket = new ClientWebSocket();

        Uri uri = new Uri($"ws://{_options.Host}:{_options.Port}");
        await _webSocket.ConnectAsync(uri, cancellationToken);

        ObsHelloData helloData = await Handshake(cancellationToken);
        await Identify(helloData, cancellationToken);

        _logger.LogInformation(
            "Connected to obs-websocket {Version} at {Host}:{Port}",
            helloData.ObsWebSocketVersion,
            _options.Host,
            _options.Port);

        UpdateState(static state => state with { IsConnected = true, LastError = null });

        await RequestStreamStatus(cancellationToken);
    }

    private async Task<ObsHelloData> Handshake(CancellationToken cancellationToken)
    {
        string helloJson = await ReceiveMessage(cancellationToken)
            ?? throw new InvalidOperationException("OBS closed the connection before sending Hello.");

        ObsMessage? hello = JsonSerializer.Deserialize<ObsMessage>(helloJson);
        if (hello?.Op != ObsOpCode.Hello)
        {
            throw new InvalidOperationException($"Expected Hello from OBS, received opcode {hello?.Op}.");
        }

        return hello.D?.Deserialize<ObsHelloData>()
            ?? throw new InvalidOperationException("OBS sent a Hello message with no payload.");
    }

    private async Task Identify(ObsHelloData helloData, CancellationToken cancellationToken)
    {
        ObsIdentifyData identifyData = new ObsIdentifyData
        {
            RpcVersion = helloData.RpcVersion
        };

        if (helloData.Authentication is not null)
        {
            if (string.IsNullOrEmpty(_options.Password))
            {
                throw new InvalidOperationException(
                    "OBS requires an obs-websocket password but none is configured. Set the Obs:Password setting.");
            }

            identifyData.Authentication = BuildAuthenticationString(
                _options.Password,
                helloData.Authentication.Salt,
                helloData.Authentication.Challenge);
        }

        await SendMessage(
            new ObsMessage
            {
                Op = ObsOpCode.Identify,
                D = JsonSerializer.SerializeToElement(identifyData)
            },
            cancellationToken);

        string identifiedJson = await ReceiveMessage(cancellationToken)
            ?? throw new InvalidOperationException("OBS closed the connection before confirming identification.");

        ObsMessage? identified = JsonSerializer.Deserialize<ObsMessage>(identifiedJson);
        if (identified?.Op != ObsOpCode.Identified)
        {
            throw new InvalidOperationException($"Expected Identified from OBS, received opcode {identified?.Op}.");
        }
    }

    /// <summary>
    /// Asks OBS whether it is already streaming, so a mid-stream restart of Thiccdal reports the
    /// truth instead of waiting for the next state transition.
    /// </summary>
    private async Task RequestStreamStatus(CancellationToken cancellationToken)
    {
        string requestId = Guid.NewGuid().ToString("N");

        await SendMessage(
            new ObsMessage
            {
                Op = ObsOpCode.Request,
                D = JsonSerializer.SerializeToElement(
                    new ObsRequestData
                    {
                        RequestType = GetStreamStatusRequest,
                        RequestId = requestId
                    })
            },
            cancellationToken);

        // OBS interleaves events with request responses, so keep reading until the answer arrives.
        while (!cancellationToken.IsCancellationRequested)
        {
            string json = await ReceiveMessage(cancellationToken)
                ?? throw new InvalidOperationException("OBS closed the connection before answering GetStreamStatus.");

            ObsMessage? message = JsonSerializer.Deserialize<ObsMessage>(json);
            if (message?.Op != ObsOpCode.RequestResponse)
            {
                ProcessMessage(message);
                continue;
            }

            ObsRequestResponseData? response = message.D?.Deserialize<ObsRequestResponseData>();
            if (response is null || !string.Equals(response.RequestId, requestId, StringComparison.Ordinal))
            {
                continue;
            }

            bool outputActive = response.ResponseData?.Deserialize<ObsStreamStatusData>()?.OutputActive ?? false;
            UpdateState(state => state with { IsStreaming = outputActive });
            return;
        }
    }

    private async Task ReceiveLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _webSocket?.State == WebSocketState.Open)
        {
            string json = await ReceiveMessage(cancellationToken)
                ?? throw new InvalidOperationException("OBS closed the obs-websocket connection.");

            ProcessMessage(JsonSerializer.Deserialize<ObsMessage>(json));
        }
    }

    private void ProcessMessage(ObsMessage? message)
    {
        if (message?.Op != ObsOpCode.Event)
        {
            return;
        }

        ObsEventData? eventData = message.D?.Deserialize<ObsEventData>();
        if (eventData is null || !string.Equals(eventData.EventType, StreamStateChangedEvent, StringComparison.Ordinal))
        {
            return;
        }

        ObsStreamStateChangedData? streamState = eventData.EventData?.Deserialize<ObsStreamStateChangedData>();
        if (streamState is null)
        {
            return;
        }

        _logger.LogInformation("OBS stream output state changed to {OutputState}", streamState.OutputState);

        bool outputActive = streamState.OutputActive;
        UpdateState(state => state with { IsStreaming = outputActive });
    }

    private async Task<string?> ReceiveMessage(CancellationToken cancellationToken)
    {
        if (_webSocket is null)
        {
            return null;
        }

        byte[] buffer = new byte[ReceiveBufferSize];
        StringBuilder messageBuilder = new StringBuilder();
        WebSocketReceiveResult result;

        do
        {
            result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            messageBuilder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
        }
        while (!result.EndOfMessage);

        return messageBuilder.ToString();
    }

    private async Task SendMessage(ObsMessage message, CancellationToken cancellationToken)
    {
        if (_webSocket is null || _webSocket.State != WebSocketState.Open)
        {
            throw new InvalidOperationException("The obs-websocket connection is not open.");
        }

        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(message);
        await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);
    }

    /// <summary>
    /// Implements the obs-websocket v5 challenge:
    /// base64(sha256(base64(sha256(password + salt)) + challenge)).
    /// </summary>
    internal static string BuildAuthenticationString(string password, string salt, string challenge)
    {
        byte[] passwordSaltHash = SHA256.HashData(Encoding.UTF8.GetBytes(password + salt));
        string secret = Convert.ToBase64String(passwordSaltHash);

        byte[] secretChallengeHash = SHA256.HashData(Encoding.UTF8.GetBytes(secret + challenge));
        return Convert.ToBase64String(secretChallengeHash);
    }

    private void UpdateState(Func<ObsState, ObsState> transform)
    {
        bool changed;

        lock (_stateLock)
        {
            ObsState updated = transform(_state);
            changed = updated != _state;
            _state = updated;
        }

        if (changed)
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void CleanupWebSocket()
    {
        _webSocket?.Dispose();
        _webSocket = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await Disconnect(CancellationToken.None);
        CleanupWebSocket();
    }
}
