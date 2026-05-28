using Microsoft.Extensions.DependencyInjection;
using Thiccdal.Infrastructure.Bot;
using Thiccdal.Infrastructure.Bot.Models;

namespace Thiccdal.Modules.ChatBot.Services;

public sealed class CommandRegistry : ICommandRegistry
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(5);

    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _reloadLock = new(1, 1);

    private volatile CacheSnapshot _cache = new([], DateTimeOffset.MinValue);

    public CommandRegistry(IServiceScopeFactory serviceScopeFactory, TimeProvider? timeProvider = null)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public IReadOnlyList<BotCommandDefinition> GetEnabledCommands()
    {
        return _cache.Commands;
    }

    public async Task Reload(CancellationToken cancellationToken = default)
    {
        CacheSnapshot currentCache = _cache;
        DateTimeOffset now = _timeProvider.GetUtcNow();
        if (currentCache.LoadedAt != DateTimeOffset.MinValue && now - currentCache.LoadedAt < CacheLifetime)
        {
            return;
        }

        await _reloadLock.WaitAsync(cancellationToken);

        try
        {
            currentCache = _cache;
            now = _timeProvider.GetUtcNow();
            if (currentCache.LoadedAt != DateTimeOffset.MinValue && now - currentCache.LoadedAt < CacheLifetime)
            {
                return;
            }

            using IServiceScope scope = _serviceScopeFactory.CreateScope();
            IBotCommandManagementService managementService = scope.ServiceProvider
                .GetRequiredService<IBotCommandManagementService>();

            IReadOnlyList<BotCommandDefinition> commands = await managementService.List(cancellationToken);
            _cache = new CacheSnapshot(commands.Where(command => command.IsEnabled).ToArray(), now);
        }
        finally
        {
            _reloadLock.Release();
        }
    }

    private sealed record CacheSnapshot(IReadOnlyList<BotCommandDefinition> Commands, DateTimeOffset LoadedAt);
}
