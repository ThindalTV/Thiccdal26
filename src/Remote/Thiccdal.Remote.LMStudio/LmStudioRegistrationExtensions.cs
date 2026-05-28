using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.LmStudio;
using Thiccdal.Infrastructure.Questions;

namespace Thiccdal.Remote.LMStudio;

/// <summary>
/// Registers reusable LM Studio services.
/// </summary>
public static class LmStudioRegistrationExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Adds the reusable LM Studio client and question detection services.
        /// </summary>
        /// <param name="configuration">The application configuration root.</param>
        /// <returns>The service collection for chaining.</returns>
        public IServiceCollection AddLmStudioIntegration(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            IConfigurationSection lmStudioSection = configuration.GetSection(LmStudioOptions.SectionName);
            IConfigurationSection questionDetectionSection = configuration.GetSection(LmStudioQuestionDetectionOptions.SectionName);

            services.AddOptions<LmStudioOptions>()
                .Bind(lmStudioSection)
                .Validate(
                    static options => Uri.TryCreate(options.BaseAddress, UriKind.Absolute, out Uri? uri)
                        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps),
                    "LMStudio:BaseAddress must be an absolute HTTP or HTTPS URI.")
                .Validate(
                    static options => options.RequestTimeoutSeconds > 0,
                    "LMStudio:RequestTimeoutSeconds must be greater than zero.");

            services.AddOptions<LmStudioQuestionDetectionOptions>()
                .Bind(questionDetectionSection)
                .Validate(
                    static options => !options.Enabled || !string.IsNullOrWhiteSpace(options.Model),
                    "LMStudio:QuestionDetection:Model is required when question detection is enabled.")
                .Validate(
                    static options => options.MaxTokens > 0,
                    "LMStudio:QuestionDetection:MaxTokens must be greater than zero.")
                .Validate(
                    static options => options.Temperature >= 0d && options.Temperature <= 2d,
                    "LMStudio:QuestionDetection:Temperature must be between 0 and 2.")
                .Validate(
                    static options => !options.Enabled || !string.IsNullOrWhiteSpace(options.SystemPrompt),
                    "LMStudio:QuestionDetection:SystemPrompt is required when question detection is enabled.")
                .Validate(
                    static options => !options.Enabled || !string.IsNullOrWhiteSpace(options.UserPromptTemplate)
                        && options.UserPromptTemplate.Contains(
                        LmStudioQuestionDetectionOptions.MessagePlaceholder,
                        StringComparison.Ordinal),
                    $"LMStudio:QuestionDetection:UserPromptTemplate must contain {LmStudioQuestionDetectionOptions.MessagePlaceholder}.");

            services.AddHttpClient(
                LmStudioClientNames.Default,
                static (serviceProvider, client) =>
                {
                    LmStudioOptions options = serviceProvider
                        .GetRequiredService<IOptions<LmStudioOptions>>()
                        .Value;

                    client.BaseAddress = CreateBaseAddress(options.BaseAddress);
                    client.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds);
                });

            services.AddSingleton<ILmStudioClient, LmStudioClient>();
            services.AddSingleton<IQuestionDetectionService, LmStudioQuestionDetectionService>();

            return services;
        }
    }

    private static Uri CreateBaseAddress(string baseAddress)
    {
        string normalizedBaseAddress = baseAddress.EndsWith("/", StringComparison.Ordinal)
            ? baseAddress
            : $"{baseAddress}/";

        return new Uri(normalizedBaseAddress, UriKind.Absolute);
    }
}
