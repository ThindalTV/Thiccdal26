using Microsoft.EntityFrameworkCore;
using Thiccdal.Data.Models;

namespace Thiccdal.Data.Tests;

public sealed class InMemoryApplicationDbContextFactoryTests : ApplicationDbContextTestFixture
{
    [Fact]
    public async Task WhenCreatingMultipleContexts_ThenTheyShareTheSameInMemoryStore()
    {
        await using ApplicationDbContext firstContext = await CreateDbContextAsync();
        firstContext.TwitchTokens.Add(new TwitchToken
        {
            AccessToken = "access-token",
            RefreshToken = "refresh-token",
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        });

        await firstContext.SaveChangesAsync();

        await using ApplicationDbContext secondContext = await CreateDbContextAsync();
        TwitchToken? token = await secondContext.TwitchTokens.SingleOrDefaultAsync();

        Assert.NotNull(token);
        Assert.Equal("access-token", token.AccessToken);
    }

    [Fact]
    public async Task WhenResettingTheFixture_ThenTheInMemoryDatabaseIsCleared()
    {
        await using ApplicationDbContext context = await CreateDbContextAsync();
        context.TwitchTokens.Add(new TwitchToken
        {
            AccessToken = "access-token",
            RefreshToken = "refresh-token",
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        });

        await context.SaveChangesAsync();

        await ResetDatabase();

        await using ApplicationDbContext resetContext = await CreateDbContextAsync();

        Assert.Empty(await resetContext.TwitchTokens.ToListAsync());
    }
}
