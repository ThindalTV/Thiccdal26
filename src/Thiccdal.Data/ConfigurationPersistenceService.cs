using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Thiccdal.Data.Models;
using Thiccdal.Infrastructure.Setup;

namespace Thiccdal.Data;

/// <summary>
/// Persists application configuration to the AppConfiguration table.
/// </summary>
public sealed class ConfigurationPersistenceService : IConfigurationPersistenceService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly ILogger<ConfigurationPersistenceService> _logger;

    public ConfigurationPersistenceService(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        ILogger<ConfigurationPersistenceService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task<string?> GetValue(string key, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var config = await context.AppConfigurations
            .FirstOrDefaultAsync(c => c.Key == key, cancellationToken);
        return config?.Value;
    }

    public async Task SetValue(string key, string value, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var config = await context.AppConfigurations
            .FirstOrDefaultAsync(c => c.Key == key, cancellationToken);

        if (config is null)
        {
            config = new AppConfiguration { Key = key };
            context.AppConfigurations.Add(config);
        }

        config.Value = value;
        config.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
        _logger.LogDebug("Saved configuration {Key}", key);
    }

    public async Task<T?> GetValue<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        var json = await GetValue(key, cancellationToken);
        if (string.IsNullOrEmpty(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize configuration {Key}", key);
            return null;
        }
    }

    public async Task SetValue<T>(string key, T value, CancellationToken cancellationToken = default) where T : class
    {
        var json = JsonSerializer.Serialize(value);
        await SetValue(key, json, cancellationToken);
    }

    public async Task<bool> HasKey(string key, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.AppConfigurations.AnyAsync(c => c.Key == key, cancellationToken);
    }

    public async Task RemoveKey(string key, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var config = await context.AppConfigurations
            .FirstOrDefaultAsync(c => c.Key == key, cancellationToken);

        if (config is not null)
        {
            context.AppConfigurations.Remove(config);
            await context.SaveChangesAsync(cancellationToken);
            _logger.LogDebug("Removed configuration {Key}", key);
        }
    }
}
