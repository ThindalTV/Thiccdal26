using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.Streaming;

namespace Thiccdal.Streaming;

/// <summary>
/// Communicates with the standalone remote RTMP server process via HTTP REST and SignalR.
/// </summary>
public sealed class RtmpServerClient : IRtmpServerClient, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ILogger<RtmpServerClient> _logger;
    private readonly Lock _connectionLock = new();
    private HubConnection? _hubConnection;
    private bool _isConnected;
    private string _baseUrl;
    private string _apiKey;

    /// <summary>
    /// Initializes a new instance of <see cref="RtmpServerClient"/>.
    /// </summary>
    public RtmpServerClient(
        IHttpClientFactory httpClientFactory,
        IOptions<RtmpServerOptions> options,
        ILogger<RtmpServerClient> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClient = httpClientFactory.CreateClient(nameof(RtmpServerClient));
        _baseUrl = options.Value.BaseUrl;
        _apiKey = options.Value.ApiKey;
        _logger = logger;
    }

    /// <inheritdoc/>
    public bool IsConnected => _isConnected;

    /// <inheritdoc/>
    public event EventHandler<RtmpServerEvent>? EventReceived;

    /// <inheritdoc/>
    public void Configure(string baseUrl, string apiKey)
    {
        lock (_connectionLock)
        {
            _baseUrl = baseUrl;
            _apiKey = apiKey;
        }
    }

    /// <inheritdoc/>
    public async Task Connect(CancellationToken cancellationToken = default)
    {
        // Disconnect any existing hub before building a new one.
        await Disconnect(cancellationToken);

        string baseUrl;
        string apiKey;
        lock (_connectionLock)
        {
            baseUrl = _baseUrl;
            apiKey = _apiKey;
        }

        HubConnection hub = new HubConnectionBuilder()
            .WithUrl(
                $"{baseUrl}/hubs/events",
                opts => opts.Headers.Add("X-Api-Key", apiKey))
            .WithAutomaticReconnect()
            .Build();

        hub.On<RtmpServerEvent>("EventReceived", rtmpEvent => EventReceived?.Invoke(this, rtmpEvent));

        hub.Reconnected += _ =>
        {
            _isConnected = true;
            _logger.LogInformation("Reconnected to RTMP server event hub.");
            return Task.CompletedTask;
        };

        hub.Closed += ex =>
        {
            _isConnected = false;
            if (ex is not null)
            {
                _logger.LogWarning(ex, "RTMP server event hub connection closed with error.");
            }
            else
            {
                _logger.LogInformation("RTMP server event hub connection closed.");
            }
            return Task.CompletedTask;
        };

        try
        {
            await hub.StartAsync(cancellationToken);
            _hubConnection = hub;
            _isConnected = true;
            _logger.LogInformation("Connected to RTMP server event hub at {BaseUrl}.", baseUrl);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to connect to RTMP server event hub at {BaseUrl}.", baseUrl);
            await hub.DisposeAsync();
        }
    }

    /// <inheritdoc/>
    public async Task Disconnect(CancellationToken cancellationToken = default)
    {
        HubConnection? hub = _hubConnection;
        _hubConnection = null;
        _isConnected = false;

        if (hub is not null)
        {
            await hub.StopAsync(cancellationToken);
            await hub.DisposeAsync();
        }
    }

    /// <inheritdoc/>
    public async Task<RtmpServerStatusResponse> GetStatus(CancellationToken cancellationToken = default)
    {
        try
        {
            using HttpRequestMessage request = BuildRequest(HttpMethod.Get, "/api/status");
            using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<RtmpServerStatusResponse>(JsonOptions, cancellationToken)
                ?? ErrorResponse("Server returned empty status response.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to get status from RTMP server.");
            return ErrorResponse(ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<RtmpServerStatusResponse> PushConfiguration(RtmpServerConfigurationPush config, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);

        try
        {
            using HttpRequestMessage request = BuildRequest(HttpMethod.Post, "/api/config");
            request.Content = JsonContent.Create(config, options: JsonOptions);
            using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<RtmpServerStatusResponse>(JsonOptions, cancellationToken)
                ?? ErrorResponse("Server returned empty response to configuration push.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to push configuration to RTMP server.");
            return ErrorResponse(ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<RtmpServerStatusResponse> Start(CancellationToken cancellationToken = default)
    {
        try
        {
            using HttpRequestMessage request = BuildRequest(HttpMethod.Post, "/api/start");
            using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<RtmpServerStatusResponse>(JsonOptions, cancellationToken)
                ?? ErrorResponse("Server returned empty response to start.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to start RTMP server.");
            return ErrorResponse(ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<RtmpServerStatusResponse> Stop(CancellationToken cancellationToken = default)
    {
        try
        {
            using HttpRequestMessage request = BuildRequest(HttpMethod.Post, "/api/stop");
            using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<RtmpServerStatusResponse>(JsonOptions, cancellationToken)
                ?? ErrorResponse("Server returned empty response to stop.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to stop RTMP server.");
            return ErrorResponse(ex.Message);
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await Disconnect();
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string path)
    {
        string baseUrl;
        string apiKey;
        lock (_connectionLock)
        {
            baseUrl = _baseUrl;
            apiKey = _apiKey;
        }

        HttpRequestMessage request = new HttpRequestMessage(method, $"{baseUrl}{path}");
        request.Headers.Add("X-Api-Key", apiKey);
        return request;
    }

    private static RtmpServerStatusResponse ErrorResponse(string message)
    {
        return new RtmpServerStatusResponse(
            IsIngestRunning: false,
            IsFanoutRunning: false,
            IsRecording: false,
            IngestState: string.Empty,
            ActiveRelayCount: 0,
            ErrorMessage: message);
    }
}
