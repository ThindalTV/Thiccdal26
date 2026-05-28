using Thiccdal.Data.Models;
using Thiccdal.Infrastructure.Operators;

namespace Thiccdal.Data.Tests;

public sealed class CustomChecklistItemCatalogTests : ApplicationDbContextTestFixture
{
    [Fact]
    public async Task WhenListingItems_ThenCatalogReturnsPersistedOrderAndEnabledState()
    {
        await using ApplicationDbContext dbContext = await CreateDbContextAsync();
        dbContext.CustomChecklistItems.AddRange(
            new CustomChecklistItem
            {
                Id = 12,
                Label = "Third item",
                DisplayOrder = 3,
                IsEnabled = true
            },
            new CustomChecklistItem
            {
                Id = 10,
                Label = "First item",
                DisplayOrder = 1,
                IsEnabled = false
            },
            new CustomChecklistItem
            {
                Id = 11,
                Label = "Second item",
                DisplayOrder = 2,
                IsEnabled = true
            });
        await dbContext.SaveChangesAsync();

        CustomChecklistItemCatalog catalog = new(DbContextFactory);

        IReadOnlyList<CustomChecklistItemDefinition> items = await catalog.List(CancellationToken.None);

        Assert.Equal([10, 11, 12], items.Select(item => item.Id).ToArray());
        Assert.Equal(
            ["First item", "Second item", "Third item"],
            items.Select(item => item.Label).ToArray());
        Assert.False(items[0].IsEnabled);
        Assert.True(items[1].IsEnabled);
    }
}
