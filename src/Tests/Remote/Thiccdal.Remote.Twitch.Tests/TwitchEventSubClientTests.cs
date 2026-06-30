using System.Net;
using System.Net.WebSockets;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Thiccdal.Infrastructure.Twitch;
using Thiccdal.Remote.Twitch;

namespace Thiccdal.Remote.Twitch.Tests;

/// <summary>
/// Tests in this collection use real network sockets (HttpListener / WebSocket) and must not run
/// in parallel with other network-heavy tests to avoid resource conflicts during cleanup.
/// </summary>
[CollectionDefinition("NetworkIntegration", DisableParallelization = true)]
public sealed class NetworkIntegrationCollection { }

[Collection("NetworkIntegration")]
public sealed class TwitchEventSubClientTests
{
    private static TwitchChatConnectionProfile BuildProfile() => new TwitchChatConnectionProfile
    {
        BotUsername = "testbot",
        BotUserId = "111",
        TargetChannel = "channel",
        BroadcasterId = "222"
    };

    private static TwitchEventSubClient BuildClient(string webSocketUrl, Mock<ITwitchHelixClient> helixMock)
    {
        TwitchOptions options = new TwitchOptions
        {
            ClientId = "client-id",
            EventSub = new TwitchEventSubOptions { WebSocketUrl = webSocketUrl }
        };
        return new TwitchEventSubClient(
            Options.Create(options),
            helixMock.Object,
            new TwitchEventSubNotificationMapper(new EmoteRenderingOptions(false)),
            NullLogger<TwitchEventSubClient>.Instance);
    }

