using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.X;

namespace Thiccdal.Remote.X;

internal sealed class XApiClient : IXApiClient
{
    private readonly XOptions _options;
    private readonly HttpClient _httpClient;

    public XApiClient(
        IOptions<XOptions> options,
        IHttpClientFactory httpClientFactory)
    {
        _options = options.Value;
        _httpClient = httpClientFactory.CreateClient(XClientNames.Api);
    }

    public async Task<XReplyPollResult> PollReplies(string conversationId, string? sinceId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);

        List<XTweetReply> replies = [];
        Dictionary<string, XUserProfile> users = new(StringComparer.Ordinal);
        string? nextToken = null;
        string? newestReplyId = sinceId;
        XApiRateLimit rateLimit = new();

        do
        {
            string requestUri = BuildReplySearchRequestUri(conversationId, sinceId, nextToken);
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            ApplyReadAuthentication(request);

            using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            rateLimit = GetRateLimit(response);

            using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            JsonElement root = document.RootElement;
            users = ParseUsers(root);

            if (root.TryGetProperty("data", out JsonElement dataElement) && dataElement.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement tweetElement in dataElement.EnumerateArray())
                {
                    string id = GetRequiredString(tweetElement, "id");
                    string authorId = GetRequiredString(tweetElement, "author_id");
                    string text = GetOptionalString(tweetElement, "text");
                    DateTimeOffset createdAt = DateTimeOffset.Parse(
                        GetRequiredString(tweetElement, "created_at"),
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

                    if (!users.TryGetValue(authorId, out XUserProfile? author))
                    {
                        author = new XUserProfile
                        {
                            Id = authorId,
                            Username = authorId
                        };
                    }

                    replies.Add(new XTweetReply
                    {
                        Id = id,
                        AuthorId = authorId,
                        Text = text,
                        CreatedAt = createdAt,
                        Author = author
                    });

                    newestReplyId = GetNewestTweetId(newestReplyId, id);
                }
            }

            nextToken = GetNextToken(root);
        }
        while (!string.IsNullOrWhiteSpace(nextToken));

