using Microsoft.Extensions.DependencyInjection;
using Thiccdal.Infrastructure.Bot;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Infrastructure.Twitch;
using Thiccdal.Modules.ChatBot.Services;
using Thiccdal.Remote.Twitch;

namespace Thiccdal.Modules.ChatBot;

public static class ChatBotRegistrationExtension
{
    extension(IServiceCollection collection)
    {
        public IServiceCollection AddChatBotServices()
        {
            collection.AddSingleton<IChatService, ChatServiceAggregator>();

            collection.AddSingleton<ITwitchTokenManager, TwitchTokenManager>();

            // TwitchService satisfies both IChatSource and ITwitchService; share the same singleton.
            collection.AddSingleton<TwitchService>();
            collection.AddSingleton<IChatSource>(sp => sp.GetRequiredService<TwitchService>());
            collection.AddSingleton<ITwitchService>(sp => sp.GetRequiredService<TwitchService>());

            collection.AddHostedService<BotCommandWorker>();

            return collection;
        }
    }
}