using Microsoft.EntityFrameworkCore;
using Thiccdal.Data.Models;
using Thiccdal.Infrastructure.Operators;

namespace Thiccdal.Data;

/// <summary>
/// Persists operator-managed Personal Prep checklist items.
/// </summary>
public sealed class CustomChecklistItemManagementService : ICustomChecklistItemManagementService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

    public CustomChecklistItemManagementService(IDbContextFactory<ApplicationDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<IReadOnlyList<CustomChecklistItemDefinition>> List(CancellationToken cancellationToken = default)
    {
        await using ApplicationDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await ListDefinitions(dbContext, cancellationToken);
    }

    public async Task<CustomChecklistItemDefinition> Create(string label, CancellationToken cancellationToken = default)
    {
        string normalizedLabel = NormalizeLabel(label);

        await using ApplicationDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        int nextDisplayOrder = await dbContext.CustomChecklistItems.CountAsync(cancellationToken) + 1;
        CustomChecklistItem item = new()
        {
            Label = normalizedLabel,
            DisplayOrder = nextDisplayOrder,
            IsEnabled = true
        };

        dbContext.CustomChecklistItems.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Map(item);
    }

    public async Task<CustomChecklistItemDefinition?> Update(CustomChecklistItemDefinition item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        string normalizedLabel = NormalizeLabel(item.Label);

        await using ApplicationDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        CustomChecklistItem? persistedItem = await dbContext.CustomChecklistItems
            .SingleOrDefaultAsync(existingItem => existingItem.Id == item.Id, cancellationToken);

        if (persistedItem is null)
        {
            return null;
        }

        persistedItem.Label = normalizedLabel;
        persistedItem.IsEnabled = item.IsEnabled;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Map(persistedItem);
    }

    public async Task<bool> Delete(int id, CancellationToken cancellationToken = default)
    {
        await using ApplicationDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        CustomChecklistItem? item = await dbContext.CustomChecklistItems
            .SingleOrDefaultAsync(existingItem => existingItem.Id == id, cancellationToken);

        if (item is null)
        {
            return false;
        }

        List<CustomChecklistItem> remainingItems = await dbContext.CustomChecklistItems
            .OrderBy(existingItem => existingItem.DisplayOrder)
            .ThenBy(existingItem => existingItem.Id)
            .Where(existingItem => existingItem.Id != id)
            .ToListAsync(cancellationToken);

        dbContext.CustomChecklistItems.Remove(item);
        NormalizeDisplayOrder(remainingItems);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<bool> MoveUp(int id, CancellationToken cancellationToken = default)
    {
        return Move(id, moveEarlier: true, cancellationToken);
    }

    public Task<bool> MoveDown(int id, CancellationToken cancellationToken = default)
    {
        return Move(id, moveEarlier: false, cancellationToken);
    }

    private async Task<bool> Move(int id, bool moveEarlier, CancellationToken cancellationToken)
    {
        await using ApplicationDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        List<CustomChecklistItem> items = await dbContext.CustomChecklistItems
            .OrderBy(item => item.DisplayOrder)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);

        int currentIndex = items.FindIndex(item => item.Id == id);
        if (currentIndex < 0)
        {
            return false;
        }

        int neighborIndex = moveEarlier ? currentIndex - 1 : currentIndex + 1;
        if (neighborIndex < 0 || neighborIndex >= items.Count)
        {
            return false;
        }

        (items[currentIndex].DisplayOrder, items[neighborIndex].DisplayOrder) =
            (items[neighborIndex].DisplayOrder, items[currentIndex].DisplayOrder);

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static async Task<IReadOnlyList<CustomChecklistItemDefinition>> ListDefinitions(ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        return await dbContext.CustomChecklistItems
            .AsNoTracking()
            .OrderBy(item => item.DisplayOrder)
            .ThenBy(item => item.Id)
            .Select(item => new CustomChecklistItemDefinition
            {
                Id = item.Id,
                Label = item.Label,
                DisplayOrder = item.DisplayOrder,
                IsEnabled = item.IsEnabled
            })
            .ToArrayAsync(cancellationToken);
    }

    private static void NormalizeDisplayOrder(IEnumerable<CustomChecklistItem> items)
    {
        int index = 1;

        foreach (CustomChecklistItem item in items)
        {
            item.DisplayOrder = index++;
        }
    }

    private static string NormalizeLabel(string label)
    {
        string normalizedLabel = label.Trim();
        if (string.IsNullOrWhiteSpace(normalizedLabel))
        {
            throw new InvalidOperationException("Custom checklist item labels cannot be blank.");
        }

        return normalizedLabel;
    }

    private static CustomChecklistItemDefinition Map(CustomChecklistItem item)
    {
        return new CustomChecklistItemDefinition
        {
            Id = item.Id,
            Label = item.Label,
            DisplayOrder = item.DisplayOrder,
            IsEnabled = item.IsEnabled
        };
    }
}
