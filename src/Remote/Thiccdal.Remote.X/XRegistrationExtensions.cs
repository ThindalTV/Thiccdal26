using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.Integrations;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Infrastructure.X;
using Polly;

namespace Thiccdal.Remote.X;

public static class XRegistrationExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddXIntegration(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            var xSection = configuration.GetSection(XOptions.SectionName);

            services.AddOptions<XOptions>()
                .Bind(xSection)
                .Validate(
                    static options => Uri.TryCreate(options.OAuthBaseAddress, UriKind.Absolute, out Uri? oauthUri)
                        && (oauthUri.Scheme == Uri.UriSchemeHttps || oauthUri.Scheme == Uri.UriSchemeHttp),
                    "X OAuthBaseAddress must be an absolute HTTP or HTTPS URI.")
                .Validate(
                    static options => Uri.TryCreate(options.ApiBaseAddress, UriKind.Absolute, out Uri? apiUri)
                        && (apiUri.Scheme == Uri.UriSchemeHttps || apiUri.Scheme == Uri.UriSchemeHttp),
                    "X ApiBaseAddress must be an absolute HTTP or HTTPS URI.")
                .Validate(
                    static options => options.TweetPollingIntervalSeconds > 0,
                    "X TweetPollingIntervalSeconds must be greater than zero.")
                .Validate(
                    static options => options.PollIntervalMs > 0,
                    "X PollIntervalMs must be greater than zero.")
                .Validate(
                    static options => options.LikesPollIntervalMs > 0,
                    "X LikesPollIntervalMs must be greater than zero.")
                .Validate(
                    static options => options.ReconnectDelaySeconds > 0,
                    "X ReconnectDelaySeconds must be greater than zero.")
                .Validate(
                    static options => Uri.TryCreate(options.AuthorizationUrl, UriKind.Absolute, out Uri? authorizationUri)
                        && (authorizationUri.Scheme == Uri.UriSchemeHttps || authorizationUri.Scheme == Uri.UriSchemeHttp),
                    "X AuthorizationUrl must be an absolute HTTP or HTTPS URI.");

            services.AddHttpClient(
                XClientNames.Api,
                static (serviceProvider, client) =>
                {
                    XOptions options = serviceProvider.GetRequiredService<IOptions<XOptions>>().Value;
                    client.BaseAddress = CreateBaseAddress(options.ApiBaseAddress, options.ApiVersion);
                })
                .AddHttpMessageHandler<XRateLimitBackoffHandler>()
                .AddResilienceHandler(
                    "x-api-retry",
                    static (builder, context) => builder.AddRetry(CreateRetryStrategyOptions(context, "X API", 5)));

            services.AddTransient<XRateLimitBackoffHandler>();
            services.AddSingleton<IXApiClient, XApiClient>();

            services.AddSingleton<XService>();
            services.AddSingleton<IPlatformConnection>(sp => sp.GetRequiredService<XService>());
            services.AddSingleton<IChatSource>(sp => sp.GetRequiredService<XService>());
            services.AddSingleton<IStreamTarget>(sp => sp.GetRequiredService<XService>());
            services.AddSingleton<IEventSource>(sp => sp.GetRequiredService<XService>());
            services.AddSingleton<IPlatformEventSource>(sp => sp.GetRequiredService<XService>());
            services.AddSingleton<IXService>(sp => sp.GetRequiredService<XService>());

            services.AddSingleton<XConnectionMonitor>();
            services.AddSingleton<IXConnectionMonitor>(sp => sp.GetRequiredService<XConnectionMonitor>());
            services.AddSingleton<IIntegrationConnectionMonitor>(sp => sp.GetRequiredService<XConnectionMonitor>());

            return services;
        }
    }

    private static Uri CreateBaseAddress(string baseAddress, string apiVersion)
    {
        string normalizedBaseAddress = baseAddress.EndsWith('/') ? baseAddress : $"{baseAddress}/";
        string normalizedVersion = apiVersion.Trim('/');
        return new Uri($"{normalizedBaseAddress}{normalizedVersion}/", UriKind.Absolute);
    }

    private static HttpRetryStrategyOptions CreateRetryStrategyOptions(
        ResilienceHandlerContext context,
        string clientName,
        int maxRetryAttempts)
    {
        ILogger logger = context.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Thiccdal.Remote.X.HttpResilience");

        return new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = maxRetryAttempts,
            Delay = TimeSpan.FromMilliseconds(100),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                .Handle<HttpRequestException>()
                .HandleResult(static response =>
                    response.StatusCode == HttpStatusCode.RequestTimeout ||
                    (int)response.StatusCode >= 500),
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
