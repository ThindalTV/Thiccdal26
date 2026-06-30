using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Thiccdal.Teleprompter.Display;

public sealed class ObsWebSocketClient : IDisposable, IAsyncDisposable
{
    private const int ReceiveBufferSize = 8192;
    private const int MaxReconnectDelaySeconds = 60;
    private const int InitialReconnectDelaySeconds = 1;

    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _receiveCts;
    private Task? _receiveTask;
    private bool _disposed;
    private bool _intentionalDisconnect;

    private string? _host;
    private int _port;
    private string? _password;

    public event EventHandler? StreamStarted;
    public event EventHandler? StreamStopped;
    public event EventHandler? Connected;
    public event EventHandler? Disconnected;

    public bool IsConnected => _webSocket?.State == WebSocketState.Open;

    public async Task ConnectAsync(string host, int port, string? password, CancellationToken ct)
    {
        ThrowIfDisposed();

        _host = host;
        _port = port;
        _password = password;
        _intentionalDisconnect = false;

        await ConnectInternalAsync(ct).ConfigureAwait(false);
    }

    private async Task ConnectInternalAsync(CancellationToken ct)
    {
        _webSocket?.Dispose();
        _webSocket = new ClientWebSocket();

        var uri = new Uri($"ws://{_host}:{_port}");
        await _webSocket.ConnectAsync(uri, ct).ConfigureAwait(false);

        var helloMessage = await ReceiveMessageAsync(ct).ConfigureAwait(false);
        if (helloMessage == null)
        {
            throw new InvalidOperationException("Did not receive Hello message from OBS.");
        }

        var hello = JsonSerializer.Deserialize<ObsMessage>(helloMessage);
        if (hello?.Op != OpCode.Hello)
        {
            throw new InvalidOperationException($"Expected Hello message, got op code {hello?.Op}.");
        }

        var helloData = hello.D?.Deserialize<HelloData>();
        if (helloData == null)
        {
            throw new InvalidOperationException("Failed to parse Hello data.");
        }

        var identifyPayload = new IdentifyData
        {
            RpcVersion = helloData.RpcVersion
        };

        if (helloData.Authentication != null && !string.IsNullOrEmpty(_password))
        {
            identifyPayload.Authentication = GenerateAuthString(
                _password,
                helloData.Authentication.Salt,
                helloData.Authentication.Challenge);
        }

        var identifyMessage = new ObsMessage
        {
            Op = OpCode.Identify,
            D = JsonSerializer.SerializeToElement(identifyPayload)
        };

        await SendMessageAsync(identifyMessage, ct).ConfigureAwait(false);

        var identifiedMessage = await ReceiveMessageAsync(ct).ConfigureAwait(false);
        if (identifiedMessage == null)
        {
            throw new InvalidOperationException("Did not receive Identified message from OBS.");
        }

        var identified = JsonSerializer.Deserialize<ObsMessage>(identifiedMessage);
        if (identified?.Op != OpCode.Identified)
        {
            throw new InvalidOperationException($"Expected Identified message, got op code {identified?.Op}.");
        }

        _receiveCts = new CancellationTokenSource();
        _receiveTask = ReceiveLoopAsync(_receiveCts.Token);

        Connected?.Invoke(this, EventArgs.Empty);
    }

    public async Task DisconnectAsync()
    {
        ThrowIfDisposed();

        _intentionalDisconnect = true;

        if (_receiveCts != null)
        {
            await _receiveCts.CancelAsync().ConfigureAwait(false);
        }

        if (_receiveTask != null)
        {
            try
            {
                await _receiveTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelling
            }
        }

        if (_webSocket != null && _webSocket.State == WebSocketState.Open)
        {
            try
            {
                await _webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Client disconnecting",
                    CancellationToken.None).ConfigureAwait(false);
            }
            catch (WebSocketException)
            {
                // Socket may already be closed
            }
        }

        CleanupWebSocket();
        Disconnected?.Invoke(this, EventArgs.Empty);
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[ReceiveBufferSize];
        var messageBuilder = new StringBuilder();

        try
        {
            while (!ct.IsCancellationRequested && _webSocket?.State == WebSocketState.Open)
            {
                messageBuilder.Clear();
                WebSocketReceiveResult result;

                do
                {
                    result = await _webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        ct).ConfigureAwait(false);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        return;
                    }

                    messageBuilder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                }
                while (!result.EndOfMessage);

