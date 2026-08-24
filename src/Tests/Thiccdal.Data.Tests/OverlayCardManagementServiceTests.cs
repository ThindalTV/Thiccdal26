using Thiccdal.Infrastructure.Overlay;

namespace Thiccdal.Data.Tests;

public sealed class OverlayCardManagementServiceTests : ApplicationDbContextTestFixture
{
    [Fact]
    public async Task WhenNoCardsExist_ThenListSeedsTheDefaultCards()
    {
        OverlayCardManagementService service = new(DbContextFactory);

        IReadOnlyList<OverlayCardDefinition> cards = await service.List();

        Assert.NotEmpty(cards);
        Assert.Equal(cards.OrderBy(card => card.SortOrder).Select(card => card.Id), cards.Select(card => card.Id));
    }

    [Fact]
    public async Task WhenCreatingCard_ThenItIsPersistedAndListed()
    {
        OverlayCardManagementService service = new(DbContextFactory);

        OverlayCardDefinition created = await service.Create(new OverlayCardInput
        {
            Category = "CLIP ALERT",
            Title = "Clip that!",
            Description = "Use !clip in chat",
            AccentColor = "#00d4ff",
            SortOrder = 9
        });

        IReadOnlyList<OverlayCardDefinition> cards = await service.List();

        Assert.NotEqual(0, created.Id);
        Assert.Contains(cards, card => card.Id == created.Id && card.Title == "Clip that!");
    }

    [Fact]
    public async Task WhenTitleIsBlank_ThenCreateIsRejected()
    {
        OverlayCardManagementService service = new(DbContextFactory);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.Create(new OverlayCardInput { Category = "BRB", Title = "   ", AccentColor = "#ffffff" }));
    }

    [Fact]
    public async Task WhenUpdatingCard_ThenValuesArePersisted()
    {
        OverlayCardManagementService service = new(DbContextFactory);
        OverlayCardDefinition created = await service.Create(new OverlayCardInput
        {
            Category = "BRB",
            Title = "Be right back",
            Description = "Back soon",
            AccentColor = "#9146ff"
        });

        OverlayCardDefinition? updated = await service.Update(created.Id, new OverlayCardInput
        {
            Category = "BRB",
            Title = "Back in five",
            Description = "Grabbing coffee",
            AccentColor = "#3cc864",
            IsEnabled = false
        });

        Assert.NotNull(updated);
        Assert.Equal("Back in five", updated.Title);
        Assert.Equal("#3cc864", updated.AccentColor);
        Assert.False(updated.IsEnabled);
    }

    [Fact]
    public async Task WhenDeletingCard_ThenItLeavesTheList()
    {
        OverlayCardManagementService service = new(DbContextFactory);
        OverlayCardDefinition created = await service.Create(new OverlayCardInput
        {
            Category = "SOCIALS",
            Title = "Find me everywhere",
            AccentColor = "#e84393"
        });

        bool deleted = await service.Delete(created.Id);
        IReadOnlyList<OverlayCardDefinition> cards = await service.List();

        Assert.True(deleted);
        Assert.DoesNotContain(cards, card => card.Id == created.Id);
    }

    [Fact]
    public async Task WhenCardIsMissing_ThenUpdateAndDeleteReportIt()
    {
        OverlayCardManagementService service = new(DbContextFactory);

        OverlayCardDefinition? updated = await service.Update(4242, new OverlayCardInput { Title = "Ghost", AccentColor = "#ffffff" });
        bool deleted = await service.Delete(4242);

        Assert.Null(updated);
        Assert.False(deleted);
    }
}
