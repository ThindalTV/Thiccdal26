using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.Bot;
using Thiccdal.Infrastructure.Operators;
using Thiccdal.Infrastructure.Overlay;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Infrastructure.Setup;

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

                string connectionString = connectionStrings.DefaultConnection;
                DatabaseProviderDetector.DatabaseProvider provider = DatabaseProviderDetector.Detect(connectionString);

                switch (provider)
                {
                    case DatabaseProviderDetector.DatabaseProvider.PostgreSQL:
                        options.UseNpgsql(connectionString);
                        break;
                    case DatabaseProviderDetector.DatabaseProvider.SqlServer:
                        options.UseSqlServer(connectionString);
                        break;
                    case DatabaseProviderDetector.DatabaseProvider.SQLite:
                    default:
                        options.UseSqlite(connectionString);
                        break;
                }
            });

        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.AddScoped<IPlatformUserService, PlatformUserService>();
        services.AddScoped<IChatPersistenceService, ChatPersistenceService>();
        services.AddScoped<IEventPersistenceService, EventPersistenceService>();
        services.AddSingleton<IChecklistSessionService, ChecklistSessionService>();
        services.AddSingleton<ICustomChecklistItemCatalog, CustomChecklistItemCatalog>();
        services.AddSingleton<ICustomChecklistItemManagementService, CustomChecklistItemManagementService>();
        services.AddSingleton<IUserIdentityService, UserIdentityService>();
        services.AddSingleton<IBotCommandManagementService, BotCommandManagementService>();
        services.AddSingleton<IChatterMemoryService, ChatterMemoryService>();
        services.AddSingleton<IOverlayCardManagementService, OverlayCardManagementService>();
        services.AddSingleton<IProactiveMessageCatalog, ProactiveMessageCatalog>();
        services.AddSingleton<IProactiveMessageManagementService, ProactiveMessageManagementService>();
        services.AddSingleton<IEventBus, EventBus>();
        services.AddSingleton<IPlatformEventPump, PlatformEventPump>();
        services.AddScoped<ISetupStateService, SetupStateService>();
        services.AddScoped<IConfigurationPersistenceService, ConfigurationPersistenceService>();

        return services;
    }
}
