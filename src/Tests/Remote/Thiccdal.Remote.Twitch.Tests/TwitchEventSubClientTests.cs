using System.Net;
using System.Net.WebSockets;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Thiccdal.Infrastructure.Twitch;
using Thiccdal.Remote.Twitch;

namespace Thiccdal.Remote.Twitch.Tests;

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
        // Server 2 must be created first so its URL can be embedded in server 1's reconnect message
        await using FakeEventSubServer server2 = new(
            new[] { BuildWelcomePayload("reconnect-session") },
            closeAfterSending: false);

        await using FakeEventSubServer server1 = new(
            new[] { BuildWelcomePayload("initial-session"), BuildReconnectPayload(server2.WebSocketUrl, "reconnect-session") },
            closeAfterSending: false);

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

        // Wait for server 2 to receive the reconnect connection
        bool reconnected = await Task.WhenAny(server2.ConnectionTask, Task.Delay(TimeSpan.FromSeconds(5))) == server2.ConnectionTask;

        Assert.True(reconnected, "Client should connect to the reconnect URL provided by Twitch");
        // Subscriptions are created only on initial connect (subscribe: true), not on reconnect (subscribe: false)
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

        private async Task ServeAsync()
        {
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
                    if (socket.State != WebSocketState.Open)
                    {
                        break;
                    }

                    byte[] bytes = Encoding.UTF8.GetBytes(message);
                    await socket.SendAsync(
                        new ArraySegment<byte>(bytes),
                        WebSocketMessageType.Text,
                        true,
                        CancellationToken.None).ConfigureAwait(false);
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
                    // Drain messages until the client closes the connection
                    byte[] buf = new byte[64];
                    while (socket.State == WebSocketState.Open)
                    {
                        WebSocketReceiveResult result = await socket.ReceiveAsync(
                            new ArraySegment<byte>(buf),
                            CancellationToken.None).ConfigureAwait(false);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            break;
                        }
                    }
                }
            }
            catch (ObjectDisposedException) { }
            catch (HttpListenerException) { }
            catch (WebSocketException) { }
        }

        public ValueTask DisposeAsync()
        {
            _connectionTcs.TrySetCanceled();
            try
            {
                _listener.Stop();
            }
            catch (ObjectDisposedException) { }

            return ValueTask.CompletedTask;
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