    private static Mock<ITwitchHelixClient> BuildHelixMock()
    {
        Mock<ITwitchHelixClient> mock = new Mock<ITwitchHelixClient>();
        mock.Setup(h => h.GetEventSubscriptions(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TwitchEventSubSubscription>());
        mock.Setup(h => h.CreateEventSubscription(
                It.IsAny<TwitchEventSubSubscriptionRequest>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return mock;
    }

    [Fact]
    public void WhenNotConnected_ThenConnectedIsFalse()
    {
        Mock<ITwitchHelixClient> helixMock = BuildHelixMock();
        TwitchEventSubClient client = BuildClient("ws://127.0.0.1:1/", helixMock);

        Assert.False(client.Connected);
    }

    [Fact]
    public async Task WhenSessionWelcomeReceived_ThenSessionIdIsStored()
    {
        const string sessionId = "session-abc-123";

        await using FakeEventSubServer server = new(new[] { BuildWelcomePayload(sessionId) }, closeAfterSending: true);

        List<TwitchEventSubSubscriptionRequest> captured = new List<TwitchEventSubSubscriptionRequest>();
        Mock<ITwitchHelixClient> helixMock = BuildHelixMock();
        helixMock.Setup(h => h.CreateEventSubscription(
                It.IsAny<TwitchEventSubSubscriptionRequest>(),
                It.IsAny<CancellationToken>()))
            .Callback<TwitchEventSubSubscriptionRequest, CancellationToken>((req, _) => captured.Add(req))
            .Returns(Task.CompletedTask);

        await using TwitchEventSubClient client = BuildClient(server.WebSocketUrl, helixMock);

        using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await client.Connect(BuildProfile(), cts.Token);

        Assert.NotEmpty(captured);
        Assert.All(captured, req => Assert.Equal(sessionId, req.SessionId));
    }

    [Fact]
    public async Task WhenKeepaliveReceived_ThenConnectionRemainsActive()
    {
        await using FakeEventSubServer server = new(
            new[] { BuildWelcomePayload("keepalive-session"), BuildKeepalivePayload() },
            closeAfterSending: false);

        Mock<ITwitchHelixClient> helixMock = BuildHelixMock();
        await using TwitchEventSubClient client = BuildClient(server.WebSocketUrl, helixMock);

        using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await client.Connect(BuildProfile(), cts.Token);

        Assert.True(client.Connected);

        // Allow keepalive to be processed by the listen loop, then verify still connected
        await Task.Delay(200);
        Assert.True(client.Connected);
    }

    [Fact]
    public async Task WhenConnectionDropped_ThenDisconnectedEventRaised()
    {
        // Server closes immediately after welcome — listen task detects close and fires Disconnected
        await using FakeEventSubServer server = new(new[] { BuildWelcomePayload("drop-session") }, closeAfterSending: true);

        Mock<ITwitchHelixClient> helixMock = BuildHelixMock();
        await using TwitchEventSubClient client = BuildClient(server.WebSocketUrl, helixMock);

        TaskCompletionSource disconnectedTcs = new TaskCompletionSource();
        client.Disconnected += (_, _) => disconnectedTcs.TrySetResult();

        using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await client.Connect(BuildProfile(), cts.Token);

        bool fired = await Task.WhenAny(disconnectedTcs.Task, Task.Delay(TimeSpan.FromSeconds(5))) == disconnectedTcs.Task;

        Assert.True(fired, "Disconnected event should fire after server closes connection");
        Assert.False(client.Connected);
    }

    [Fact]
    public async Task WhenSessionReconnectReceived_ThenClientConnectsToNewUrl()
    {
        // Server 2 must be created first so its URL can be embedded in server 1's reconnect message.
        await using FakeEventSubServer server2 = new(
            new[] { BuildWelcomePayload("reconnect-session") },
            closeAfterSending: false);

        await server2.WaitForReady();

        await using FakeEventSubServer server1 = new(
            new[] { BuildWelcomePayload("initial-session"), BuildReconnectPayload(server2.WebSocketUrl, "reconnect-session") },
            closeAfterSending: false);

        await server1.WaitForReady();

        List<TwitchEventSubSubscriptionRequest> captured = new List<TwitchEventSubSubscriptionRequest>();
        Mock<ITwitchHelixClient> helixMock = BuildHelixMock();
        helixMock.Setup(h => h.CreateEventSubscription(
                It.IsAny<TwitchEventSubSubscriptionRequest>(),
                It.IsAny<CancellationToken>()))
            .Callback<TwitchEventSubSubscriptionRequest, CancellationToken>((req, _) => captured.Add(req))
            .Returns(Task.CompletedTask);

        await using TwitchEventSubClient client = BuildClient(server1.WebSocketUrl, helixMock);

        using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await client.Connect(BuildProfile(), cts.Token);

        // Wait for server2 to accept the reconnect connection and then allow a short settling
        // period for the listen task to finish ConnectCore and release _connectionGate.
        // Without this padding the test's await-using cleanup would race against the gate release.
        bool reconnected = await Task.WhenAny(server2.ConnectionTask, Task.Delay(TimeSpan.FromSeconds(10), cts.Token)) == server2.ConnectionTask;
        if (reconnected)
        {
            await Task.Delay(500, cts.Token); // let ConnectCore finish and release the gate
        }

        Assert.True(reconnected, "Client should connect to the reconnect URL provided by Twitch");
        // Subscriptions are created only on initial connect (subscribe: true), not on reconnect
        Assert.NotEmpty(captured);
        Assert.All(captured, req => Assert.Equal("initial-session", req.SessionId));
    }

    private static string BuildWelcomePayload(string sessionId) =>
        """{"metadata":{"message_type":"session_welcome","message_id":"w-1"},"payload":{"session":{"id":"<SID>","reconnect_url":null}}}"""
            .Replace("<SID>", sessionId);

    private static string BuildKeepalivePayload() =>
        """{"metadata":{"message_type":"session_keepalive","message_id":"k-1"},"payload":{}}""";

    private static string BuildReconnectPayload(string reconnectUrl, string newSessionId) =>
        """{"metadata":{"message_type":"session_reconnect","message_id":"r-1"},"payload":{"session":{"id":"<SID>","reconnect_url":"<URL>"}}}"""
            .Replace("<SID>", newSessionId)
            .Replace("<URL>", reconnectUrl);

    private sealed class FakeEventSubServer : IAsyncDisposable
    {
        private readonly HttpListener _listener;
        private readonly string[] _messages;
        private readonly bool _closeAfterSending;
        private readonly TaskCompletionSource<WebSocket> _connectionTcs = new TaskCompletionSource<WebSocket>();
        private readonly TaskCompletionSource _readyTcs = new TaskCompletionSource();
        private readonly CancellationTokenSource _serverCts = new CancellationTokenSource();

        public FakeEventSubServer(string[] messages, bool closeAfterSending)
        {
            _messages = messages;
            _closeAfterSending = closeAfterSending;
            Port = FindFreePort();
            WebSocketUrl = $"ws://127.0.0.1:{Port}/";
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
            _listener.Start();
            _ = Task.Run(ServeAsync);
        }

        public int Port { get; }
        public string WebSocketUrl { get; }
        public Task<WebSocket> ConnectionTask => _connectionTcs.Task;

        /// <summary>Resolves when <see cref="ServeAsync"/> has started and is ready to accept connections.</summary>
        public Task WaitForReady() => _readyTcs.Task;

        private async Task ServeAsync()
        {
            _readyTcs.TrySetResult();
            try
            {
                HttpListenerContext context = await _listener.GetContextAsync().ConfigureAwait(false);
                if (!context.Request.IsWebSocketRequest)
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                    return;
                }

                HttpListenerWebSocketContext wsContext = await context.AcceptWebSocketAsync(null).ConfigureAwait(false);
                WebSocket socket = wsContext.WebSocket;
                _connectionTcs.TrySetResult(socket);

                foreach (string message in _messages)
                {
                    if (socket.State != WebSocketState.Open || _serverCts.IsCancellationRequested)
                    {
                        break;
                    }

                    byte[] bytes = Encoding.UTF8.GetBytes(message);
                    await socket.SendAsync(
                        new ArraySegment<byte>(bytes),
                        WebSocketMessageType.Text,
                        true,
                        _serverCts.Token).ConfigureAwait(false);
                }

                if (_closeAfterSending && socket.State == WebSocketState.Open)
                {
                    await socket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        string.Empty,
                        CancellationToken.None).ConfigureAwait(false);
                }
                else if (!_closeAfterSending)
                {
                    // Drain until the client closes or server is disposed; complete the close handshake properly
                    byte[] buf = new byte[64];
                    while (socket.State == WebSocketState.Open && !_serverCts.IsCancellationRequested)
                    {
                        try
                        {
                            WebSocketReceiveResult result = await socket.ReceiveAsync(
                                new ArraySegment<byte>(buf),
                                _serverCts.Token).ConfigureAwait(false);
                            if (result.MessageType == WebSocketMessageType.Close)
                            {
                                // Acknowledge the close frame so the client's CloseAsync can complete
                                if (socket.State == WebSocketState.CloseReceived)
                                {
                                    await socket.CloseOutputAsync(
                                        WebSocketCloseStatus.NormalClosure,
                                        string.Empty,
                                        CancellationToken.None).ConfigureAwait(false);
                                }

                                break;
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                    }
                }
            }
            catch (ObjectDisposedException) { }
            catch (HttpListenerException) { }
            catch (WebSocketException) { }
            catch (OperationCanceledException) { }
        }

        public async ValueTask DisposeAsync()
        {
            _connectionTcs.TrySetCanceled();
            await _serverCts.CancelAsync().ConfigureAwait(false);
            _serverCts.Dispose();
            try
            {
                _listener.Stop();
            }
            catch (ObjectDisposedException) { }
        }

        private static int FindFreePort()
        {
            using System.Net.Sockets.TcpListener listener =
                new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}
