using Microsoft.EntityFrameworkCore;
using Thiccdal.Data.Models;
using Thiccdal.Infrastructure.Overlay;

namespace Thiccdal.Data;

/// <summary>
/// Persists the predefined overlay cards shown on the dashboard.
/// </summary>
public sealed class OverlayCardManagementService : IOverlayCardManagementService
{
    private static readonly IReadOnlyList<OverlayCardInput> SeedCards =
    [
        new OverlayCardInput { Category = "FOLLOW REMINDER", Title = "Enjoying the stream?", Description = "Hit that follow button!", AccentColor = "#3cc864", SortOrder = 0 },
        new OverlayCardInput { Category = "DISCORD", Title = "Join the community!", Description = "discord.gg/thiccdal", AccentColor = "#5865F2", SortOrder = 1 },
        new OverlayCardInput { Category = "BRB", Title = "Be right back!", Description = "Back in a few minutes", AccentColor = "#9146FF", SortOrder = 2 },
        new OverlayCardInput { Category = "SOCIALS", Title = "Find me everywhere", Description = "@thindal on all platforms", AccentColor = "#e84393", SortOrder = 3 }
    ];

    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

    public OverlayCardManagementService(IDbContextFactory<ApplicationDbContext> dbContextFactory)
    {
        ArgumentNullException.ThrowIfNull(dbContextFactory);

        _dbContextFactory = dbContextFactory;
    }

    public async Task<IReadOnlyList<OverlayCardDefinition>> List(CancellationToken cancellationToken = default)
    {
        await using ApplicationDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureSeedCards(dbContext, cancellationToken);

        return await dbContext.OverlayCards
            .AsNoTracking()
            .OrderBy(card => card.SortOrder)
            .ThenBy(card => card.Id)
            .Select(card => new OverlayCardDefinition(
                card.Id,
                card.Category,
                card.Title,
                card.Description,
                card.AccentColor,
                card.SortOrder,
                card.IsEnabled))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<OverlayCardDefinition> Create(OverlayCardInput card, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(card);
        Validate(card);

        await using ApplicationDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureSeedCards(dbContext, cancellationToken);

        OverlayCard entity = new OverlayCard();
        Apply(entity, card);

        dbContext.OverlayCards.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Map(entity);
    }

    public async Task<OverlayCardDefinition?> Update(long id, OverlayCardInput card, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(card);
        Validate(card);

        await using ApplicationDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        OverlayCard? entity = await dbContext.OverlayCards.SingleOrDefaultAsync(
            existingCard => existingCard.Id == id,
            cancellationToken);

        if (entity is null)
        {
            return null;
        }

        Apply(entity, card);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Map(entity);
    }

    public async Task<bool> Delete(long id, CancellationToken cancellationToken = default)
    {
        await using ApplicationDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        OverlayCard? entity = await dbContext.OverlayCards.SingleOrDefaultAsync(
            existingCard => existingCard.Id == id,
            cancellationToken);

        if (entity is null)
        {
            return false;
        }

        dbContext.OverlayCards.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static void Apply(OverlayCard entity, OverlayCardInput card)
    {
        entity.Category = card.Category.Trim();
        entity.Title = card.Title.Trim();
        entity.Description = card.Description.Trim();
        entity.AccentColor = card.AccentColor.Trim();
        entity.SortOrder = card.SortOrder;
        entity.IsEnabled = card.IsEnabled;
    }

    private static OverlayCardDefinition Map(OverlayCard card)
    {
        return new OverlayCardDefinition(
            card.Id,
            card.Category,
            card.Title,
            card.Description,
            card.AccentColor,
            card.SortOrder,
            card.IsEnabled);
    }

    private static void Validate(OverlayCardInput card)
    {
        if (string.IsNullOrWhiteSpace(card.Title))
        {
            throw new InvalidOperationException("Card title is required.");
        }

        if (string.IsNullOrWhiteSpace(card.AccentColor))
        {
            throw new InvalidOperationException("Card accent colour is required.");
        }
    }

    private static async Task EnsureSeedCards(ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        if (await dbContext.OverlayCards.AnyAsync(cancellationToken))
        {
            return;
        }

        foreach (OverlayCardInput seedCard in SeedCards)
        {
            OverlayCard entity = new OverlayCard();
            Apply(entity, seedCard);
            dbContext.OverlayCards.Add(entity);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
