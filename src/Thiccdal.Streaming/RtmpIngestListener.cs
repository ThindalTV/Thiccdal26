using System.Net;
using LiveStreamingServerNet;
using LiveStreamingServerNet.Networking.Contracts;
using LiveStreamingServerNet.Rtmp.Server.Auth;
using LiveStreamingServerNet.Rtmp.Server.Auth.Contracts;
using LiveStreamingServerNet.Rtmp.Server.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.Streaming;

namespace Thiccdal.Streaming;

public sealed class RtmpIngestListener : IRtmpIngestListener
{
    private readonly StreamingOptions _options;
    private readonly ILogger<RtmpIngestListener> _logger;
    private readonly Lock _stateLock = new();
    private readonly IngestReservationState _reservationState = new();
    private ILiveStreamingServer? _server;
    private Task? _runTask;

    public RtmpIngestListener(IOptions<StreamingOptions> options, ILogger<RtmpIngestListener> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options.Value;
        _logger = logger;
    }

    public bool IsListening
    {
        get
        {
            lock (_stateLock)
            {
                return _server is not null;
            }
        }
    }

    public event EventHandler<RtmpIngestStateChanged>? StateChanged;

    public Task Start(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_stateLock)
        {
            if (_server is not null)
            {
                return Task.CompletedTask;
            }

            (IPEndPoint listenEndPoint, string expectedStreamPath) = ParseIngestEndpoint(_options.IngestUrl);
            _reservationState.Reset(expectedStreamPath);

            _server = LiveStreamingServerBuilder.Create()
                .ConfigureRtmpServer(options =>
                {
                    options.AddAuthorizationHandler(_ => new SinglePublisherAuthorizationHandler(_reservationState, _logger));
                    options.AddStreamEventHandler(_ => new IngestLifecycleEventHandler(_reservationState, NotifyStateChanged, _logger));
                })
                .Build();

            ILiveStreamingServer server = _server;
            _runTask = Task.Run(
                async () =>
                {
                    try
                    {
                        await server.RunAsync(listenEndPoint);
                    }
                    catch (ObjectDisposedException)
                    {
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogError(ex, "RTMP ingest listener failed.");
                        NotifyStateChanged(
                            new RtmpIngestStateChanged
                            {
                                State = StreamingState.Error,
                                StreamPath = _reservationState.ExpectedStreamPath,
                                Message = "RTMP ingest listener failed."
                            });
                    }
                },
                CancellationToken.None);

            _logger.LogInformation(
                "RTMP ingest listener is bound to {ListenEndPoint} for stream path {StreamPath}.",
                listenEndPoint,
                expectedStreamPath);
        }

