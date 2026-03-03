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
            collection.AddScoped<IChatService, ChatServiceAggregator>();

            collection.AddScoped<ITwitchTokenManager, TwitchTokenManager>();
            collection.AddScoped<IChatSource, TwitchService>();
            return collection;
        }
    }
}
