using Microsoft.EntityFrameworkCore;
using Thiccdal.Data.Models;
using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Infrastructure.Remotes.Models;

namespace Thiccdal.Data.Tests;

public sealed class UserIdentityServiceTests : ApplicationDbContextTestFixture
{
    [Fact]
    public async Task WhenSearchMatchesDisplayNameAcrossPlatforms_ThenMatchingRowsAreReturned()
    {
        await SeedPlatformUser(PlatformEventSource.Twitch, "alice-twitch", "AliceTV");
        await SeedPlatformUser(PlatformEventSource.Null, "alice-youtube", "Alice_YT");
        await SeedPlatformUser(PlatformEventSource.Null, "bob-discord", "Bob");

        UserIdentityService service = new(DbContextFactory);

        IReadOnlyList<UserIdentitySearchResult> results = await service.Search("Alice");

        Assert.Equal(2, results.Count);
        Assert.Contains(results, static result => result.Source == PlatformEventSource.Twitch);
        Assert.Contains(results, static result => result.Source == PlatformEventSource.Null);
    }

    [Fact]
    public async Task WhenMergeHasNoExistingIdentity_ThenCanonicalIdentityIsCreatedAndSuggestionAccepted()
    {
        long firstUserId = await SeedPlatformUser(PlatformEventSource.Twitch, "alice-twitch", "AliceTV");
        long secondUserId = await SeedPlatformUser(PlatformEventSource.Null, "alice-youtube", "Alice_YT");

        await using (ApplicationDbContext seedContext = await CreateDbContextAsync())
        {
            seedContext.UserIdentitySuggestions.Add(
                new UserIdentitySuggestion
                {
                    FirstPlatformUserId = Math.Min(firstUserId, secondUserId),
                    SecondPlatformUserId = Math.Max(firstUserId, secondUserId),
                    SimilarityScore = 0.91d
                });
            await seedContext.SaveChangesAsync();
        }

        UserIdentityService service = new(DbContextFactory);

        UserIdentityMergeResult result = await service.Merge(
            [firstUserId, secondUserId],
            "Alice");

        await using ApplicationDbContext assertContext = await CreateDbContextAsync();
        UserIdentity storedIdentity = await assertContext.UserIdentities
            .Include(identity => identity.PlatformUsers)
            .SingleAsync();
        UserIdentitySuggestion storedSuggestion = await assertContext.UserIdentitySuggestions.SingleAsync();

        Assert.Equal(storedIdentity.Id, result.UserIdentityId);
        Assert.Equal("Alice", storedIdentity.DisplayName);
        Assert.Equal(2, storedIdentity.PlatformUsers.Count);
        Assert.Equal(UserIdentitySuggestionStatus.Accepted, storedSuggestion.Status);
    }

    [Fact]
    public async Task WhenMergeIncludesExistingIdentities_ThenRowsAreConsolidatedIntoOneIdentity()
    {
        await using (ApplicationDbContext seedContext = await CreateDbContextAsync())
        {
            UserIdentity firstIdentity = new()
            {
                DisplayName = "Alice"
            };
            UserIdentity secondIdentity = new()
            {
                DisplayName = "Alice Alt"
            };

            PlatformUser twitchUser = new()
            {
                Source = PlatformEventSource.Twitch,
                PlatformUserId = "alice-twitch",
                DisplayName = "AliceTV",
                UserIdentity = firstIdentity
            };
            PlatformUser youTubeUser = new()
            {
                Source = PlatformEventSource.Null,
                PlatformUserId = "alice-youtube",
                DisplayName = "Alice_YT",
                UserIdentity = secondIdentity
            };
            PlatformUser discordUser = new()
            {
                Source = PlatformEventSource.Null,
                PlatformUserId = "alice-discord",
                DisplayName = "AliceDisc",
                UserIdentity = secondIdentity
            };

            seedContext.PlatformUsers.AddRange(twitchUser, youTubeUser, discordUser);
            await seedContext.SaveChangesAsync();
        }

        long[] userIds;
        await using (ApplicationDbContext readContext = await CreateDbContextAsync())
        {
            userIds = await readContext.PlatformUsers
                .OrderBy(platformUser => platformUser.Id)
                .Select(platformUser => platformUser.Id)
                .ToArrayAsync();
        }

        UserIdentityService service = new(DbContextFactory);

        await service.Merge(userIds.Take(2).ToArray(), "Alice Prime");

        await using ApplicationDbContext assertContext = await CreateDbContextAsync();
        List<UserIdentity> identities = await assertContext.UserIdentities
            .Include(identity => identity.PlatformUsers)
            .OrderBy(identity => identity.Id)
            .ToListAsync();

        Assert.Single(identities);
        Assert.Equal("Alice Prime", identities[0].DisplayName);
        Assert.Equal(3, identities[0].PlatformUsers.Count);
    }

