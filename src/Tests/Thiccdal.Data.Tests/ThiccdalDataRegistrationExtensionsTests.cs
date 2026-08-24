using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.Bot;
using Thiccdal.Infrastructure.Operators;
using Thiccdal.Infrastructure.Overlay;
using Thiccdal.Infrastructure.Remotes;

namespace Thiccdal.Data.Tests;

public class ThiccdalDataRegistrationExtensionsTests
{
    [Fact]
    public void WhenAddingThiccdalData_ThenEventPipelineServicesAreRegistered()
    {
        ConfigurationManager configuration = new();
        configuration[$"{ConnectionStringsOptions.SectionName}:{nameof(ConnectionStringsOptions.DefaultConnection)}"] =
            "Data Source=thiccdal-registration-tests.db";

        ServiceCollection services = new();
        services.AddLogging();
        services.AddThiccdalData(configuration);

        using ServiceProvider provider = services.BuildServiceProvider();

        ConnectionStringsOptions options = provider.GetRequiredService<IOptions<ConnectionStringsOptions>>().Value;
        UserIdentityOptions userIdentityOptions = provider.GetRequiredService<IOptions<UserIdentityOptions>>().Value;
        IDbContextFactory<ApplicationDbContext> dbContextFactory = provider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        IPlatformUserService platformUserService = provider.GetRequiredService<IPlatformUserService>();
        IChatPersistenceService chatPersistenceService = provider.GetRequiredService<IChatPersistenceService>();
        IEventPersistenceService persistenceService = provider.GetRequiredService<IEventPersistenceService>();
        IChecklistSessionService checklistSessionService = provider.GetRequiredService<IChecklistSessionService>();
        ICustomChecklistItemCatalog customChecklistItemCatalog = provider.GetRequiredService<ICustomChecklistItemCatalog>();
        IOverlayCardManagementService overlayCardManagementService = provider.GetRequiredService<IOverlayCardManagementService>();
        ICustomChecklistItemManagementService customChecklistItemManagementService = provider.GetRequiredService<ICustomChecklistItemManagementService>();
        IUserIdentityService userIdentityService = provider.GetRequiredService<IUserIdentityService>();
        IBotCommandManagementService commandManagementService = provider.GetRequiredService<IBotCommandManagementService>();
        IProactiveMessageCatalog proactiveMessageCatalog = provider.GetRequiredService<IProactiveMessageCatalog>();
        IEventBus eventBus = provider.GetRequiredService<IEventBus>();
        IPlatformEventPump eventPump = provider.GetRequiredService<IPlatformEventPump>();
        using ApplicationDbContext dbContext = dbContextFactory.CreateDbContext();

        Assert.Equal("Data Source=thiccdal-registration-tests.db", options.DefaultConnection);
        Assert.Equal(0.85d, userIdentityOptions.SimilarityThreshold);
        Assert.Equal("Data Source=thiccdal-registration-tests.db", dbContext.Database.GetConnectionString());
        Assert.NotNull(platformUserService);
        Assert.NotNull(chatPersistenceService);
        Assert.NotNull(persistenceService);
        Assert.NotNull(checklistSessionService);
        Assert.NotNull(customChecklistItemCatalog);
        Assert.NotNull(overlayCardManagementService);
        Assert.NotNull(customChecklistItemManagementService);
        Assert.NotNull(userIdentityService);
        Assert.NotNull(commandManagementService);
        Assert.NotNull(proactiveMessageCatalog);
        Assert.NotNull(eventBus);
        Assert.NotNull(eventPump);
        using IServiceScope firstScope = provider.CreateScope();
        using IServiceScope secondScope = provider.CreateScope();
        Assert.NotSame(
            firstScope.ServiceProvider.GetRequiredService<IPlatformUserService>(),
            secondScope.ServiceProvider.GetRequiredService<IPlatformUserService>());
        Assert.NotSame(
            firstScope.ServiceProvider.GetRequiredService<IChatPersistenceService>(),
            secondScope.ServiceProvider.GetRequiredService<IChatPersistenceService>());
        Assert.NotSame(
            firstScope.ServiceProvider.GetRequiredService<IEventPersistenceService>(),
            secondScope.ServiceProvider.GetRequiredService<IEventPersistenceService>());
        Assert.Same(
            firstScope.ServiceProvider.GetRequiredService<IChecklistSessionService>(),
            secondScope.ServiceProvider.GetRequiredService<IChecklistSessionService>());
        Assert.Same(
            firstScope.ServiceProvider.GetRequiredService<ICustomChecklistItemCatalog>(),
            secondScope.ServiceProvider.GetRequiredService<ICustomChecklistItemCatalog>());
        Assert.Same(
            firstScope.ServiceProvider.GetRequiredService<ICustomChecklistItemManagementService>(),
            secondScope.ServiceProvider.GetRequiredService<ICustomChecklistItemManagementService>());
        Assert.Same(
            firstScope.ServiceProvider.GetRequiredService<IUserIdentityService>(),
            secondScope.ServiceProvider.GetRequiredService<IUserIdentityService>());
        Assert.Same(
            firstScope.ServiceProvider.GetRequiredService<IBotCommandManagementService>(),
            secondScope.ServiceProvider.GetRequiredService<IBotCommandManagementService>());
    }

    [Fact]
    public void WhenDefaultConnectionIsBlank_ThenOptionsValidationFails()
    {
        ConfigurationManager configuration = new();
        configuration[$"{ConnectionStringsOptions.SectionName}:{nameof(ConnectionStringsOptions.DefaultConnection)}"] = string.Empty;
        ServiceCollection services = new();
        services.AddLogging();
        services.AddThiccdalData(configuration);

        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<ConnectionStringsOptions>>().Value);
    }
}
