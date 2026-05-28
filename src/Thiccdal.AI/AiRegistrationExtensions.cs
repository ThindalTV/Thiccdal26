using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.AI;
using Thiccdal.Infrastructure.Questions;

namespace Thiccdal.AI;

/// <summary>
/// Registers AI services.
/// </summary>
public static class AiRegistrationExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Adds the repository-owned AI services and OpenAI-compatible transport.
        /// </summary>
        /// <param name="configuration">The application configuration root.</param>
        /// <returns>The service collection for chaining.</returns>
        public IServiceCollection AddAiIntegration(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            IConfigurationSection openAiSection = configuration.GetSection(OpenAiOptions.SectionName);
            IConfigurationSection questionDetectionSection = configuration.GetSection(QuestionDetectionOptions.SectionName);

            services.AddOptions<OpenAiOptions>()
                .Bind(openAiSection)
                .Validate(
                    static options => Uri.TryCreate(options.Endpoint, UriKind.Absolute, out Uri? uri)
                        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps),
                    "AI:OpenAICompatible:Endpoint must be an absolute HTTP or HTTPS URI.")
                .Validate(
                    static options => options.RequestTimeoutSeconds > 0,
                    "AI:OpenAICompatible:RequestTimeoutSeconds must be greater than zero.");

            services.AddOptions<QuestionDetectionOptions>()
                .Bind(questionDetectionSection)
                .Validate(
                    static options => !options.Enabled || !string.IsNullOrWhiteSpace(options.Model),
                    "AI:QuestionDetection:Model is required when question detection is enabled.")
                .Validate(
                    static options => options.MaxOutputTokenCount > 0,
                    "AI:QuestionDetection:MaxOutputTokenCount must be greater than zero.")
                .Validate(
                    static options => options.Temperature >= 0d && options.Temperature <= 2d,
                    "AI:QuestionDetection:Temperature must be between 0 and 2.")
                .Validate(
                    static options => !options.Enabled || !string.IsNullOrWhiteSpace(options.SystemPrompt),
                    "AI:QuestionDetection:SystemPrompt is required when question detection is enabled.")
                .Validate(
                    static options => !options.Enabled || !string.IsNullOrWhiteSpace(options.UserPromptTemplate)
                        && options.UserPromptTemplate.Contains(
                            QuestionDetectionOptions.MessagePlaceholder,
                            StringComparison.Ordinal),
                    $"AI:QuestionDetection:UserPromptTemplate must contain {QuestionDetectionOptions.MessagePlaceholder}.");

            services.AddSingleton<IChatCompletionClient, OpenAiCompatibleChatClient>();
            services.AddSingleton<IQuestionDetectionService, OpenAiQuestionDetectionService>();

            return services;
        }
    }
}
