using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Thiccdal.Data.Models;
using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Infrastructure.Remotes;

namespace Thiccdal.Data.Tests;

public sealed class PlatformUserServiceTests : ApplicationDbContextTestFixture
{
    [Fact]
    public async Task WhenNewUser_ThenPlatformUserRowIsCreated()
    {
        PlatformUserService platformUserService = new(
            DbContextFactory,
            new NullLogger<PlatformUserService>(),
            Options.Create(new UserIdentityOptions()));
        DateTime seenAt = DateTime.UtcNow;

        long id = await platformUserService.Upsert(
            PlatformEventSource.Twitch,
            "viewer-42",
            "viewer",
            seenAt);

        await using ApplicationDbContext dbContext = await CreateDbContextAsync();
        List<PlatformUser> users = await dbContext.PlatformUsers.ToListAsync();

        Assert.Single(users);
        Assert.Equal(id, users[0].Id);
        Assert.Equal("viewer", users[0].DisplayName);
    }

    [Fact]
    public async Task WhenExistingUserSameDisplayName_ThenNoExtraRowCreated()
    {
        PlatformUserService platformUserService = new(
            DbContextFactory,
            new NullLogger<PlatformUserService>(),
            Options.Create(new UserIdentityOptions()));
        DateTime firstSeen = DateTime.UtcNow.AddMinutes(-5);
        DateTime secondSeen = DateTime.UtcNow;

        long firstId = await platformUserService.Upsert(
            PlatformEventSource.Twitch,
            "viewer-42",
            "viewer",
            firstSeen);
        long secondId = await platformUserService.Upsert(
            PlatformEventSource.Twitch,
            "viewer-42",
            "viewer",
            secondSeen);

        await using ApplicationDbContext dbContext = await CreateDbContextAsync();
        List<PlatformUser> users = await dbContext.PlatformUsers.ToListAsync();

        Assert.Single(users);
        Assert.Equal(firstId, secondId);
        Assert.Equal(secondSeen, users[0].LastSeen);
    }

    [Fact]
    public async Task WhenExistingUserDisplayNameChanged_ThenDisplayNameIsUpdated()
    {
        PlatformUserService platformUserService = new(
            DbContextFactory,
            new NullLogger<PlatformUserService>(),
            Options.Create(new UserIdentityOptions()));
        DateTime firstSeen = DateTime.UtcNow.AddMinutes(-5);
        DateTime lastSeen = DateTime.UtcNow;

        long firstId = await platformUserService.Upsert(
            PlatformEventSource.Twitch,
            "viewer-42",
            "old-name",
            firstSeen);
        long secondId = await platformUserService.Upsert(
            PlatformEventSource.Twitch,
            "viewer-42",
            "new-name",
            lastSeen);

        await using ApplicationDbContext dbContext = await CreateDbContextAsync();
        List<PlatformUser> users = await dbContext.PlatformUsers.ToListAsync();

        Assert.Single(users);
        Assert.Equal(firstId, secondId);
        Assert.Equal("new-name", users[0].DisplayName);
        Assert.Equal(lastSeen, users[0].LastSeen);
    }

    [Fact]
    public async Task WhenTwoPlatformsShareExternalId_ThenSeparateRowsAreCreated()
    {
        PlatformUserService platformUserService = new(
            DbContextFactory,
            new NullLogger<PlatformUserService>(),
            Options.Create(new UserIdentityOptions()));

        await platformUserService.Upsert(
            PlatformEventSource.Twitch,
            "viewer-42",
            "viewer",
            DateTime.UtcNow.AddMinutes(-1));
        await platformUserService.Upsert(
            PlatformEventSource.YouTube,
            "viewer-42",
            "viewer",
            DateTime.UtcNow);

        await using ApplicationDbContext dbContext = await CreateDbContextAsync();
        List<PlatformUser> users = await dbContext.PlatformUsers
            .OrderBy(user => user.Source)
            .ToListAsync();

        Assert.Equal(2, users.Count);
        Assert.NotEqual(users[0].Source, users[1].Source);
    }

    [Fact]
    public async Task WhenNewUserCloselyMatchesOtherPlatformDisplayName_ThenPendingSuggestionIsCreated()
    {
        PlatformUserService platformUserService = new(
            DbContextFactory,
            new NullLogger<PlatformUserService>(),
            Options.Create(new UserIdentityOptions()));

        long existingUserId = await platformUserService.Upsert(
            PlatformEventSource.YouTube,
            "alice-yt",
            "Alice_YT",
            DateTime.UtcNow.AddMinutes(-1));
        long newUserId = await platformUserService.Upsert(
            PlatformEventSource.Twitch,
            "alice-ttv",
            "xXxAlice_TTV",
            DateTime.UtcNow);

        await using ApplicationDbContext dbContext = await CreateDbContextAsync();
        UserIdentitySuggestion? suggestion = await dbContext.UserIdentitySuggestions.SingleOrDefaultAsync();

        Assert.NotNull(suggestion);
        Assert.Equal(Math.Min(existingUserId, newUserId), suggestion.FirstPlatformUserId);
        Assert.Equal(Math.Max(existingUserId, newUserId), suggestion.SecondPlatformUserId);
        Assert.Equal(UserIdentitySuggestionStatus.Pending, suggestion.Status);
        Assert.True(suggestion.SimilarityScore >= 0.85d);
    }

    [Fact]
    public async Task WhenExistingUserIsUpdated_ThenNewSuggestionIsNotCreated()
    {
        PlatformUserService platformUserService = new(
            DbContextFactory,
            new NullLogger<PlatformUserService>(),
            Options.Create(new UserIdentityOptions()));

        await platformUserService.Upsert(
            PlatformEventSource.YouTube,
            "alice-yt",
            "Alice_YT",
            DateTime.UtcNow.AddMinutes(-2));
        await platformUserService.Upsert(
            PlatformEventSource.Twitch,
            "alice-ttv",
            "xXxAlice_TTV",
            DateTime.UtcNow.AddMinutes(-1));

        await platformUserService.Upsert(
            PlatformEventSource.Twitch,
            "alice-ttv",
            "AliceTV",
            DateTime.UtcNow);

        await using ApplicationDbContext dbContext = await CreateDbContextAsync();
        List<UserIdentitySuggestion> suggestions = await dbContext.UserIdentitySuggestions.ToListAsync();

        Assert.Single(suggestions);
    }

    [Fact]
    public async Task WhenSimilarityFallsBelowConfiguredThreshold_ThenSuggestionIsSkipped()
    {
        PlatformUserService platformUserService = new(
            DbContextFactory,
            new NullLogger<PlatformUserService>(),
            Options.Create(new UserIdentityOptions
            {
                SimilarityThreshold = 0.96d
            }));

        await platformUserService.Upsert(
            PlatformEventSource.YouTube,
            "alice-yt",
            "Alice_YT",
            DateTime.UtcNow.AddMinutes(-1));
        await platformUserService.Upsert(
            PlatformEventSource.Twitch,
            "alice-ttv",
            "AliceTV",
            DateTime.UtcNow);

        await using ApplicationDbContext dbContext = await CreateDbContextAsync();

        Assert.Empty(await dbContext.UserIdentitySuggestions.ToListAsync());
    }
}
