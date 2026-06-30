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
using Thiccdal.Infrastructure.Twitch;

namespace Thiccdal.Remote.Twitch;

public static class TwitchRegistrationExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddTwitchIntegration(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            var twitchSection = configuration.GetSection(TwitchOptions.SectionName);
            var helixSection = twitchSection.GetSection(nameof(TwitchOptions.Helix));
            var eventSubSection = twitchSection.GetSection(nameof(TwitchOptions.EventSub));

            services.AddOptions<TwitchOptions>()
                .Bind(twitchSection)
                .Validate(
                    static options => Uri.TryCreate(options.OAuthBaseAddress, UriKind.Absolute, out Uri? oauthUri)
                        && (oauthUri.Scheme == Uri.UriSchemeHttps || oauthUri.Scheme == Uri.UriSchemeHttp),
                    "Twitch OAuthBaseAddress must be an absolute HTTP or HTTPS URI.")
                .Validate(
                    static options => Uri.TryCreate(options.Helix.BaseAddress, UriKind.Absolute, out Uri? helixUri)
                        && (helixUri.Scheme == Uri.UriSchemeHttps || helixUri.Scheme == Uri.UriSchemeHttp),
                    "Twitch Helix:BaseAddress must be an absolute HTTP or HTTPS URI.")
                .Validate(
                    static options => Uri.TryCreate(options.EventSub.WebSocketUrl, UriKind.Absolute, out Uri? eventSubUri)
                        && (eventSubUri.Scheme == Uri.UriSchemeWss || eventSubUri.Scheme == Uri.UriSchemeWs),
                    "Twitch EventSub:WebSocketUrl must be an absolute WS or WSS URI.")
                .Validate(
                    static options => options.Helix.StreamStateRefreshSeconds > 0,
                    "Twitch Helix:StreamStateRefreshSeconds must be greater than zero.")
                .Validate(
                    static options => options.EventSub.ReconnectDelaySeconds > 0,
                    "Twitch EventSub:ReconnectDelaySeconds must be greater than zero.");

            services.AddOptions<TwitchHelixOptions>()
                .Bind(helixSection)
                .Validate(
                    static options => Uri.TryCreate(options.BaseAddress, UriKind.Absolute, out Uri? helixUri)
                        && (helixUri.Scheme == Uri.UriSchemeHttps || helixUri.Scheme == Uri.UriSchemeHttp),
                    "Twitch Helix:BaseAddress must be an absolute HTTP or HTTPS URI.")
                .Validate(
                    static options => options.StreamStateRefreshSeconds > 0,
                    "Twitch Helix:StreamStateRefreshSeconds must be greater than zero.");

            services.AddOptions<TwitchEventSubOptions>()
                .Bind(eventSubSection)
                .Validate(
                    static options => Uri.TryCreate(options.WebSocketUrl, UriKind.Absolute, out Uri? eventSubUri)
                        && (eventSubUri.Scheme == Uri.UriSchemeWss || eventSubUri.Scheme == Uri.UriSchemeWs),
                    "Twitch EventSub:WebSocketUrl must be an absolute WS or WSS URI.")
                .Validate(
                    static options => options.ReconnectDelaySeconds > 0,
                    "Twitch EventSub:ReconnectDelaySeconds must be greater than zero.");

            services.AddHttpClient(
                TwitchClientNames.OAuth,
                static (serviceProvider, client) =>
                {
                    TwitchOptions options = serviceProvider.GetRequiredService<IOptions<TwitchOptions>>().Value;
                    client.BaseAddress = CreateBaseAddress(options.OAuthBaseAddress);
                })
                .AddResilienceHandler(
                    "twitch-oauth-retry",
                    static (builder, context) => builder.AddRetry(CreateRetryStrategyOptions(context, "Twitch OAuth", 5)));
            services.AddHttpClient(
                TwitchClientNames.Helix,
                static (serviceProvider, client) =>
                {
                    TwitchOptions options = serviceProvider.GetRequiredService<IOptions<TwitchOptions>>().Value;
                    client.BaseAddress = CreateBaseAddress(options.Helix.BaseAddress);
                })
                .AddResilienceHandler(
                    "twitch-helix-retry",
                    static (builder, context) => builder.AddRetry(CreateRetryStrategyOptions(context, "Twitch Helix", 5)));

            services.AddSingleton<ITwitchTokenManager, TwitchTokenManager>();
            services.AddSingleton<IEmoteRenderingOptions>(sp =>
                new EmoteRenderingOptions(sp.GetRequiredService<IOptions<TwitchEventSubOptions>>().Value.UseAnimatedEmotes));
            services.AddSingleton<ITwitchHelixClient, TwitchHelixClient>();
            services.AddSingleton<TwitchEventSubNotificationMapper>();
            services.AddSingleton<ITwitchEventSubClient, TwitchEventSubClient>();
            services.AddSingleton<TwitchTargetChannelService>();
            services.AddSingleton<ITwitchTargetChannelService>(sp => sp.GetRequiredService<TwitchTargetChannelService>());

            services.AddSingleton<TwitchService>();
            services.AddSingleton<IPlatformConnection>(sp => sp.GetRequiredService<TwitchService>());
            services.AddSingleton<IChatSource>(sp => sp.GetRequiredService<TwitchService>());
            services.AddSingleton<IStreamTarget>(sp => sp.GetRequiredService<TwitchService>());
            services.AddSingleton<IEventSource>(sp => sp.GetRequiredService<TwitchService>());
            services.AddSingleton<IPlatformEventSource>(sp => sp.GetRequiredService<TwitchService>());
            services.AddSingleton<ITwitchService>(sp => sp.GetRequiredService<TwitchService>());
            services.AddSingleton<IStreamInfoProvider>(sp => sp.GetRequiredService<TwitchService>());

            services.AddSingleton<TwitchConnectionMonitor>();
            services.AddSingleton<ITwitchConnectionMonitor>(sp => sp.GetRequiredService<TwitchConnectionMonitor>());
            services.AddSingleton<IIntegrationConnectionMonitor>(sp => sp.GetRequiredService<TwitchConnectionMonitor>());

            services.AddSingleton<TwitchStreamInfoService>();
            services.AddSingleton<ITwitchStreamInfoService>(sp => sp.GetRequiredService<TwitchStreamInfoService>());
            services.AddHostedService(static sp => sp.GetRequiredService<TwitchStreamInfoService>());

            return services;
        }
    }

    extension(IEndpointRouteBuilder endpoints)
    {
        public IEndpointRouteBuilder MapTwitchEndpoints()
        {
            ArgumentNullException.ThrowIfNull(endpoints);

            endpoints.MapGet("/auth/twitch/callback", HandleOAuthCallback)
                .WithName("TwitchOAuthCallback");

            return endpoints;
        }
    }

    private static async Task<IResult> HandleOAuthCallback(
        string? code,
        string? state,
        string? error,
        string? error_description,
        ITwitchTokenManager tokenManager,
        ITwitchService twitchService,
        ITwitchConnectionMonitor connectionMonitor,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("Thiccdal.Remote.Twitch.Callback");

        if (!string.IsNullOrEmpty(error))
        {
            logger.LogWarning("Twitch OAuth callback returned error: {Error} — {Description}", error, error_description);
            return Results.Redirect("/dashboard?twitch_error=oauth_denied");
        }

        if (string.IsNullOrEmpty(code))
        {
            logger.LogWarning("Twitch OAuth callback received no code and no error — possible misconfiguration");
            return Results.Redirect("/dashboard?twitch_error=missing_code");
        }

        if (string.IsNullOrEmpty(state) || !tokenManager.ValidateAndConsumeState(state))
        {
            logger.LogWarning("Twitch OAuth callback state validation failed — possible CSRF attempt (state={State})", state);
            return Results.Redirect("/dashboard?twitch_error=invalid_state");
        }

        await tokenManager.StoreToken(code, cancellationToken);
        await twitchService.RefreshConnectionState(cancellationToken);
        await connectionMonitor.RefreshConnectionState(cancellationToken);

        return Results.Redirect("/dashboard");
    }

    private static Uri CreateBaseAddress(string baseAddress)
    {
        string normalizedBaseAddress = baseAddress.EndsWith('/') ? baseAddress : $"{baseAddress}/";
        return new Uri(normalizedBaseAddress, UriKind.Absolute);
    }

    private static HttpRetryStrategyOptions CreateRetryStrategyOptions(
        ResilienceHandlerContext context,
        string clientName,
        int maxRetryAttempts)
    {
        ILogger logger = context.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Thiccdal.Remote.Twitch.HttpResilience");

        return new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = maxRetryAttempts,
            Delay = TimeSpan.FromMilliseconds(100),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
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