    [Fact]
    public async Task WhenMergeTargetsSelectedIdentity_ThenChosenIdentityKeepsItsIdAndAllRowsMoveIntoIt()
    {
        long twitchUserId;
        long youTubeUserId;
        long discordUserId;
        int targetIdentityId;

        await using (ApplicationDbContext seedContext = await CreateDbContextAsync())
        {
            UserIdentity twitchIdentity = new()
            {
                DisplayName = "Alice Twitch"
            };
            UserIdentity youTubeIdentity = new()
            {
                DisplayName = "Alice Alt"
            };

            PlatformUser twitchUser = new()
            {
                Source = PlatformEventSource.Twitch,
                PlatformUserId = "alice-twitch",
                DisplayName = "AliceTV",
                UserIdentity = twitchIdentity
            };
            PlatformUser youTubeUser = new()
            {
                Source = PlatformEventSource.Null,
                PlatformUserId = "alice-youtube",
                DisplayName = "Alice_YT",
                UserIdentity = youTubeIdentity
            };
            PlatformUser discordUser = new()
            {
                Source = PlatformEventSource.Null,
                PlatformUserId = "alice-discord",
                DisplayName = "AliceDisc",
                UserIdentity = youTubeIdentity
            };

            seedContext.PlatformUsers.AddRange(twitchUser, youTubeUser, discordUser);
            await seedContext.SaveChangesAsync();

            twitchUserId = twitchUser.Id;
            youTubeUserId = youTubeUser.Id;
            discordUserId = discordUser.Id;
            targetIdentityId = youTubeIdentity.Id;
        }

        UserIdentityService service = new(DbContextFactory);

        UserIdentityMergeResult result = await service.Merge(
            new UserIdentityMergeRequest(
                [twitchUserId, youTubeUserId],
                youTubeUserId,
                null));

        await using ApplicationDbContext assertContext = await CreateDbContextAsync();
        List<UserIdentity> identities = await assertContext.UserIdentities
            .Include(identity => identity.PlatformUsers)
            .OrderBy(identity => identity.Id)
            .ToListAsync();

        Assert.Single(identities);
        Assert.Equal(targetIdentityId, result.UserIdentityId);
        Assert.Equal(targetIdentityId, identities[0].Id);
        Assert.Equal("Alice Alt", result.DisplayName);
        Assert.Equal([twitchUserId, youTubeUserId, discordUserId], result.PlatformUserIds.OrderBy(static id => id).ToArray());
        Assert.Equal(3, identities[0].PlatformUsers.Count);
    }

