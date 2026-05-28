using Microsoft.Extensions.DependencyInjection;
using Thiccdal.Infrastructure.Bot;
using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Modules.ChatBot.Services;

namespace Thiccdal.Tests;

public sealed class CommandRegistryTests
{
    [Fact]
    public async Task WhenReloading_ThenEnabledCommandsAreCachedFromManagementService()
    {
        StubBotCommandManagementService managementService = new(
        [
            CreateCommand("!hello", true),
            CreateCommand("!disabled", false)
        ]);
        CommandRegistry registry = CreateRegistry(managementService);

        await registry.Reload();

        IReadOnlyList<BotCommandDefinition> commands = registry.GetEnabledCommands();

        Assert.Single(commands);
        Assert.Equal("!hello", commands[0].Trigger);
        Assert.Equal(1, managementService.ListCalls);
    }

    [Fact]
    public async Task WhenReloadingWithinCacheLifetime_ThenDatabaseIsNotHitAgain()
    {
        FixedTimeProvider timeProvider = new(new DateTimeOffset(2026, 05, 29, 12, 00, 00, TimeSpan.Zero));
        StubBotCommandManagementService managementService = new([CreateCommand("!hello", true)]);
        CommandRegistry registry = CreateRegistry(managementService, timeProvider);

        await registry.Reload();
        timeProvider.Advance(TimeSpan.FromSeconds(4));
        await registry.Reload();

        Assert.Equal(1, managementService.ListCalls);
    }

    [Fact]
    public async Task WhenReloadingAfterCacheLifetime_ThenCommandsAreRefreshed()
    {
        FixedTimeProvider timeProvider = new(new DateTimeOffset(2026, 05, 29, 12, 00, 00, TimeSpan.Zero));
        StubBotCommandManagementService managementService = new([CreateCommand("!hello", true)]);
        CommandRegistry registry = CreateRegistry(managementService, timeProvider);

        await registry.Reload();
        timeProvider.Advance(TimeSpan.FromSeconds(6));
        await registry.Reload();

        Assert.Equal(2, managementService.ListCalls);
    }

    private static CommandRegistry CreateRegistry(
        StubBotCommandManagementService managementService,
        TimeProvider? timeProvider = null)
    {
        ServiceCollection services = new();
        services.AddSingleton<IBotCommandManagementService>(managementService);
        ServiceProvider provider = services.BuildServiceProvider();
        return new CommandRegistry(provider.GetRequiredService<IServiceScopeFactory>(), timeProvider);
    }

    private static BotCommandDefinition CreateCommand(string trigger, bool isEnabled)
    {
        return new BotCommandDefinition
        {
            Id = 1,
            Trigger = trigger,
            ResponseTemplate = "response",
            HandlerType = null,
            IsEnabled = isEnabled,
            UseCount = 0
        };
    }

    private sealed class StubBotCommandManagementService : IBotCommandManagementService
    {
        private readonly IReadOnlyList<BotCommandDefinition> _commands;

        public StubBotCommandManagementService(IReadOnlyList<BotCommandDefinition> commands)
        {
            _commands = commands;
        }

        public int ListCalls { get; private set; }

        public Task<IReadOnlyList<BotCommandDefinition>> List(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ListCalls++;
            return Task.FromResult(_commands);
        }

        public Task<BotCommandDefinition> Create(BotCommandDefinitionInput command, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<BotCommandDefinition?> Update(long id, BotCommandDefinitionInput command, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<bool> Delete(long id, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task IncrementUseCount(string trigger, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }

        public void Advance(TimeSpan duration)
        {
            _utcNow = _utcNow.Add(duration);
        }
    }
}
