using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Infrastructure.YouTube;

namespace Thiccdal.Remote.YouTube;

public sealed class YouTubeApiClient : IYouTubeApiClient
{
    private readonly YouTubeOptions _options;
    private readonly IYouTubeTokenManager _tokenManager;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<YouTubeApiClient> _logger;

    public YouTubeApiClient(
        IOptions<YouTubeOptions> options,
        IYouTubeTokenManager tokenManager,
        IHttpClientFactory httpClientFactory,
        ILogger<YouTubeApiClient> logger)
    {
        _options = options.Value;
        _tokenManager = tokenManager;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<YouTubeBroadcastInfo?> GetActiveBroadcast(CancellationToken cancellationToken = default)
    {
        string? token = await _tokenManager.GetToken(cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Cannot fetch active broadcast without valid token");
            return null;
        }

        var httpClient = _httpClientFactory.CreateClient(YouTubeClientNames.Api);
        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        string url = $"liveBroadcasts?part=snippet,contentDetails,status,liveStreamingDetails&broadcastStatus=active&mine=true";
        HttpResponseMessage response = await httpClient.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("YouTube API liveBroadcasts request failed: {StatusCode}", response.StatusCode);
            return null;
        }

        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("items", out var items) || items.GetArrayLength() == 0)
        {
            return null;
        }

        var broadcast = items[0];
        string broadcastId = broadcast.GetProperty("id").GetString() ?? string.Empty;
        var snippet = broadcast.GetProperty("snippet");
        string liveChatId = snippet.TryGetProperty("liveChatId", out JsonElement liveChatProperty)
            ? liveChatProperty.GetString() ?? string.Empty
            : string.Empty;

        string title = snippet.GetProperty("title").GetString() ?? string.Empty;
        string description = snippet.GetProperty("description").GetString() ?? string.Empty;
        string category = snippet.TryGetProperty("categoryId", out JsonElement categoryProperty)
            ? categoryProperty.GetString() ?? string.Empty
            : string.Empty;
        string[] tags = snippet.TryGetProperty("tags", out JsonElement tagsProperty) && tagsProperty.ValueKind == JsonValueKind.Array
            ? tagsProperty.EnumerateArray()
                .Select(static tag => tag.GetString() ?? string.Empty)
                .Where(static tag => !string.IsNullOrWhiteSpace(tag))
                .ToArray()
            : [];

        bool isLive = broadcast.GetProperty("status").GetProperty("lifeCycleStatus").GetString() == "live";
        DateTimeOffset? startedAt = null;
        if (broadcast.TryGetProperty("liveStreamingDetails", out JsonElement liveStreamingDetails))
        {
            startedAt = ParseDateTimeOffset(liveStreamingDetails, "actualStartTime")
                ?? ParseDateTimeOffset(liveStreamingDetails, "scheduledStartTime");
        }

        int? concurrentViewers = null;
        if (broadcast.TryGetProperty("statistics", out var stats) &&
            stats.TryGetProperty("concurrentViewers", out var viewersProp))
        {
            if (viewersProp.TryGetInt32(out int viewers))
            {
                concurrentViewers = viewers;
            }
        }

        return new YouTubeBroadcastInfo
        {
            BroadcastId = broadcastId,
            LiveChatId = liveChatId,
            Title = title,
            Description = description,
            Category = category,
            Tags = tags,
            IsLive = isLive,
            StartedAt = startedAt,
            ConcurrentViewers = concurrentViewers
        };
    }

    public async Task<YouTubeLiveChatPollResult> PollLiveChat(string liveChatId, string? pageToken, CancellationToken cancellationToken = default)
    {
        string? token = await _tokenManager.GetToken(cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Cannot poll live chat without valid token");
            return new YouTubeLiveChatPollResult
            {
                NextPageToken = pageToken ?? string.Empty,
                PollingIntervalMillis = 5000,
                RawJson = "{}"
            };
        }

        var httpClient = _httpClientFactory.CreateClient(YouTubeClientNames.Api);
        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        string url = $"liveChat/messages?liveChatId={Uri.EscapeDataString(liveChatId)}&part=snippet,authorDetails";
        if (!string.IsNullOrWhiteSpace(pageToken))
        {
            url += $"&pageToken={Uri.EscapeDataString(pageToken)}";
        }

        HttpResponseMessage response = await httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        string nextPageToken = root.TryGetProperty("nextPageToken", out JsonElement nextPageTokenProperty)
            ? nextPageTokenProperty.GetString() ?? string.Empty
            : string.Empty;
        int pollingIntervalMillis = root.TryGetProperty("pollingIntervalMillis", out JsonElement pollingIntervalProperty)
            && pollingIntervalProperty.TryGetInt32(out int hint)
            ? hint
            : _options.PollFallbackIntervalMillis;

        return new YouTubeLiveChatPollResult
        {
            NextPageToken = nextPageToken,
            PollingIntervalMillis = pollingIntervalMillis,
            RawJson = json
        };
    }

    public async Task SendLiveChatMessage(string liveChatId, string message, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(liveChatId);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        string? token = await _tokenManager.GetToken(cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("Cannot send a YouTube live chat message without a valid token.");
        }

        HttpClient httpClient = _httpClientFactory.CreateClient(YouTubeClientNames.Api);
        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var payload = new
        {
            snippet = new
            {
                liveChatId,
                type = "textMessageEvent",
                textMessageDetails = new
                {
                    messageText = message
                }
            }
        };

        string requestJson = JsonSerializer.Serialize(payload);
        using StringContent content = new StringContent(requestJson, Encoding.UTF8, "application/json");

        HttpResponseMessage response = await httpClient.PostAsync("liveChat/messages?part=snippet", content, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateBroadcastInfo(string broadcastId, string title, string description, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(broadcastId);

        string? token = await _tokenManager.GetToken(cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("Cannot update YouTube broadcast info without a valid token.");
        }

        HttpClient httpClient = _httpClientFactory.CreateClient(YouTubeClientNames.Api);
        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var payload = new
        {
            id = broadcastId,
            snippet = new
            {
                title,
                description
            }
        };

        string requestJson = JsonSerializer.Serialize(payload);
        using StringContent content = new StringContent(requestJson, Encoding.UTF8, "application/json");

        string url = $"liveBroadcasts?part=snippet";
        HttpResponseMessage response = await httpClient.PutAsync(url, content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("YouTube API liveBroadcasts update failed: {StatusCode}", response.StatusCode);
            throw new PlatformOperationException($"YouTube broadcast update failed with status code {(int)response.StatusCode}.");
        }

        _logger.LogInformation("Updated YouTube broadcast {BroadcastId} title and description", broadcastId);
    }

    private static DateTimeOffset? ParseDateTimeOffset(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property) || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        string? rawValue = property.GetString();
        return DateTimeOffset.TryParse(rawValue, out DateTimeOffset parsedValue)
            ? parsedValue
            : null;
    }
}
