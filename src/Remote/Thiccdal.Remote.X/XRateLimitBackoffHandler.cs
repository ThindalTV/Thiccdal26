using System.Net;
using Microsoft.Extensions.Logging;

namespace Thiccdal.Remote.X;

internal sealed class XRateLimitBackoffHandler : DelegatingHandler
{
    private static readonly HttpRequestOptionsKey<bool> RateLimitRetryKey = new("Thiccdal.Remote.X.RateLimitRetried");

    private readonly ILogger<XRateLimitBackoffHandler> _logger;

    public XRateLimitBackoffHandler(ILogger<XRateLimitBackoffHandler> logger)
    {
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode != HttpStatusCode.TooManyRequests ||
            request.Options.TryGetValue(RateLimitRetryKey, out bool alreadyRetried) && alreadyRetried ||
            !TryGetRateLimitDelay(response, out TimeSpan delay))
        {
            return response;
        }

        _logger.LogWarning(
            "X API rate limit hit on {RequestUri}; backing off for {Delay} before retrying.",
            request.RequestUri,
            delay);

        HttpRequestMessage retryRequest = await CloneRequest(request, cancellationToken);
        retryRequest.Options.Set(RateLimitRetryKey, true);

        response.Dispose();

        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, cancellationToken);
        }

        return await base.SendAsync(retryRequest, cancellationToken);
    }

    private static bool TryGetRateLimitDelay(HttpResponseMessage response, out TimeSpan delay)
    {
        delay = TimeSpan.Zero;

        if (!response.Headers.TryGetValues("x-rate-limit-reset", out IEnumerable<string>? values))
        {
            return false;
        }

        string? rawValue = values.FirstOrDefault();
        if (!long.TryParse(rawValue, out long resetUnixSeconds))
        {
            return false;
        }

        DateTimeOffset resetAt = DateTimeOffset.FromUnixTimeSeconds(resetUnixSeconds);
        delay = resetAt - DateTimeOffset.UtcNow;
        if (delay < TimeSpan.Zero)
        {
            delay = TimeSpan.Zero;
        }

        return true;
    }

    private static async Task<HttpRequestMessage> CloneRequest(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpRequestMessage clone = new(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy
        };

        foreach (KeyValuePair<string, IEnumerable<string>> header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (request.Content is not null)
        {
            byte[] content = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            ByteArrayContent clonedContent = new(content);

            foreach (KeyValuePair<string, IEnumerable<string>> header in request.Content.Headers)
            {
                clonedContent.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            clone.Content = clonedContent;
        }

        return clone;
    }
}
