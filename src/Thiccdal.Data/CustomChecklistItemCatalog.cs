using Microsoft.EntityFrameworkCore;
using Thiccdal.Infrastructure.Operators;

namespace Thiccdal.Data;

/// <summary>
/// Reads operator-managed custom checklist items from the application database.
/// </summary>
public sealed class CustomChecklistItemCatalog : ICustomChecklistItemCatalog
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

    public CustomChecklistItemCatalog(IDbContextFactory<ApplicationDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<IReadOnlyList<CustomChecklistItemDefinition>> List(CancellationToken cancellationToken = default)
    {
        await using ApplicationDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        CustomChecklistItemDefinition[] items = await dbContext.CustomChecklistItems
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

        return items;
    }
}
