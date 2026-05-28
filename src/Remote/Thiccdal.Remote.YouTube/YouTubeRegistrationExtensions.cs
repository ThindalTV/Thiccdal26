using System.Net.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Thiccdal.Infrastructure.Integrations;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Infrastructure.YouTube;

namespace Thiccdal.Remote.YouTube;

public static class YouTubeRegistrationExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddYouTubeIntegration(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            var youTubeSection = configuration.GetSection(YouTubeOptions.SectionName);

            services.AddOptions<YouTubeOptions>()
                .Bind(youTubeSection)
                .Validate(
                    static options => Uri.TryCreate(options.OAuthBaseAddress, UriKind.Absolute, out Uri? oauthUri)
                        && (oauthUri.Scheme == Uri.UriSchemeHttps || oauthUri.Scheme == Uri.UriSchemeHttp),
                    "YouTube OAuthBaseAddress must be an absolute HTTP or HTTPS URI.")
                .Validate(
                    static options => Uri.TryCreate(options.ApiBaseAddress, UriKind.Absolute, out Uri? apiUri)
                        && (apiUri.Scheme == Uri.UriSchemeHttps || apiUri.Scheme == Uri.UriSchemeHttp),
                    "YouTube ApiBaseAddress must be an absolute HTTP or HTTPS URI.")
                .Validate(
                    static options => options.LiveChatPollingIntervalSeconds > 0,
                    "YouTube LiveChatPollingIntervalSeconds must be greater than zero.")
                .Validate(
                    static options => options.BroadcastInfoRefreshSeconds > 0,
                    "YouTube BroadcastInfoRefreshSeconds must be greater than zero.");

            services.AddHttpClient(
                YouTubeClientNames.OAuth,
                static (serviceProvider, client) =>
                {
                    YouTubeOptions options = serviceProvider.GetRequiredService<IOptions<YouTubeOptions>>().Value;
                    client.BaseAddress = CreateBaseAddress(options.OAuthBaseAddress);
                })
                .AddResilienceHandler(
                    "youtube-oauth-retry",
                    static (builder, context) => builder.AddRetry(CreateRetryStrategyOptions(context, "YouTube OAuth", 5, false)));
            services.AddHttpClient(
                YouTubeClientNames.Api,
                static (serviceProvider, client) =>
                {
                    YouTubeOptions options = serviceProvider.GetRequiredService<IOptions<YouTubeOptions>>().Value;
                    client.BaseAddress = CreateBaseAddress(options.ApiBaseAddress);
                })
                .AddResilienceHandler(
                    "youtube-api-retry",
                    static (builder, context) => builder.AddRetry(CreateRetryStrategyOptions(context, "YouTube API", 5, true)));

            services.AddSingleton<IYouTubeTokenManager, YouTubeTokenManager>();
            services.AddSingleton<IYouTubeApiClient, YouTubeApiClient>();
            services.AddSingleton<YouTubeLiveChatMessageMapper>();

            services.AddSingleton<YouTubeService>();
            services.AddSingleton<IYouTubePlatformConnection>(sp => sp.GetRequiredService<YouTubeService>());
            services.AddSingleton<IPlatformConnection>(sp => sp.GetRequiredService<YouTubeService>());
            services.AddSingleton<IChatSource>(sp => sp.GetRequiredService<YouTubeService>());
            services.AddSingleton<IStreamTarget>(sp => sp.GetRequiredService<YouTubeService>());
            services.AddSingleton<IEventSource>(sp => sp.GetRequiredService<YouTubeService>());
            services.AddSingleton<IPlatformEventSource>(sp => sp.GetRequiredService<YouTubeService>());
            services.AddSingleton<IYouTubeService>(sp => sp.GetRequiredService<YouTubeService>());
            services.AddSingleton<IStreamInfoProvider>(sp => sp.GetRequiredService<YouTubeService>());

            services.AddSingleton<YouTubeConnectionMonitor>();
            services.AddSingleton<IYouTubeConnectionMonitor>(sp => sp.GetRequiredService<YouTubeConnectionMonitor>());
            services.AddSingleton<IIntegrationConnectionMonitor>(sp => sp.GetRequiredService<YouTubeConnectionMonitor>());

            return services;
        }

        public IServiceCollection AddYouTubePlatform(IConfiguration configuration)
        {
            return services.AddYouTubeIntegration(configuration);
        }
    }

    extension(IEndpointRouteBuilder endpoints)
    {
        public IEndpointRouteBuilder MapYouTubeEndpoints()
        {
            ArgumentNullException.ThrowIfNull(endpoints);

            endpoints.MapGet("/auth/youtube/callback", HandleOAuthCallback)
                .WithName("YouTubeOAuthCallback");

            return endpoints;
        }
    }

    private static async Task<IResult> HandleOAuthCallback(
        string? code,
        string? state,
        string? error,
        string? error_description,
        IYouTubeTokenManager tokenManager,
        IYouTubeService youTubeService,
        IYouTubeConnectionMonitor connectionMonitor,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("Thiccdal.Remote.YouTube.Callback");

        if (!string.IsNullOrEmpty(error))
        {
            logger.LogWarning("YouTube OAuth callback returned error: {Error} — {Description}", error, error_description);
            return Results.Redirect("/youtube/connect?error=oauth_denied");
        }

        if (string.IsNullOrEmpty(code))
        {
            logger.LogWarning("YouTube OAuth callback received no code and no error — possible misconfiguration");
            return Results.Redirect("/youtube/connect?error=missing_code");
        }

        if (string.IsNullOrEmpty(state) || !tokenManager.ValidateAndConsumeState(state))
        {
            logger.LogWarning("YouTube OAuth callback state validation failed — possible CSRF attempt (state={State})", state);
            return Results.Redirect("/youtube/connect?error=invalid_state");
        }

        await tokenManager.StoreToken(code, cancellationToken);
        await youTubeService.RefreshConnectionState(cancellationToken);
        await connectionMonitor.RefreshConnectionState(cancellationToken);

        return Results.Redirect("/youtube/connect");
    }

    private static Uri CreateBaseAddress(string baseAddress)
    {
        string normalizedBaseAddress = baseAddress.EndsWith('/') ? baseAddress : $"{baseAddress}/";
        return new Uri(normalizedBaseAddress, UriKind.Absolute);
    }

    private static HttpRetryStrategyOptions CreateRetryStrategyOptions(
        ResilienceHandlerContext context,
        string clientName,
        int maxRetryAttempts,
        bool useRetryAfterHeader)
    {
        ILogger logger = context.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Thiccdal.Remote.YouTube.HttpResilience");

        return new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = maxRetryAttempts,
            Delay = TimeSpan.FromMilliseconds(100),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            ShouldRetryAfterHeader = useRetryAfterHeader,
            OnRetry = args =>
            {
                logger.LogWarning(
                    "{ClientName} HTTP retry {Attempt} after {Delay} because {Reason}",
                    clientName,
                    args.AttemptNumber + 1,
                    args.RetryDelay,
                    DescribeRetryReason(args.Outcome));
                return default;
            }
        };
    }

    private static string DescribeRetryReason(Outcome<HttpResponseMessage> outcome)
    {
        if (outcome.Exception is not null)
        {
            return outcome.Exception.Message;
        }

        if (outcome.Result is not null)
        {
            return $"HTTP {(int)outcome.Result.StatusCode}";
        }

        return "unknown failure";
    }
}