    [Fact]
    public async Task WhenMergeMovesWholeIdentity_ThenPendingSuggestionsForMovedRowsAreAccepted()
    {
        long twitchUserId;
        long youTubeUserId;
        long discordUserId;

        await using (ApplicationDbContext seedContext = await CreateDbContextAsync())
        {
            UserIdentity firstIdentity = new()
            {
                DisplayName = "Alice"
            };
            UserIdentity secondIdentity = new()
            {
                DisplayName = "Alice Alt"
            };

            PlatformUser twitchUser = new()
            {
                Source = PlatformEventSource.Twitch,
                PlatformUserId = "alice-twitch",
                DisplayName = "AliceTV",
                UserIdentity = firstIdentity
            };
            PlatformUser youTubeUser = new()
            {
                Source = PlatformEventSource.Null,
                PlatformUserId = "alice-youtube",
                DisplayName = "Alice_YT",
                UserIdentity = secondIdentity
            };
            PlatformUser discordUser = new()
            {
                Source = PlatformEventSource.Null,
                PlatformUserId = "alice-discord",
                DisplayName = "AliceDisc",
                UserIdentity = secondIdentity
            };

            seedContext.PlatformUsers.AddRange(twitchUser, youTubeUser, discordUser);
            await seedContext.SaveChangesAsync();

            twitchUserId = twitchUser.Id;
            youTubeUserId = youTubeUser.Id;
            discordUserId = discordUser.Id;

            seedContext.UserIdentitySuggestions.AddRange(
                new UserIdentitySuggestion
                {
                    FirstPlatformUserId = Math.Min(twitchUserId, youTubeUserId),
                    SecondPlatformUserId = Math.Max(twitchUserId, youTubeUserId),
                    SimilarityScore = 0.91d
                },
                new UserIdentitySuggestion
                {
                    FirstPlatformUserId = Math.Min(twitchUserId, discordUserId),
                    SecondPlatformUserId = Math.Max(twitchUserId, discordUserId),
                    SimilarityScore = 0.88d
                });

            await seedContext.SaveChangesAsync();
        }

        UserIdentityService service = new(DbContextFactory);

        await service.Merge(
            new UserIdentityMergeRequest(
                [twitchUserId, youTubeUserId],
                youTubeUserId,
                "Alice Prime"));

        await using ApplicationDbContext assertContext = await CreateDbContextAsync();
        List<UserIdentitySuggestion> suggestions = await assertContext.UserIdentitySuggestions
            .OrderBy(suggestion => suggestion.Id)
            .ToListAsync();

        Assert.All(suggestions, suggestion => Assert.Equal(UserIdentitySuggestionStatus.Accepted, suggestion.Status));
    }

    [Fact]
    public async Task WhenMergeTargetIsNotSelected_ThenMergeThrows()
    {
        long firstUserId = await SeedPlatformUser(PlatformEventSource.Twitch, "alice-twitch", "AliceTV");
        long secondUserId = await SeedPlatformUser(PlatformEventSource.Null, "alice-youtube", "Alice_YT");

        UserIdentityService service = new(DbContextFactory);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.Merge(
                new UserIdentityMergeRequest(
                    [firstUserId, secondUserId],
                    9999,
                    null)));

        Assert.Equal("Choose one of the selected viewer rows as the merge target.", exception.Message);
    }

    [Fact]
    public async Task WhenUnlinkRemovesLastMember_ThenIdentityIsDeleted()
    {
        long platformUserId;

        await using (ApplicationDbContext seedContext = await CreateDbContextAsync())
        {
            UserIdentity identity = new()
            {
                DisplayName = "Alice"
            };
            PlatformUser platformUser = new()
            {
                Source = PlatformEventSource.Twitch,
                PlatformUserId = "alice-twitch",
                DisplayName = "AliceTV",
                UserIdentity = identity
            };

            seedContext.PlatformUsers.Add(platformUser);
            await seedContext.SaveChangesAsync();
            platformUserId = platformUser.Id;
        }

        UserIdentityService service = new(DbContextFactory);

        await service.Unlink(platformUserId);

        await using ApplicationDbContext assertContext = await CreateDbContextAsync();
        PlatformUser storedUser = await assertContext.PlatformUsers.SingleAsync();

        Assert.Null(storedUser.UserIdentityId);
        Assert.Empty(await assertContext.UserIdentities.ToListAsync());
    }

    private async Task<long> SeedPlatformUser(
        PlatformEventSource source,
        string platformUserId,
        string displayName)
    {
        await using ApplicationDbContext dbContext = await CreateDbContextAsync();
        PlatformUser platformUser = new()
        {
            Source = source,
            PlatformUserId = platformUserId,
            DisplayName = displayName,
            LastSeen = DateTime.UtcNow
        };

        dbContext.PlatformUsers.Add(platformUser);
        await dbContext.SaveChangesAsync();
        return platformUser.Id;
    }
}
