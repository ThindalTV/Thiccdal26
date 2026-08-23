using Thiccdal.Infrastructure.Bot;

namespace Thiccdal.Data.Tests;

public sealed class ProactiveMessageManagementServiceTests : ApplicationDbContextTestFixture
{
    [Fact]
    public async Task WhenCreatingAutoresponse_ThenItIsPersistedAndListed()
    {
        ProactiveMessageManagementService service = new(DbContextFactory);

        ProactiveMessageDefinition created = await service.Create(
            new ProactiveMessageInput("Follow if you are enjoying the stream!", 600, true));

        IReadOnlyList<ProactiveMessageDefinition> messages = await service.List();

        Assert.NotEqual(0, created.Id);
        Assert.Contains(messages, message => message.Id == created.Id && message.IntervalSeconds == 600);
    }

    [Fact]
    public async Task WhenMessageTextIsBlank_ThenCreateIsRejected()
    {
        ProactiveMessageManagementService service = new(DbContextFactory);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.Create(new ProactiveMessageInput("   ", 600, true)));
    }

    [Fact]
    public async Task WhenIntervalIsBelowMinimum_ThenCreateIsRejected()
    {
        ProactiveMessageManagementService service = new(DbContextFactory);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.Create(new ProactiveMessageInput("Too chatty", 5, true)));
    }

    [Fact]
    public async Task WhenUpdatingAutoresponse_ThenValuesArePersisted()
    {
        ProactiveMessageManagementService service = new(DbContextFactory);
        ProactiveMessageDefinition created = await service.Create(
            new ProactiveMessageInput("Original", 600, true));

        ProactiveMessageDefinition? updated = await service.Update(
            created.Id,
            new ProactiveMessageInput("Replacement", 900, false));

        Assert.NotNull(updated);
        Assert.Equal("Replacement", updated.Message);
        Assert.Equal(900, updated.IntervalSeconds);
        Assert.False(updated.IsEnabled);
    }

    [Fact]
    public async Task WhenUpdatingMissingAutoresponse_ThenNullIsReturned()
    {
        ProactiveMessageManagementService service = new(DbContextFactory);

        ProactiveMessageDefinition? updated = await service.Update(
            4242,
            new ProactiveMessageInput("Nowhere", 600, true));

        Assert.Null(updated);
    }

    [Fact]
    public async Task WhenDeletingAutoresponse_ThenItIsRemoved()
    {
        ProactiveMessageManagementService service = new(DbContextFactory);
        ProactiveMessageDefinition created = await service.Create(
            new ProactiveMessageInput("Temporary", 600, true));

        bool deleted = await service.Delete(created.Id);
        IReadOnlyList<ProactiveMessageDefinition> messages = await service.List();

        Assert.True(deleted);
        Assert.DoesNotContain(messages, message => message.Id == created.Id);
    }

    [Fact]
    public async Task WhenDisabled_ThenTheAutoresponseStillListsForOperators()
    {
        ProactiveMessageManagementService service = new(DbContextFactory);
        ProactiveMessageCatalog catalog = new(DbContextFactory);
        ProactiveMessageDefinition created = await service.Create(
            new ProactiveMessageInput("Paused message", 600, false));

        IReadOnlyList<ProactiveMessageDefinition> managed = await service.List();
        IReadOnlyList<ProactiveMessageDefinition> enabled = await catalog.GetEnabledMessages();

        Assert.Contains(managed, message => message.Id == created.Id);
        Assert.DoesNotContain(enabled, message => message.Id == created.Id);
    }
}
