using Microsoft.EntityFrameworkCore;
using Thiccdal.Data.Models;
using Thiccdal.Infrastructure.Bot.Models;

namespace Thiccdal.Data.Tests;

public sealed class UserIdentityPersistenceTests : ApplicationDbContextTestFixture
{
    [Fact]
    public async Task WhenPlatformUsersShareIdentity_ThenRelationshipIsPersisted()
    {
        UserIdentity identity = new()
        {
            DisplayName = "Alice"
        };

        identity.PlatformUsers.Add(new PlatformUser
        {
            Source = PlatformEventSource.Twitch,
            PlatformUserId = "alice-twitch",
            DisplayName = "AliceTV"
        });
        identity.PlatformUsers.Add(new PlatformUser
        {
            Source = PlatformEventSource.Null,
            PlatformUserId = "alice-youtube",
            DisplayName = "Alice_YT"
        });

        await using (ApplicationDbContext dbContext = await CreateDbContextAsync())
        {
            dbContext.UserIdentities.Add(identity);
            await dbContext.SaveChangesAsync();
        }

        await using ApplicationDbContext assertContext = await CreateDbContextAsync();
        UserIdentity? storedIdentity = await assertContext.UserIdentities
            .Include(userIdentity => userIdentity.PlatformUsers)
            .SingleOrDefaultAsync();

        Assert.NotNull(storedIdentity);
        Assert.Equal("Alice", storedIdentity.DisplayName);
        Assert.Equal(2, storedIdentity.PlatformUsers.Count);
        Assert.All(storedIdentity.PlatformUsers, platformUser => Assert.Equal(storedIdentity.Id, platformUser.UserIdentityId));
    }

    [Fact]
    public async Task WhenPlatformUserHasNoIdentity_ThenNullableForeignKeyRemainsUnset()
    {
        PlatformUser platformUser = new()
        {
            Source = PlatformEventSource.Twitch,
            PlatformUserId = "viewer-42",
            DisplayName = "viewer"
        };

        await using (ApplicationDbContext dbContext = await CreateDbContextAsync())
        {
            dbContext.PlatformUsers.Add(platformUser);
            await dbContext.SaveChangesAsync();
        }

        await using ApplicationDbContext assertContext = await CreateDbContextAsync();
        PlatformUser? storedUser = await assertContext.PlatformUsers.SingleOrDefaultAsync();

        Assert.NotNull(storedUser);
        Assert.Null(storedUser.UserIdentityId);
        Assert.Null(storedUser.UserIdentity);
    }

    [Fact]
    public async Task WhenIdentityIsDeleted_ThenLinkedPlatformUsersRemainAndForeignKeysAreCleared()
    {
        string databasePath = Path.Combine(
            AppContext.BaseDirectory,
            "UserIdentityPersistenceTests",
            nameof(WhenIdentityIsDeleted_ThenLinkedPlatformUsersRemainAndForeignKeysAreCleared),
            "user-identity.db");
        string? directoryPath = Path.GetDirectoryName(databasePath);
        Assert.NotNull(directoryPath);
        Directory.CreateDirectory(directoryPath);

        if (File.Exists(databasePath))
        {
            File.Delete(databasePath);
        }

        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;

        await using (ApplicationDbContext initializationContext = new(options))
        {
            await initializationContext.Database.EnsureDeletedAsync();
            await initializationContext.Database.EnsureCreatedAsync();
        }

        UserIdentity identity = new()
        {
            DisplayName = "Alice"
        };

        identity.PlatformUsers.Add(new PlatformUser
        {
            Source = PlatformEventSource.Twitch,
            PlatformUserId = "alice-twitch",
            DisplayName = "AliceTV"
        });

        await using (ApplicationDbContext setupContext = new(options))
        {
            setupContext.UserIdentities.Add(identity);
            await setupContext.SaveChangesAsync();
        }

        await using (ApplicationDbContext deleteContext = new(options))
        {
            UserIdentity? storedIdentity = await deleteContext.UserIdentities.SingleOrDefaultAsync();
            Assert.NotNull(storedIdentity);

            deleteContext.UserIdentities.Remove(storedIdentity);
            await deleteContext.SaveChangesAsync();
        }

        await using ApplicationDbContext assertContext = new(options);
        PlatformUser? storedUser = await assertContext.PlatformUsers.SingleOrDefaultAsync();

        Assert.NotNull(storedUser);
        Assert.Null(storedUser.UserIdentityId);
        Assert.Null(storedUser.UserIdentity);
    }
}
