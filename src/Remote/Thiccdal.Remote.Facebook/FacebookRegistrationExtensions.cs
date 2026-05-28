using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Thiccdal.Infrastructure.Facebook;
using Thiccdal.Infrastructure.Integrations;
using Thiccdal.Infrastructure.Remotes;
using Polly;

namespace Thiccdal.Remote.Facebook;

public static class FacebookRegistrationExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddFacebookIntegration(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            var facebookSection = configuration.GetSection(FacebookOptions.SectionName);

            services.AddOptions<FacebookOptions>()
                .Bind(facebookSection)
                .Validate(
                    static options => Uri.TryCreate(options.OAuthBaseAddress, UriKind.Absolute, out Uri? oauthUri)
                        && (oauthUri.Scheme == Uri.UriSchemeHttps || oauthUri.Scheme == Uri.UriSchemeHttp),
                    "Facebook OAuthBaseAddress must be an absolute HTTP or HTTPS URI.")
                .Validate(
                    static options => Uri.TryCreate(options.GraphApiBaseAddress, UriKind.Absolute, out Uri? graphUri)
                        && (graphUri.Scheme == Uri.UriSchemeHttps || graphUri.Scheme == Uri.UriSchemeHttp),
                    "Facebook GraphApiBaseAddress must be an absolute HTTP or HTTPS URI.")
                .Validate(
                    static options => !string.IsNullOrWhiteSpace(options.DefaultPrivacy),
                    "Facebook DefaultPrivacy must be configured.")
                .Validate(
                    static options => options.PollIntervalMs > 0,
                    "Facebook PollIntervalMs must be greater than zero.")
                .Validate(
                    static options => options.ReconnectDelaySeconds > 0,
                    "Facebook ReconnectDelaySeconds must be greater than zero.");

            services.AddHttpClient(
                FacebookClientNames.GraphApi,
                static (serviceProvider, client) =>
                {
                    FacebookOptions options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<FacebookOptions>>().Value;
                    client.BaseAddress = CreateGraphApiBaseAddress(options);
                })
                .AddResilienceHandler(
                    "facebook-graph-retry",
                    static (builder, context) => builder.AddRetry(CreateRetryStrategyOptions(context, "Facebook Graph API", 3)));

            services.AddSingleton<IFacebookGraphClient, FacebookGraphClient>();
            services.AddSingleton<FacebookService>();
            services.AddSingleton<IPlatformConnection>(sp => sp.GetRequiredService<FacebookService>());
            services.AddSingleton<IChatSource>(sp => sp.GetRequiredService<FacebookService>());
            services.AddSingleton<IStreamTarget>(sp => sp.GetRequiredService<FacebookService>());
            services.AddSingleton<IEventSource>(sp => sp.GetRequiredService<FacebookService>());
            services.AddSingleton<IPlatformEventSource>(sp => sp.GetRequiredService<FacebookService>());
            services.AddSingleton<IFacebookService>(sp => sp.GetRequiredService<FacebookService>());

            services.AddSingleton<FacebookConnectionMonitor>();
            services.AddSingleton<IFacebookConnectionMonitor>(sp => sp.GetRequiredService<FacebookConnectionMonitor>());
            services.AddSingleton<IIntegrationConnectionMonitor>(sp => sp.GetRequiredService<FacebookConnectionMonitor>());

            return services;
        }
    }

    private static Uri CreateGraphApiBaseAddress(FacebookOptions options)
    {
        string normalizedBaseAddress = options.GraphApiBaseAddress.EndsWith('/')
            ? options.GraphApiBaseAddress
            : $"{options.GraphApiBaseAddress}/";

        return new Uri($"{normalizedBaseAddress}{options.GraphApiVersion.Trim('/')}/", UriKind.Absolute);
    }

    private static HttpRetryStrategyOptions CreateRetryStrategyOptions(
        ResilienceHandlerContext context,
        string clientName,
        int maxRetryAttempts)
    {
        ILogger logger = context.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Thiccdal.Remote.Facebook.HttpResilience");

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