                var messageJson = messageBuilder.ToString();
                ProcessMessage(messageJson);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Normal cancellation
        }
        catch (WebSocketException)
        {
            // Connection lost
        }
        finally
        {
            if (!_intentionalDisconnect && !_disposed)
            {
                CleanupWebSocket();
                Disconnected?.Invoke(this, EventArgs.Empty);
                _ = ReconnectWithBackoffAsync();
            }
        }
    }

    private async Task ReconnectWithBackoffAsync()
    {
        if (_disposed || _intentionalDisconnect || string.IsNullOrEmpty(_host))
        {
            return;
        }

        var delay = InitialReconnectDelaySeconds;

        while (!_disposed && !_intentionalDisconnect)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(delay)).ConfigureAwait(false);

                if (_disposed || _intentionalDisconnect)
                {
                    return;
                }

                await ConnectInternalAsync(CancellationToken.None).ConfigureAwait(false);
                return;
            }
            catch (Exception)
            {
                delay = Math.Min(delay * 2, MaxReconnectDelaySeconds);
            }
        }
    }

    private void ProcessMessage(string messageJson)
    {
        try
        {
            var message = JsonSerializer.Deserialize<ObsMessage>(messageJson);
            if (message?.Op != OpCode.Event)
            {
                return;
            }

            var eventData = message.D?.Deserialize<EventMessage>();
            if (eventData?.EventType != "StreamStateChanged")
            {
                return;
            }

            var streamStateData = eventData.EventData?.Deserialize<StreamStateChangedData>();
            if (streamStateData == null)
            {
                return;
            }

            switch (streamStateData.OutputState)
            {
                case "OBS_WEBSOCKET_OUTPUT_STARTED":
                    StreamStarted?.Invoke(this, EventArgs.Empty);
                    break;
                case "OBS_WEBSOCKET_OUTPUT_STOPPED":
                    StreamStopped?.Invoke(this, EventArgs.Empty);
                    break;
            }
        }
        catch (JsonException)
        {
            // Ignore malformed messages
        }
    }

    private async Task<string?> ReceiveMessageAsync(CancellationToken ct)
    {
        if (_webSocket == null)
        {
            return null;
        }

        var buffer = new byte[ReceiveBufferSize];
        var messageBuilder = new StringBuilder();
        WebSocketReceiveResult result;

        do
        {
            result = await _webSocket.ReceiveAsync(
                new ArraySegment<byte>(buffer),
                ct).ConfigureAwait(false);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            messageBuilder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
        }
        while (!result.EndOfMessage);

        return messageBuilder.ToString();
    }

    private async Task SendMessageAsync(ObsMessage message, CancellationToken ct)
    {
        if (_webSocket == null || _webSocket.State != WebSocketState.Open)
        {
            throw new InvalidOperationException("WebSocket is not connected.");
        }

        var json = JsonSerializer.Serialize(message);
        var bytes = Encoding.UTF8.GetBytes(json);
        await _webSocket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            true,
            ct).ConfigureAwait(false);
    }

    private static string GenerateAuthString(string password, string salt, string challenge)
    {
        var passwordSalt = password + salt;
        var passwordSaltHash = SHA256.HashData(Encoding.UTF8.GetBytes(passwordSalt));
        var base64Secret = Convert.ToBase64String(passwordSaltHash);

        var secretChallenge = base64Secret + challenge;
        var secretChallengeHash = SHA256.HashData(Encoding.UTF8.GetBytes(secretChallenge));
        return Convert.ToBase64String(secretChallengeHash);
    }

    private void CleanupWebSocket()
    {
        _receiveCts?.Dispose();
        _receiveCts = null;
        _receiveTask = null;
        _webSocket?.Dispose();
        _webSocket = null;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _intentionalDisconnect = true;
        _receiveCts?.Cancel();
        CleanupWebSocket();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _intentionalDisconnect = true;

        if (_receiveCts != null)
        {
            await _receiveCts.CancelAsync().ConfigureAwait(false);
        }

        if (_receiveTask != null)
        {
            try
            {
                await _receiveTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }

        CleanupWebSocket();
    }

    #region OBS WebSocket Protocol Types

    private enum OpCode
    {
        Hello = 0,
        Identify = 1,
        Identified = 2,
        Event = 5
    }

    private sealed class ObsMessage
    {
        [JsonPropertyName("op")]
        public OpCode Op { get; set; }

        [JsonPropertyName("d")]
        public JsonElement? D { get; set; }
    }

    private sealed class HelloData
    {
        [JsonPropertyName("obsWebSocketVersion")]
        public string ObsWebSocketVersion { get; set; } = string.Empty;

        [JsonPropertyName("rpcVersion")]
        public int RpcVersion { get; set; }

        [JsonPropertyName("authentication")]
        public AuthenticationData? Authentication { get; set; }
    }

    private sealed class AuthenticationData
    {
        [JsonPropertyName("challenge")]
        public string Challenge { get; set; } = string.Empty;

        [JsonPropertyName("salt")]
        public string Salt { get; set; } = string.Empty;
    }

    private sealed class IdentifyData
    {
        [JsonPropertyName("rpcVersion")]
        public int RpcVersion { get; set; }

        [JsonPropertyName("authentication")]
        public string? Authentication { get; set; }
    }

    private sealed class EventMessage
    {
        [JsonPropertyName("eventType")]
        public string EventType { get; set; } = string.Empty;

        [JsonPropertyName("eventData")]
        public JsonElement? EventData { get; set; }
    }

    private sealed class StreamStateChangedData
    {
        [JsonPropertyName("outputActive")]
        public bool OutputActive { get; set; }

        [JsonPropertyName("outputState")]
        public string OutputState { get; set; } = string.Empty;
    }

    #endregion
}
