using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Infrastructure.Twitch;

namespace Thiccdal.Remote.Twitch;

public sealed class TwitchHelixClient : ITwitchHelixClient
{
    private readonly TwitchOptions _options;
    private readonly ITwitchTokenManager _tokenManager;
    private readonly ILogger<TwitchHelixClient> _logger;
    private readonly HttpClient _httpClient;

    public TwitchHelixClient(
        IOptions<TwitchOptions> options,
        ITwitchTokenManager tokenManager,
        ILogger<TwitchHelixClient> logger,
        IHttpClientFactory httpClientFactory)
    {
        _options = options.Value;
        _tokenManager = tokenManager;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient(TwitchClientNames.Helix);
    }

    public async Task<TwitchSendMessageResult> SendChatMessage(
        TwitchChatConnectionProfile profile,
        string message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("A Twitch chat message is required.", nameof(message));
        }

        if (string.IsNullOrWhiteSpace(profile.BroadcasterId))
        {
            return new TwitchSendMessageResult
            {
                FailureCode = "missing_broadcaster_id",
                FailureMessage = "Twitch broadcaster ID is required for Helix chat send."
            };
        }

        if (string.IsNullOrWhiteSpace(profile.BotUserId))
        {
            return new TwitchSendMessageResult
            {
                FailureCode = "missing_sender_id",
                FailureMessage = "Twitch bot user ID is required for Helix chat send."
            };
        }

        string? token = await _tokenManager.GetToken(cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            return new TwitchSendMessageResult
            {
                FailureCode = "not_authorized",
                FailureMessage = "Twitch is not authorized."
            };
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/messages")
        {
            Content = JsonContent.Create(new SendChatMessageRequest(
                profile.BroadcasterId,
                profile.BotUserId,
                message))
        };

        ApplyAuthentication(request, token);

        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        HelixSendMessageResponse? payload = await response.Content.ReadFromJsonAsync<HelixSendMessageResponse>(cancellationToken: cancellationToken);
        HelixSendMessageData? messageResult = payload?.Data?.SingleOrDefault();
        if (messageResult == null)
        {
            throw new InvalidOperationException("Twitch Helix send chat response did not include a message result.");
        }

        if (!messageResult.IsSent)
        {
            _logger.LogWarning(
                "Twitch Helix dropped chat message for broadcaster {BroadcasterId}. Code: {FailureCode}; Message: {FailureMessage}",
                profile.BroadcasterId,
                messageResult.DropReason?.Code,
                messageResult.DropReason?.Message);
        }

        return new TwitchSendMessageResult
        {
            IsSuccessful = messageResult.IsSent,
            MessageId = messageResult.MessageId ?? string.Empty,
            FailureCode = messageResult.DropReason?.Code ?? string.Empty,
            FailureMessage = messageResult.DropReason?.Message ?? string.Empty
        };
    }

    public async Task<TwitchStreamState> GetStreamState(
        TwitchChatConnectionProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (string.IsNullOrWhiteSpace(profile.BroadcasterId) || string.IsNullOrWhiteSpace(_options.ClientId))
        {
            return new TwitchStreamState();
        }

        string? token = await _tokenManager.GetToken(cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            return new TwitchStreamState();
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"streams?user_id={Uri.EscapeDataString(profile.BroadcasterId)}");

        ApplyAuthentication(request, token);

        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        HelixStreamsResponse? payload = await response.Content.ReadFromJsonAsync<HelixStreamsResponse>(cancellationToken: cancellationToken);
        HelixStreamData? stream = payload?.Data?.FirstOrDefault();
        return new TwitchStreamState
        {
            IsLive = stream is not null,
            Title = stream?.Title ?? string.Empty,
            Category = stream?.GameName ?? string.Empty,
            Tags = stream?.Tags?
                .Where(static tag => !string.IsNullOrWhiteSpace(tag))
                .ToArray() ?? [],
            StartedAt = stream?.StartedAt,
            ViewerCount = stream?.ViewerCount ?? 0
        };
    }