        return new XReplyPollResult
        {
            Replies = replies,
            NewestReplyId = newestReplyId,
            RateLimit = rateLimit
        };
    }

    public Task<XEngagementPollResult> GetLikingUsers(string tweetId, CancellationToken cancellationToken = default)
    {
        return GetUsers($"tweets/{Uri.EscapeDataString(tweetId)}/liking_users?user.fields=name,username&max_results=100", cancellationToken);
    }

    public Task<XEngagementPollResult> GetRepostedUsers(string tweetId, CancellationToken cancellationToken = default)
    {
        return GetUsers($"tweets/{Uri.EscapeDataString(tweetId)}/retweeted_by?user.fields=name,username&max_results=100", cancellationToken);
    }

    public async Task SendReply(string tweetId, string message, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tweetId);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        using var request = new HttpRequestMessage(HttpMethod.Post, "tweets")
        {
            Content = JsonContent.Create(new CreateTweetRequest(
                message,
                new ReplyRequest(tweetId)))
        };

        ApplyWriteAuthentication(request);

        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task<XEngagementPollResult> GetUsers(string requestUri, CancellationToken cancellationToken)
    {
        List<XUserProfile> users = [];
        string? nextToken = null;
        XApiRateLimit rateLimit = new();

        do
        {
            string requestPath = string.IsNullOrWhiteSpace(nextToken)
                ? requestUri
                : requestUri + "&pagination_token=" + Uri.EscapeDataString(nextToken);

            using var request = new HttpRequestMessage(HttpMethod.Get, requestPath);
            ApplyReadAuthentication(request);

            using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            rateLimit = GetRateLimit(response);

            using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            JsonElement root = document.RootElement;

            if (root.TryGetProperty("data", out JsonElement dataElement) && dataElement.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement userElement in dataElement.EnumerateArray())
                {
                    users.Add(new XUserProfile
                    {
                        Id = GetRequiredString(userElement, "id"),
                        Name = GetOptionalString(userElement, "name"),
                        Username = GetOptionalString(userElement, "username")
                    });
                }
            }

            nextToken = GetNextToken(root);
        }
        while (!string.IsNullOrWhiteSpace(nextToken));

        return new XEngagementPollResult
        {
            Users = users,
            RateLimit = rateLimit
        };
    }

    private string BuildReplySearchRequestUri(string conversationId, string? sinceId, string? nextToken)
    {
        StringBuilder builder = new();
        builder.Append("tweets/search/recent?query=");
        builder.Append(Uri.EscapeDataString($"conversation_id:{conversationId}"));
        builder.Append("&tweet.fields=created_at,author_id,text");
        builder.Append("&expansions=author_id");
        builder.Append("&user.fields=name,username");
        builder.Append("&max_results=100");

        if (!string.IsNullOrWhiteSpace(sinceId))
        {
            builder.Append("&since_id=");
            builder.Append(Uri.EscapeDataString(sinceId));
        }

        if (!string.IsNullOrWhiteSpace(nextToken))
        {
            builder.Append("&next_token=");
            builder.Append(Uri.EscapeDataString(nextToken));
        }

        return builder.ToString();
    }

    private void ApplyReadAuthentication(HttpRequestMessage request)
    {
        if (!string.IsNullOrWhiteSpace(_options.BearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.BearerToken);
            return;
        }

        if (HasUserContextCredentials())
        {
            ApplyOAuthAuthentication(request);
            return;
        }

        throw new InvalidOperationException("X polling requires either a bearer token or OAuth 1.0a user credentials.");
    }

    private void ApplyWriteAuthentication(HttpRequestMessage request)
    {
        if (!HasUserContextCredentials())
        {
            throw new InvalidOperationException("X write operations require ApiKey, ApiKeySecret, AccessToken, and AccessTokenSecret.");
        }

        ApplyOAuthAuthentication(request);
    }

    private void ApplyOAuthAuthentication(HttpRequestMessage request)
    {
        request.Headers.TryAddWithoutValidation("Authorization", BuildOAuthHeader(request));
    }

    private string BuildOAuthHeader(HttpRequestMessage request)
    {
        Uri requestUri = request.RequestUri?.IsAbsoluteUri == true
            ? request.RequestUri
            : new Uri(_httpClient.BaseAddress!, request.RequestUri ?? throw new InvalidOperationException("Request URI is required."));

        Dictionary<string, string> oauthParameters = new(StringComparer.Ordinal)
        {
            ["oauth_consumer_key"] = _options.ApiKey,
            ["oauth_nonce"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)),
            ["oauth_signature_method"] = "HMAC-SHA1",
            ["oauth_timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            ["oauth_token"] = _options.AccessToken,
            ["oauth_version"] = "1.0"
        };

        List<KeyValuePair<string, string>> signatureParameters = [];
        foreach (KeyValuePair<string, string> parameter in oauthParameters)
        {
            signatureParameters.Add(parameter);
        }

        foreach (KeyValuePair<string, string> queryParameter in ParseQueryString(requestUri.Query))
        {
            signatureParameters.Add(queryParameter);
        }

        string normalizedParameters = string.Join(
            "&",
            signatureParameters
                .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
                .ThenBy(static pair => pair.Value, StringComparer.Ordinal)
                .Select(static pair => $"{Escape(pair.Key)}={Escape(pair.Value)}"));

        string baseUri = requestUri.GetLeftPart(UriPartial.Path);
        string signatureBaseString = string.Join(
            "&",
            request.Method.Method.ToUpperInvariant(),
            Escape(baseUri),
            Escape(normalizedParameters));

        string signingKey = $"{Escape(_options.ApiKeySecret)}&{Escape(_options.AccessTokenSecret)}";
        using var hasher = new HMACSHA1(Encoding.ASCII.GetBytes(signingKey));
        string signature = Convert.ToBase64String(hasher.ComputeHash(Encoding.ASCII.GetBytes(signatureBaseString)));
        oauthParameters["oauth_signature"] = signature;

        return "OAuth " + string.Join(
            ", ",
            oauthParameters
                .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
                .Select(static pair => $"{Escape(pair.Key)}=\"{Escape(pair.Value)}\""));
    }

    private bool HasUserContextCredentials()
    {
        return !string.IsNullOrWhiteSpace(_options.ApiKey)
            && !string.IsNullOrWhiteSpace(_options.ApiKeySecret)
            && !string.IsNullOrWhiteSpace(_options.AccessToken)
            && !string.IsNullOrWhiteSpace(_options.AccessTokenSecret);
    }

    private static Dictionary<string, XUserProfile> ParseUsers(JsonElement root)
    {
        Dictionary<string, XUserProfile> users = new(StringComparer.Ordinal);

        if (!root.TryGetProperty("includes", out JsonElement includesElement) ||
            !includesElement.TryGetProperty("users", out JsonElement usersElement) ||
            usersElement.ValueKind != JsonValueKind.Array)
        {
            return users;
        }

        foreach (JsonElement userElement in usersElement.EnumerateArray())
        {
            string id = GetRequiredString(userElement, "id");
            users[id] = new XUserProfile
            {
                Id = id,
                Name = GetOptionalString(userElement, "name"),
                Username = GetOptionalString(userElement, "username")
            };
        }

        return users;
    }

    private static string? GetNextToken(JsonElement root)
    {
        if (!root.TryGetProperty("meta", out JsonElement metaElement) ||
            !metaElement.TryGetProperty("next_token", out JsonElement nextTokenElement) ||
            nextTokenElement.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return nextTokenElement.GetString();
    }

    private static XApiRateLimit GetRateLimit(HttpResponseMessage response)
    {
        return new XApiRateLimit
        {
            Remaining = TryGetIntegerHeader(response, "x-rate-limit-remaining"),
            ResetAt = TryGetIntegerHeader(response, "x-rate-limit-reset") is int resetSeconds
                ? DateTimeOffset.FromUnixTimeSeconds(resetSeconds)
                : null
        };
    }

    private static int? TryGetIntegerHeader(HttpResponseMessage response, string headerName)
    {
        if (!response.Headers.TryGetValues(headerName, out IEnumerable<string>? values))
        {
            return null;
        }

        string? firstValue = values.FirstOrDefault();
        if (int.TryParse(firstValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedValue))
        {
            return parsedValue;
        }

        return null;
    }

    private static string GetRequiredString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out JsonElement propertyElement)
            ? propertyElement.GetString() ?? string.Empty
            : string.Empty;
    }

    private static string GetOptionalString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out JsonElement propertyElement)
            ? propertyElement.GetString() ?? string.Empty
            : string.Empty;
    }

    private static string GetNewestTweetId(string? currentId, string candidateId)
    {
        if (string.IsNullOrWhiteSpace(currentId))
        {
            return candidateId;
        }

        if (ulong.TryParse(currentId, CultureInfo.InvariantCulture, out ulong currentValue)
            && ulong.TryParse(candidateId, CultureInfo.InvariantCulture, out ulong candidateValue))
        {
            return candidateValue > currentValue ? candidateId : currentId;
        }

        return string.CompareOrdinal(candidateId, currentId) > 0 ? candidateId : currentId;
    }

    private static IEnumerable<KeyValuePair<string, string>> ParseQueryString(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            yield break;
        }

        string trimmedQuery = query.StartsWith('?') ? query[1..] : query;
        foreach (string pair in trimmedQuery.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] segments = pair.Split('=', 2);
            string key = Uri.UnescapeDataString(segments[0]);
            string value = segments.Length > 1 ? Uri.UnescapeDataString(segments[1]) : string.Empty;
            yield return new KeyValuePair<string, string>(key, value);
        }
    }

    private static string Escape(string value)
    {
        return Uri.EscapeDataString(value)
            .Replace("!", "%21", StringComparison.Ordinal)
            .Replace("*", "%2A", StringComparison.Ordinal)
            .Replace("'", "%27", StringComparison.Ordinal)
            .Replace("(", "%28", StringComparison.Ordinal)
            .Replace(")", "%29", StringComparison.Ordinal);
    }

    private sealed record CreateTweetRequest(
        [property: JsonPropertyName("text")] string Text,
        [property: JsonPropertyName("reply")] ReplyRequest Reply);

    private sealed record ReplyRequest(
        [property: JsonPropertyName("in_reply_to_tweet_id")] string InReplyToTweetId);
}
