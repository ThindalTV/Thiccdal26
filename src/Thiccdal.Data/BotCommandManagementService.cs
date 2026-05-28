using Microsoft.EntityFrameworkCore;
using Thiccdal.Data.Models;
using Thiccdal.Infrastructure.Bot;
using Thiccdal.Infrastructure.Bot.Models;

namespace Thiccdal.Data;

/// <summary>
/// Persists operator-managed bot commands in the application database.
/// </summary>
public sealed class BotCommandManagementService : IBotCommandManagementService
{
    private static readonly IReadOnlyList<BotCommandSeedDefinition> SeedCommands =
    [
        new BotCommandSeedDefinition("!shoutout", "Go show {user} some love after the stream.", null, true),
        new BotCommandSeedDefinition("!discord", "Join the Discord: https://discord.gg/thiccdal", null, true),
        new BotCommandSeedDefinition("!socials", "All the links live at https://thiccdal.tv/socials", null, true),
        new BotCommandSeedDefinition("!clip", "Clip it now so chat can replay the chaos later.", null, true),
        new BotCommandSeedDefinition("!schedule", "Next stream goes live Friday at 7 PM Central.", null, true),
        new BotCommandSeedDefinition("!lurk", "Thanks for the lurk, {user} — appreciate you.", null, true),
        new BotCommandSeedDefinition("!uptime", "We have been live for {uptime}.", null, true),
        new BotCommandSeedDefinition("!poll", "Polls are available from the control surface when we are live.", null, false)
    ];

    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

    public BotCommandManagementService(IDbContextFactory<ApplicationDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<IReadOnlyList<BotCommandDefinition>> List(CancellationToken cancellationToken)
    {
        await using ApplicationDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureSeedCommands(dbContext, cancellationToken);

        BotCommandDefinition[] commands = await dbContext.BotCommands
            .AsNoTracking()
            .OrderBy(command => command.Trigger)
            .Select(command => Map(command))
            .ToArrayAsync(cancellationToken);

        return commands;
    }

    public async Task<BotCommandDefinition> Create(BotCommandDefinitionInput command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using ApplicationDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureSeedCommands(dbContext, cancellationToken);

        string normalizedTrigger = NormalizeTrigger(command.Trigger);
        await EnsureTriggerAvailable(dbContext, normalizedTrigger, null, cancellationToken);

        BotCommand entity = new BotCommand
        {
            Trigger = normalizedTrigger,
            ResponseTemplate = NormalizeResponseTemplate(command.ResponseTemplate),
            HandlerType = NormalizeHandlerType(command.HandlerType),
            IsEnabled = command.IsEnabled,
            UseCount = 0
        };

        dbContext.BotCommands.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Map(entity);
    }

    public async Task<BotCommandDefinition?> Update(long id, BotCommandDefinitionInput command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using ApplicationDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureSeedCommands(dbContext, cancellationToken);

        BotCommand? entity = await dbContext.BotCommands.SingleOrDefaultAsync(
            existingCommand => existingCommand.Id == id,
            cancellationToken);

        if (entity is null)
        {
            return null;
        }

        string normalizedTrigger = NormalizeTrigger(command.Trigger);
        await EnsureTriggerAvailable(dbContext, normalizedTrigger, id, cancellationToken);

        entity.Trigger = normalizedTrigger;
        entity.ResponseTemplate = NormalizeResponseTemplate(command.ResponseTemplate);
        entity.HandlerType = NormalizeHandlerType(command.HandlerType);
        entity.IsEnabled = command.IsEnabled;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Map(entity);
    }

    public async Task<bool> Delete(long id, CancellationToken cancellationToken)
    {
        await using ApplicationDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureSeedCommands(dbContext, cancellationToken);

        BotCommand? entity = await dbContext.BotCommands.SingleOrDefaultAsync(
            existingCommand => existingCommand.Id == id,
            cancellationToken);

        if (entity is null)
        {
            return false;
        }

        dbContext.BotCommands.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task IncrementUseCount(string trigger, CancellationToken cancellationToken)
    {
        string normalizedTrigger = NormalizeTrigger(trigger);

        await using ApplicationDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        BotCommand? entity = await dbContext.BotCommands.SingleOrDefaultAsync(
            existingCommand => existingCommand.Trigger == normalizedTrigger,
            cancellationToken);
        if (entity is null)
        {
            return;
        }

        entity.UseCount++;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static BotCommandDefinition Map(BotCommand command)
    {
        return new BotCommandDefinition
        {
            Id = command.Id,
            Trigger = command.Trigger,
            ResponseTemplate = command.ResponseTemplate,
            HandlerType = command.HandlerType,
            IsEnabled = command.IsEnabled,
            UseCount = command.UseCount
        };
    }

    private static string NormalizeTrigger(string trigger)
    {
        if (string.IsNullOrWhiteSpace(trigger))
        {
            throw new InvalidOperationException("Command trigger is required.");
        }

        string normalizedTrigger = trigger.Trim();
        normalizedTrigger = normalizedTrigger.TrimStart('!');
        normalizedTrigger = normalizedTrigger.Replace(" ", string.Empty, StringComparison.Ordinal);

        if (normalizedTrigger.Length == 0)
        {
            throw new InvalidOperationException("Command trigger is required.");
        }

        return $"!{normalizedTrigger.ToLowerInvariant()}";
    }

    private static string NormalizeResponseTemplate(string responseTemplate)
    {
        if (string.IsNullOrWhiteSpace(responseTemplate))
        {
            throw new InvalidOperationException("Command response is required.");
        }

        return responseTemplate.Trim();
    }

    private static string? NormalizeHandlerType(string? handlerType)
    {
        return string.IsNullOrWhiteSpace(handlerType)
            ? null
            : handlerType.Trim();
    }

    private static async Task EnsureTriggerAvailable(
        ApplicationDbContext dbContext,
        string trigger,
        long? currentCommandId,
        CancellationToken cancellationToken)
    {
        bool exists = await dbContext.BotCommands.AnyAsync(
            existingCommand => existingCommand.Trigger == trigger &&
                               (!currentCommandId.HasValue || existingCommand.Id != currentCommandId.Value),
            cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException($"The {trigger} command already exists.");
        }
    }

    private static async Task EnsureSeedCommands(ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        if (await dbContext.BotCommands.AnyAsync(cancellationToken))
        {
            return;
        }

        foreach (BotCommandSeedDefinition seedCommand in SeedCommands)
        {
            dbContext.BotCommands.Add(new BotCommand
            {
                Trigger = seedCommand.Trigger,
                ResponseTemplate = seedCommand.ResponseTemplate,
                HandlerType = seedCommand.HandlerType,
                IsEnabled = seedCommand.IsEnabled,
                UseCount = 0
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private sealed record BotCommandSeedDefinition(
        string Trigger,
        string ResponseTemplate,
        string? HandlerType,
        bool IsEnabled);
}
