using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Thiccdal.Infrastructure.Bot;
using Thiccdal.Infrastructure.Questions;
using Thiccdal.Modules.ChatBot.Services;

namespace Thiccdal.Modules.ChatBot;

public static class ChatBotRegistrationExtension
{
    extension(IServiceCollection collection)
    {
        public IServiceCollection AddChatBotServices(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(collection);
            ArgumentNullException.ThrowIfNull(configuration);

            IConfigurationSection chatBotSection = configuration.GetSection(ChatBotOptions.SectionName);

            collection.AddOptions<ChatBotOptions>()
                .Bind(chatBotSection)
                .Validate(
                    static options => !options.AiResponder.Enabled || !string.IsNullOrWhiteSpace(options.BotName),
                    "ChatBot:BotName is required when ChatBot:AiResponder:Enabled is true.")
                .Validate(
                    static options => !options.AiResponder.Enabled || !string.IsNullOrWhiteSpace(options.AiResponder.Model),
                    "ChatBot:AiResponder:Model is required when ChatBot:AiResponder:Enabled is true.")
                .Validate(
                    static options => options.AiResponder.MaxOutputTokenCount > 0,
                    "ChatBot:AiResponder:MaxOutputTokenCount must be greater than zero.")
                .Validate(
                    static options => options.AiResponder.Temperature >= 0d && options.AiResponder.Temperature <= 2d,
                    "ChatBot:AiResponder:Temperature must be between 0 and 2.")
                .Validate(
                    static options => !options.AiResponder.ChatterMemoryRetentionDays.HasValue || options.AiResponder.ChatterMemoryRetentionDays.Value > 0,
                    "ChatBot:AiResponder:ChatterMemoryRetentionDays must be greater than zero when provided.")
                .Validate(
                    static options => !options.AiResponder.Enabled || !string.IsNullOrWhiteSpace(options.AiResponder.SystemPrompt),
                    "ChatBot:AiResponder:SystemPrompt is required when ChatBot:AiResponder:Enabled is true.");

            collection.TryAddSingleton<TimeProvider>(TimeProvider.System);
            collection.AddSingleton<ChatAggregationService>();
            collection.AddSingleton<IChatAggregationService>(sp => sp.GetRequiredService<ChatAggregationService>());
            collection.AddSingleton<IChatService>(sp => sp.GetRequiredService<ChatAggregationService>());
            collection.AddSingleton<IHostedService>(sp => sp.GetRequiredService<ChatAggregationService>());
            collection.AddSingleton<IQuestionLocatorService, QuestionLocatorService>();
            collection.AddSingleton<ActivityFeedService>();
            collection.AddSingleton<IActivityFeedService>(sp => sp.GetRequiredService<ActivityFeedService>());
            collection.AddSingleton<IHostedService>(sp => sp.GetRequiredService<ActivityFeedService>());
            collection.AddSingleton<ChatRepostService>();
            collection.AddSingleton<IChatRepostService>(sp => sp.GetRequiredService<ChatRepostService>());
            collection.AddSingleton<IHostedService>(sp => sp.GetRequiredService<ChatRepostService>());
            collection.TryAddSingleton<ICommandRegistry, CommandRegistry>();
            collection.TryAddSingleton<ITokenInterpolator, TokenInterpolator>();
            collection.TryAddSingleton<ICommandUsageTracker, InMemoryCommandUsageTracker>();
            collection.TryAddSingleton<ICommandResponseSink, ChatServiceCommandResponseSink>();
            collection.TryAddSingleton<IChatBotAiResponder, ChatBotAiResponder>();
            collection.TryAddSingleton<ICommandDispatcher, CommandDispatcher>();
            collection.AddSingleton<ProactiveMessagingService>();
            collection.AddSingleton<IProactiveMessagingService>(sp => sp.GetRequiredService<ProactiveMessagingService>());
            collection.AddSingleton<IHostedService>(sp => sp.GetRequiredService<ProactiveMessagingService>());

            return collection;
        }
    }
}