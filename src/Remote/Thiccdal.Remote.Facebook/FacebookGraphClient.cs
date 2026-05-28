using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.Facebook;

namespace Thiccdal.Remote.Facebook;

public sealed class FacebookGraphClient : IFacebookGraphClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly HashSet<string> InactiveStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "LIVE_STOPPED",
        "VOD",
        "COMPLETE"
    };

    private readonly ILogger<FacebookGraphClient> _logger;
    private readonly HttpClient _httpClient;

    public FacebookGraphClient(
        IOptions<FacebookOptions> options,
        ILogger<FacebookGraphClient> logger,
        IHttpClientFactory httpClientFactory)
    {
        _ = options.Value;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient(FacebookClientNames.GraphApi);
    }

    public async Task<FacebookLiveVideo> CreateLiveVideo(
        string pageId,
        string pageAccessToken,
        string title,
        string description,
        string privacy,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            status = "LIVE_NOW",
            title,
            description,
            privacy = new
            {
                value = privacy
            },
            access_token = pageAccessToken
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{Uri.EscapeDataString(pageId)}/live_videos")
        {
            Content = JsonContent.Create(payload, options: SerializerOptions)
        };

        return await SendForJson<FacebookLiveVideo>(request, cancellationToken)
            ?? throw new InvalidOperationException("Facebook Graph API did not return a live video payload.");
    }

    public async Task EndLiveVideo(
        string liveVideoId,
        string pageAccessToken,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Uri.EscapeDataString(liveVideoId))
        {
            Content = JsonContent.Create(
                new
                {
                    end_live_video = true,
                    access_token = pageAccessToken
                },
                options: SerializerOptions)
        };

        await SendWithoutResult(request, cancellationToken);
    }

    public async Task<FacebookLiveVideo?> GetActiveLiveVideo(
        string pageId,
        string pageAccessToken,
        CancellationToken cancellationToken = default)
    {
        string requestUri =
            $"{Uri.EscapeDataString(pageId)}/live_videos?fields=id,status,title,description,stream_url,secure_stream_url&access_token={Uri.EscapeDataString(pageAccessToken)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        FacebookPagedResponse<FacebookLiveVideo>? response = await SendForJson<FacebookPagedResponse<FacebookLiveVideo>>(request, cancellationToken);

        return response?.Data.FirstOrDefault(static liveVideo =>
            !string.IsNullOrWhiteSpace(liveVideo.Id) &&
            !InactiveStatuses.Contains(liveVideo.Status));
    }

    public async Task<IReadOnlyList<FacebookComment>> GetComments(
        string liveVideoId,
        string pageAccessToken,
        DateTimeOffset? since,
        CancellationToken cancellationToken = default)
    {
        string requestUri =
            $"{Uri.EscapeDataString(liveVideoId)}/comments?fields=id,message,from,created_time&access_token={Uri.EscapeDataString(pageAccessToken)}";

        if (since.HasValue)
        {
            requestUri = $"{requestUri}&since={since.Value.ToUnixTimeSeconds()}";
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        FacebookPagedResponse<FacebookComment>? response = await SendForJson<FacebookPagedResponse<FacebookComment>>(request, cancellationToken);
        return response?.Data ?? [];
    }

    public async Task PostComment(
        string liveVideoId,
        string pageAccessToken,
        string message,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{Uri.EscapeDataString(liveVideoId)}/comments")
        {
            Content = JsonContent.Create(
                new
                {
                    message,
                    access_token = pageAccessToken
                },
                options: SerializerOptions)
        };

        await SendWithoutResult(request, cancellationToken);
    }

    public async Task<IReadOnlyList<FacebookReaction>> GetReactions(
        string liveVideoId,
        string pageAccessToken,
        CancellationToken cancellationToken = default)
    {
        string requestUri =
            $"{Uri.EscapeDataString(liveVideoId)}/reactions?fields=type,name,id&access_token={Uri.EscapeDataString(pageAccessToken)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        FacebookPagedResponse<FacebookReaction>? response = await SendForJson<FacebookPagedResponse<FacebookReaction>>(request, cancellationToken);
        return response?.Data ?? [];
    }

    public async Task UpdateLiveVideo(
        string liveVideoId,
        string pageAccessToken,
        string? title,
        string? description,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Uri.EscapeDataString(liveVideoId))
        {
            Content = JsonContent.Create(
                new
                {
                    title,
                    description,
                    access_token = pageAccessToken
                },
                options: SerializerOptions)
        };

        await SendWithoutResult(request, cancellationToken);
    }

    private async Task<T?> SendForJson<T>(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccess(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(SerializerOptions, cancellationToken);
    }

    private async Task SendWithoutResult(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccess(response, cancellationToken);
    }

    private async Task EnsureSuccess(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogWarning(
            "Facebook Graph API request to {RequestUri} failed with status code {StatusCode}",
            response.RequestMessage?.RequestUri,
            (int)response.StatusCode);

        throw new HttpRequestException(
            $"Facebook Graph API request failed with status code {(int)response.StatusCode}: {responseBody}",
            null,
            response.StatusCode);
    }
}