    public async Task UpdateChannelInfo(
        TwitchChatConnectionProfile profile,
        string? title,
        string? category,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (string.IsNullOrWhiteSpace(profile.BroadcasterId))
        {
            throw new PlatformOperationException("Twitch broadcaster ID is required to update channel info.");
        }

        string? token = await _tokenManager.GetToken(cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new PlatformOperationException("Twitch is not authorized.");
        }

        Dictionary<string, object?> payload = new(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(title))
        {
            payload["title"] = title;
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            string gameId = await ResolveCategoryId(category, token, cancellationToken);
            if (string.IsNullOrWhiteSpace(gameId))
            {
                throw new PlatformOperationException($"Twitch category '{category}' was not found.");
            }

            payload["game_id"] = gameId;
        }

        if (payload.Count == 0)
        {
            return;
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"channels?broadcaster_id={Uri.EscapeDataString(profile.BroadcasterId)}")
        {
            Content = JsonContent.Create(payload)
        };

        ApplyAuthentication(request, token);

        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<TwitchEventSubSubscription>> GetEventSubscriptions(CancellationToken cancellationToken = default)
    {
        string? token = await _tokenManager.GetToken(cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            return [];
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, "eventsub/subscriptions");
        ApplyAuthentication(request, token);

        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        HelixEventSubSubscriptionsResponse? payload = await response.Content.ReadFromJsonAsync<HelixEventSubSubscriptionsResponse>(cancellationToken: cancellationToken);
        if (payload?.Data == null)
        {
            return [];
        }

        return payload.Data.Select(static subscription => new TwitchEventSubSubscription
        {
            Id = subscription.Id ?? string.Empty,
            Type = subscription.Type ?? string.Empty,
            Version = subscription.Version ?? string.Empty,
            Condition = subscription.Condition?.ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value.ValueKind == JsonValueKind.String ? pair.Value.GetString() ?? string.Empty : pair.Value.ToString(),
                StringComparer.Ordinal) ?? new Dictionary<string, string>(),
            SessionId = subscription.Transport?.SessionId ?? string.Empty
        }).ToArray();
    }

