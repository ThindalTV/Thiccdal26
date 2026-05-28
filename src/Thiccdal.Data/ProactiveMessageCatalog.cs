using Microsoft.EntityFrameworkCore;
using Thiccdal.Infrastructure.Bot;

namespace Thiccdal.Data;

public sealed class ProactiveMessageCatalog : IProactiveMessageCatalog
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

    public ProactiveMessageCatalog(IDbContextFactory<ApplicationDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<IReadOnlyList<ProactiveMessageDefinition>> GetEnabledMessages(CancellationToken cancellationToken = default)
    {
        await using ApplicationDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.ProactiveMessages
            .AsNoTracking()
            .Where(message => message.IsEnabled)
            .OrderBy(message => message.Id)
            .Select(message => new ProactiveMessageDefinition(
                message.Id,
                message.Message,
                message.IntervalSeconds,
                message.IsEnabled,
                message.LastSentAt))
            .ToArrayAsync(cancellationToken);
    }

    public async Task MarkSent(long messageId, DateTimeOffset sentAt, CancellationToken cancellationToken = default)
    {
        await using ApplicationDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        Models.ProactiveMessage? message = await dbContext.ProactiveMessages.SingleOrDefaultAsync(
            proactiveMessage => proactiveMessage.Id == messageId,
            cancellationToken);
        if (message is null)
        {
            return;
        }

        message.LastSentAt = sentAt;
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
