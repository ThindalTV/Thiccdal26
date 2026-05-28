using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.Bot;
using Thiccdal.Infrastructure.Operators;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Infrastructure.Streaming;
using Thiccdal.Infrastructure.YouTube;

namespace Thiccdal.Data;

public static class ThiccdalDataRegistrationExtensions
{
    public static IServiceCollection AddThiccdalData(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<ConnectionStringsOptions>()
            .Bind(configuration.GetSection(ConnectionStringsOptions.SectionName))
            .Validate(
                static options => !string.IsNullOrWhiteSpace(options.DefaultConnection),
                "ConnectionStrings:DefaultConnection is required.");

        services.AddOptions<UserIdentityOptions>()
            .Bind(configuration.GetSection(UserIdentityOptions.SectionName))
            .Validate(
                static options => options.SimilarityThreshold is >= 0d and <= 1d,
                $"{UserIdentityOptions.SectionName}:{nameof(UserIdentityOptions.SimilarityThreshold)} must be between 0 and 1.");

        services.AddDbContextFactory<ApplicationDbContext>(
            static (serviceProvider, options) =>
            {
                ConnectionStringsOptions connectionStrings = serviceProvider
                    .GetRequiredService<IOptions<ConnectionStringsOptions>>()
                    .Value;

                options.UseSqlite(connectionStrings.DefaultConnection);
            });

        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.AddScoped<IPlatformUserService, PlatformUserService>();
        services.AddScoped<IChatPersistenceService, ChatPersistenceService>();
        services.AddScoped<IEventPersistenceService, EventPersistenceService>();
        services.AddSingleton<IChecklistSessionService, ChecklistSessionService>();
        services.AddSingleton<ICustomChecklistItemCatalog, CustomChecklistItemCatalog>();
        services.AddSingleton<ICustomChecklistItemManagementService, CustomChecklistItemManagementService>();
        services.AddSingleton<IUserIdentityService, UserIdentityService>();
        services.AddSingleton<IYouTubeTokenStore, YouTubeTokenStore>();
        services.AddSingleton<IBotCommandManagementService, BotCommandManagementService>();
        services.AddSingleton<IChatterMemoryService, ChatterMemoryService>();
        services.AddSingleton<IProactiveMessageCatalog, ProactiveMessageCatalog>();
        services.AddSingleton<IStreamRecordingService, StreamRecordingService>();
        services.AddSingleton<IRestreamRuntimeService, RestreamRuntimeService>();
        services.AddSingleton<IRestreamSettingsAccessor>(
            static serviceProvider => (IRestreamSettingsAccessor)serviceProvider.GetRequiredService<IRestreamRuntimeService>());
        services.AddSingleton<IEventBus, EventBus>();
        services.AddSingleton<IPlatformEventPump, PlatformEventPump>();

        return services;
    }
}