    public async Task CreateEventSubscription(
        TwitchEventSubSubscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string? token = await _tokenManager.GetToken(cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("Twitch is not authorized.");
        }

        var condition = request.Condition.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value,
            StringComparer.Ordinal);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "eventsub/subscriptions")
        {
            Content = JsonContent.Create(new CreateEventSubSubscriptionHttpRequest(
                request.Type,
                request.Version,
                condition,
                new CreateEventSubTransport("websocket", request.SessionId)))
        };

        ApplyAuthentication(httpRequest, token);

        using HttpResponseMessage response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            // Twitch explains rejected conditions and missing scopes in the body; without it the failure is undiagnosable.
            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new PlatformOperationException(
                $"Twitch rejected the {request.Type} EventSub subscription with {(int)response.StatusCode}: {body}");
        }
    }

    public async Task DeleteEventSubscription(string subscriptionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(subscriptionId))
        {
            return;
        }

        string? token = await _tokenManager.GetToken(cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("Twitch is not authorized.");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"eventsub/subscriptions?id={Uri.EscapeDataString(subscriptionId)}");
        ApplyAuthentication(request, token);

        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<TwitchUser?> GetAuthenticatedUser(CancellationToken cancellationToken = default)
    {
        string? token = await _tokenManager.GetToken(cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, "users");
        ApplyAuthentication(request, token);

        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        HelixUsersResponse? payload = await response.Content.ReadFromJsonAsync<HelixUsersResponse>(cancellationToken: cancellationToken);
        HelixUserData? userData = payload?.Data?.FirstOrDefault();
        
        if (userData == null)
        {
            return null;
        }

        return new TwitchUser
        {
            Id = userData.Id,
            Login = userData.Login,
            DisplayName = userData.DisplayName
        };
    }

    public async Task<TwitchUser?> GetUserByLogin(string login, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(login))
        {
            return null;
        }

        string? token = await _tokenManager.GetToken(cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"users?login={Uri.EscapeDataString(login)}");
        ApplyAuthentication(request, token);

        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        HelixUsersResponse? payload = await response.Content.ReadFromJsonAsync<HelixUsersResponse>(cancellationToken: cancellationToken);
        HelixUserData? userData = payload?.Data?.FirstOrDefault();

        if (userData == null)
        {
            return null;
        }

        return new TwitchUser
        {
            Id = userData.Id,
            Login = userData.Login,
            DisplayName = userData.DisplayName
        };
    }

    private async Task<string> ResolveCategoryId(string category, string token, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"games?name={Uri.EscapeDataString(category)}");

        ApplyAuthentication(request, token);

        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        HelixGamesResponse? payload = await response.Content.ReadFromJsonAsync<HelixGamesResponse>(cancellationToken: cancellationToken);
        HelixGameData? match = payload?.Data?
            .FirstOrDefault(game => string.Equals(game.Name, category, StringComparison.OrdinalIgnoreCase));

        return match?.Id ?? string.Empty;
    }

    private void ApplyAuthentication(HttpRequestMessage request, string token)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("Client-Id", _options.ClientId);
    }

    private sealed record SendChatMessageRequest(
        [property: JsonPropertyName("broadcaster_id")] string BroadcasterId,
        [property: JsonPropertyName("sender_id")] string SenderId,
        [property: JsonPropertyName("message")] string Message);

    private sealed record HelixSendMessageResponse(
        [property: JsonPropertyName("data")] IReadOnlyList<HelixSendMessageData>? Data);

    private sealed record HelixSendMessageData(
        [property: JsonPropertyName("message_id")] string? MessageId,
        [property: JsonPropertyName("is_sent")] bool IsSent,
        [property: JsonPropertyName("drop_reason")] HelixDropReason? DropReason);

    private sealed record HelixDropReason(
        [property: JsonPropertyName("code")] string? Code,
        [property: JsonPropertyName("message")] string? Message);

    private sealed record HelixStreamsResponse(
        [property: JsonPropertyName("data")] IReadOnlyList<HelixStreamData>? Data);

    private sealed record HelixStreamData(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("game_name")] string? GameName,
        [property: JsonPropertyName("tags")] IReadOnlyList<string>? Tags,
        [property: JsonPropertyName("started_at")] DateTimeOffset? StartedAt,
        [property: JsonPropertyName("viewer_count")] int ViewerCount);

    private sealed record HelixEventSubSubscriptionsResponse(
        [property: JsonPropertyName("data")] IReadOnlyList<HelixEventSubSubscriptionData>? Data);

    private sealed record HelixGamesResponse(
        [property: JsonPropertyName("data")] IReadOnlyList<HelixGameData>? Data);

    private sealed record HelixGameData(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("name")] string? Name);

    private sealed record HelixEventSubSubscriptionData(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("version")] string? Version,
        [property: JsonPropertyName("condition")] Dictionary<string, JsonElement>? Condition,
        [property: JsonPropertyName("transport")] HelixTransportData? Transport);

    private sealed record HelixTransportData(
        [property: JsonPropertyName("method")] string? Method,
        [property: JsonPropertyName("session_id")] string? SessionId);

    private sealed record CreateEventSubSubscriptionHttpRequest(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("version")] string Version,
        [property: JsonPropertyName("condition")] Dictionary<string, string> Condition,
        [property: JsonPropertyName("transport")] CreateEventSubTransport Transport);

    private sealed record CreateEventSubTransport(
        [property: JsonPropertyName("method")] string Method,
        [property: JsonPropertyName("session_id")] string SessionId);

    private sealed record HelixUsersResponse(
        [property: JsonPropertyName("data")] IReadOnlyList<HelixUserData>? Data);

    private sealed record HelixUserData(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("login")] string Login,
        [property: JsonPropertyName("display_name")] string DisplayName);
}
