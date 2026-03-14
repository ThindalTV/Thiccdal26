using Microsoft.Extensions.DependencyInjection;
using Thiccdal.Infrastructure.Teleprompter;
using Thiccdal.Modules.Teleprompter.Services;

namespace Thiccdal.Modules.Teleprompter;

public static class TeleprompterRegistrationExtension
{
    extension(IServiceCollection collection)
    {
        public IServiceCollection AddTeleprompterServices()
        {
            collection.AddSingleton<ITeleprompterService, TeleprompterService>();
            return collection;
        }
    }
}
