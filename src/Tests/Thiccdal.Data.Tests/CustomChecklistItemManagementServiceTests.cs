using Thiccdal.Data.Models;
using Thiccdal.Infrastructure.Operators;

namespace Thiccdal.Data.Tests;

public sealed class CustomChecklistItemManagementServiceTests : ApplicationDbContextTestFixture
{
    [Fact]
    public async Task WhenCreatingAndUpdatingItems_ThenChangesPersistForSubsequentLists()
    {
        CustomChecklistItemManagementService service = new(DbContextFactory);

        CustomChecklistItemDefinition createdItem = await service.Create("  Check batteries  ", CancellationToken.None);
        CustomChecklistItemDefinition? updatedItem = await service.Update(
            createdItem with
            {
                Label = "Check spare batteries",
                IsEnabled = false
            },
            CancellationToken.None);
        IReadOnlyList<CustomChecklistItemDefinition> items = await service.List(CancellationToken.None);

        Assert.NotNull(updatedItem);
        Assert.Equal("Check batteries", createdItem.Label);
        Assert.Equal("Check spare batteries", updatedItem.Label);
        Assert.False(updatedItem.IsEnabled);
        Assert.Single(items);
        Assert.Equal("Check spare batteries", items[0].Label);
        Assert.False(items[0].IsEnabled);
        Assert.Equal(1, items[0].DisplayOrder);
    }

    [Fact]
    public async Task WhenDeletingItem_ThenRemainingItemsAreResequenced()
    {
        await using ApplicationDbContext dbContext = await CreateDbContextAsync();
        dbContext.CustomChecklistItems.AddRange(
            new CustomChecklistItem
            {
                Id = 1,
                Label = "First",
                DisplayOrder = 1,
                IsEnabled = true
            },
            new CustomChecklistItem
            {
                Id = 2,
                Label = "Second",
                DisplayOrder = 2,
                IsEnabled = true
            },
            new CustomChecklistItem
            {
                Id = 3,
                Label = "Third",
                DisplayOrder = 3,
                IsEnabled = true
            });
        await dbContext.SaveChangesAsync();

        CustomChecklistItemManagementService service = new(DbContextFactory);

        bool deleted = await service.Delete(2, CancellationToken.None);
        IReadOnlyList<CustomChecklistItemDefinition> items = await service.List(CancellationToken.None);

        Assert.True(deleted);
        Assert.Equal([1, 3], items.Select(item => item.Id).ToArray());
        Assert.Equal([1, 2], items.Select(item => item.DisplayOrder).ToArray());
    }

    [Fact]
    public async Task WhenMovingItems_ThenDisplayOrderSwapsWithNeighbor()
    {
        await using ApplicationDbContext dbContext = await CreateDbContextAsync();
        dbContext.CustomChecklistItems.AddRange(
            new CustomChecklistItem
            {
                Id = 10,
                Label = "First",
                DisplayOrder = 1,
                IsEnabled = true
            },
            new CustomChecklistItem
            {
                Id = 11,
                Label = "Second",
                DisplayOrder = 2,
                IsEnabled = true
            },
            new CustomChecklistItem
            {
                Id = 12,
                Label = "Third",
                DisplayOrder = 3,
                IsEnabled = true
            });
        await dbContext.SaveChangesAsync();

        CustomChecklistItemManagementService service = new(DbContextFactory);

        bool movedUp = await service.MoveUp(11, CancellationToken.None);
        bool movedDown = await service.MoveDown(11, CancellationToken.None);
        IReadOnlyList<CustomChecklistItemDefinition> items = await service.List(CancellationToken.None);

        Assert.True(movedUp);
        Assert.True(movedDown);
        Assert.Equal([10, 11, 12], items.Select(item => item.Id).ToArray());
        Assert.Equal([1, 2, 3], items.Select(item => item.DisplayOrder).ToArray());
    }
}