        return Task.CompletedTask;
    }

    public async Task Stop(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ILiveStreamingServer? server;
        Task? runTask;

        lock (_stateLock)
        {
            server = _server;
            runTask = _runTask;
            _server = null;
            _runTask = null;
        }

        _reservationState.Reset(string.Empty);

        if (server is null)
        {
            return;
        }

        Task disposeTask = server.DisposeAsync().AsTask();
        await Task.WhenAny(disposeTask, Task.Delay(TimeSpan.FromSeconds(5), cancellationToken));
        if (runTask is not null)
        {
            await Task.WhenAny(runTask, Task.Delay(TimeSpan.FromSeconds(5), cancellationToken));
        }
    }

    private void NotifyStateChanged(RtmpIngestStateChanged stateChanged)
    {
        StateChanged?.Invoke(this, stateChanged);
    }

    private static (IPEndPoint ListenEndPoint, string StreamPath) ParseIngestEndpoint(string ingestUrl)
    {
        if (!Uri.TryCreate(ingestUrl, UriKind.Absolute, out Uri? uri))
        {
            throw new InvalidOperationException("Streaming:IngestUrl must be a valid absolute RTMP URL.");
        }

        if (!string.Equals(uri.Scheme, "rtmp", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, "rtmps", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Streaming:IngestUrl must use the rtmp or rtmps scheme.");
        }

        string streamPath = NormalizeStreamPath(uri.AbsolutePath);
        if (string.IsNullOrWhiteSpace(streamPath))
        {
            throw new InvalidOperationException("Streaming:IngestUrl must include a stream path, for example rtmp://localhost:1935/live.");
        }

        IPAddress listenAddress = uri.Host switch
        {
            "" => IPAddress.Any,
            "0.0.0.0" => IPAddress.Any,
            "127.0.0.1" => IPAddress.Any,
            "localhost" => IPAddress.Any,
            _ when IPAddress.TryParse(uri.Host, out IPAddress? parsedAddress) => parsedAddress,
            _ => IPAddress.Any
        };

        return (new IPEndPoint(listenAddress, uri.IsDefaultPort ? 1935 : uri.Port), streamPath);
    }

    private static string NormalizeStreamPath(string streamPath)
    {
        return streamPath.Trim().Trim('/');
    }

    private sealed class SinglePublisherAuthorizationHandler : IAuthorizationHandler
    {
        private readonly IngestReservationState _reservationState;
        private readonly ILogger _logger;

        public SinglePublisherAuthorizationHandler(IngestReservationState reservationState, ILogger logger)
        {
            _reservationState = reservationState;
            _logger = logger;
        }

        public Task<AuthorizationResult> AuthorizePublishingAsync(
            ISessionInfo client,
            string streamPath,
            IReadOnlyDictionary<string, string> streamArguments,
            string publishingType)
        {
            _ = publishingType;

            string normalizedStreamPath = NormalizeStreamPath(streamPath);
            if (!string.Equals(normalizedStreamPath, _reservationState.ExpectedStreamPath, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Rejected RTMP publisher {ClientId} for unexpected stream path {StreamPath}. Expected {ExpectedStreamPath}.",
                    client.Id,
                    normalizedStreamPath,
                    _reservationState.ExpectedStreamPath);
                return Task.FromResult(AuthorizationResult.Unauthorized("Unexpected stream path."));
            }

            if (!_reservationState.TryReservePublisher(client.Id))
            {
                _logger.LogWarning(
                    "Rejected RTMP publisher {ClientId} because another OBS ingest is already connected.",
                    client.Id);
                return Task.FromResult(AuthorizationResult.Unauthorized("Only one ingest publisher is supported."));
            }

            return Task.FromResult(AuthorizationResult.Authorized(_reservationState.ExpectedStreamPath, streamArguments));
        }

        public Task<AuthorizationResult> AuthorizeSubscribingAsync(
            ISessionInfo client,
            string streamPath,
            IReadOnlyDictionary<string, string> streamArguments)
        {
            _ = client;

            string normalizedStreamPath = NormalizeStreamPath(streamPath);
            if (!string.Equals(normalizedStreamPath, _reservationState.ExpectedStreamPath, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(AuthorizationResult.Unauthorized("Unexpected stream path."));
            }

            return Task.FromResult(AuthorizationResult.Authorized(_reservationState.ExpectedStreamPath, streamArguments));
        }
    }

    private sealed class IngestLifecycleEventHandler : IRtmpServerStreamEventHandler
    {
        private readonly IngestReservationState _reservationState;
        private readonly Action<RtmpIngestStateChanged> _notifyStateChanged;
        private readonly ILogger _logger;

        public IngestLifecycleEventHandler(
            IngestReservationState reservationState,
            Action<RtmpIngestStateChanged> notifyStateChanged,
            ILogger logger)
        {
            _reservationState = reservationState;
            _notifyStateChanged = notifyStateChanged;
            _logger = logger;
        }

        public ValueTask OnRtmpStreamPublishedAsync(
            LiveStreamingServerNet.Utilities.Contracts.IEventContext context,
            uint clientId,
            string streamPath,
            IReadOnlyDictionary<string, string> streamArguments)
        {
            _ = context;
            _ = streamArguments;

            _reservationState.ConfirmPublisher(clientId);
            _logger.LogInformation("OBS ingest connected on RTMP path {StreamPath}.", streamPath);
            _notifyStateChanged(
                new RtmpIngestStateChanged
                {
                    State = StreamingState.Live,
                    StreamPath = NormalizeStreamPath(streamPath),
                    Message = "OBS ingest connected."
                });
            return ValueTask.CompletedTask;
        }

        public ValueTask OnRtmpStreamUnpublishedAsync(
            LiveStreamingServerNet.Utilities.Contracts.IEventContext context,
            uint clientId,
            string streamPath)
        {
            _ = context;

            _reservationState.ReleasePublisher(clientId);
            _logger.LogInformation("OBS ingest disconnected from RTMP path {StreamPath}.", streamPath);
            _notifyStateChanged(
                new RtmpIngestStateChanged
                {
                    State = StreamingState.BrbSlate,
                    StreamPath = NormalizeStreamPath(streamPath),
                    Message = "OBS ingest disconnected."
                });
            return ValueTask.CompletedTask;
        }

        public ValueTask OnRtmpStreamMetaDataReceivedAsync(
            LiveStreamingServerNet.Utilities.Contracts.IEventContext context,
            uint clientId,
            string streamPath,
            IReadOnlyDictionary<string, object> metaData)
        {
            _ = context;
            _ = clientId;
            _ = streamPath;
            _ = metaData;
            return ValueTask.CompletedTask;
        }

        public ValueTask OnRtmpStreamSubscribedAsync(
            LiveStreamingServerNet.Utilities.Contracts.IEventContext context,
            uint clientId,
            string streamPath,
            IReadOnlyDictionary<string, string> streamArguments)
        {
            _ = context;
            _ = clientId;
            _ = streamPath;
            _ = streamArguments;
            return ValueTask.CompletedTask;
        }

        public ValueTask OnRtmpStreamUnsubscribedAsync(
            LiveStreamingServerNet.Utilities.Contracts.IEventContext context,
            uint clientId,
            string streamPath)
        {
            _ = context;
            _ = clientId;
            _ = streamPath;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class IngestReservationState
    {
        private readonly Lock _lock = new();
        private uint? _reservedPublisherId;

        public string ExpectedStreamPath { get; private set; } = string.Empty;

        public void Reset(string expectedStreamPath)
        {
            lock (_lock)
            {
                ExpectedStreamPath = expectedStreamPath;
                _reservedPublisherId = null;
            }
        }

        public bool TryReservePublisher(uint publisherId)
        {
            lock (_lock)
            {
                if (_reservedPublisherId.HasValue && _reservedPublisherId.Value != publisherId)
                {
                    return false;
                }

                _reservedPublisherId = publisherId;
                return true;
            }
        }

        public void ConfirmPublisher(uint publisherId)
        {
            lock (_lock)
            {
                _reservedPublisherId = publisherId;
            }
        }

        public void ReleasePublisher(uint publisherId)
        {
            lock (_lock)
            {
                if (_reservedPublisherId == publisherId)
                {
                    _reservedPublisherId = null;
                }
            }
        }
    }
}
