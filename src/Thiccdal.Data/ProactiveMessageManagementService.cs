using Microsoft.EntityFrameworkCore;
using Thiccdal.Infrastructure.Bot;

namespace Thiccdal.Data;

/// <summary>
/// Persists operator edits to the bot's timed autoresponses.
/// </summary>
public sealed class ProactiveMessageManagementService : IProactiveMessageManagementService
{
    private const int MinimumIntervalSeconds = 30;

    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

    public ProactiveMessageManagementService(IDbContextFactory<ApplicationDbContext> dbContextFactory)
    {
        ArgumentNullException.ThrowIfNull(dbContextFactory);

        _dbContextFactory = dbContextFactory;
    }

    public async Task<IReadOnlyList<ProactiveMessageDefinition>> List(CancellationToken cancellationToken = default)
    {
        await using ApplicationDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.ProactiveMessages
            .AsNoTracking()
            .OrderBy(message => message.IntervalSeconds)
            .ThenBy(message => message.Id)
            .Select(message => new ProactiveMessageDefinition(
                message.Id,
                message.Message,
                message.IntervalSeconds,
                message.IsEnabled,
                message.LastSentAt))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<ProactiveMessageDefinition> Create(ProactiveMessageInput message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        Validate(message);

        await using ApplicationDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        Models.ProactiveMessage entity = new Models.ProactiveMessage
        {
            Message = message.Message.Trim(),
            IntervalSeconds = message.IntervalSeconds,
            IsEnabled = message.IsEnabled
        };

        dbContext.ProactiveMessages.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Map(entity);
    }

    public async Task<ProactiveMessageDefinition?> Update(long id, ProactiveMessageInput message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        Validate(message);

        await using ApplicationDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        Models.ProactiveMessage? entity = await dbContext.ProactiveMessages.SingleOrDefaultAsync(
            proactiveMessage => proactiveMessage.Id == id,
            cancellationToken);
        if (entity is null)
        {
            return null;
        }

        entity.Message = message.Message.Trim();
        entity.IntervalSeconds = message.IntervalSeconds;
        entity.IsEnabled = message.IsEnabled;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Map(entity);
    }

    public async Task<bool> Delete(long id, CancellationToken cancellationToken = default)
    {
        await using ApplicationDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        Models.ProactiveMessage? entity = await dbContext.ProactiveMessages.SingleOrDefaultAsync(
            proactiveMessage => proactiveMessage.Id == id,
            cancellationToken);
        if (entity is null)
        {
            return false;
        }

        dbContext.ProactiveMessages.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static void Validate(ProactiveMessageInput message)
    {
        if (string.IsNullOrWhiteSpace(message.Message))
        {
            throw new InvalidOperationException("An autoresponse needs message text.");
        }

        if (message.IntervalSeconds < MinimumIntervalSeconds)
        {
            throw new InvalidOperationException($"An autoresponse must wait at least {MinimumIntervalSeconds} seconds between sends.");
        }
    }

    private static ProactiveMessageDefinition Map(Models.ProactiveMessage entity)
    {
        return new ProactiveMessageDefinition(
            entity.Id,
            entity.Message,
            entity.IntervalSeconds,
            entity.IsEnabled,
            entity.LastSentAt);
    }
}
