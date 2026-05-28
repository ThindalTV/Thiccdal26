using Thiccdal.Data.Models;

namespace Thiccdal.Data.Tests;

public sealed class ProactiveMessageCatalogTests : ApplicationDbContextTestFixture
{
    [Fact]
    public async Task WhenListingEnabledMessages_ThenDisabledEntriesAreFilteredOut()
    {
        await using ApplicationDbContext dbContext = await CreateDbContextAsync();
        dbContext.ProactiveMessages.AddRange(
            new ProactiveMessage
            {
                Message = "Stay hydrated",
                IntervalSeconds = 60,
                IsEnabled = true
            },
            new ProactiveMessage
            {
                Message = "Hidden",
                IntervalSeconds = 60,
                IsEnabled = false
            });
        await dbContext.SaveChangesAsync();

        ProactiveMessageCatalog catalog = new(DbContextFactory);

        IReadOnlyList<Thiccdal.Infrastructure.Bot.ProactiveMessageDefinition> messages = await catalog.GetEnabledMessages();

        Assert.Single(messages);
        Assert.Equal("Stay hydrated", messages[0].Message);
    }

    [Fact]
    public async Task WhenMarkingMessageSent_ThenTimestampIsPersisted()
    {
        await using ApplicationDbContext dbContext = await CreateDbContextAsync();
        ProactiveMessage entity = new()
        {
            Message = "Stay hydrated",
            IntervalSeconds = 60,
            IsEnabled = true
        };
        dbContext.ProactiveMessages.Add(entity);
        await dbContext.SaveChangesAsync();

        ProactiveMessageCatalog catalog = new(DbContextFactory);
        DateTimeOffset sentAt = new(2026, 05, 29, 12, 00, 00, TimeSpan.Zero);

        await catalog.MarkSent(entity.Id, sentAt);

        await using ApplicationDbContext verificationContext = await CreateDbContextAsync();
        Assert.Equal(sentAt, verificationContext.ProactiveMessages.Single().LastSentAt);
    }
}
