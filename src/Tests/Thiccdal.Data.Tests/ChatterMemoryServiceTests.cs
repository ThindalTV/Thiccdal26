using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Thiccdal.Data.Models;
using Thiccdal.Infrastructure.Bot;
using Thiccdal.Infrastructure.Bot.Models;

namespace Thiccdal.Data.Tests;

public sealed class ChatterMemoryServiceTests : ApplicationDbContextTestFixture
{
    [Fact]
    public async Task WhenHistoryMatchesExactScope_ThenMemoryUsesOnlyThatScope()
    {
        await SeedMessage(PlatformEventSource.Twitch, "main-channel", "viewer-1", "Kaylee", "I like soulslikes", DateTime.UtcNow.AddMinutes(-10));
        await SeedMessage(PlatformEventSource.Twitch, "main-channel", "viewer-1", "Kaylee", "Speedruns tonight sound fun", DateTime.UtcNow.AddMinutes(-5));
        await SeedMessage(PlatformEventSource.Twitch, "other-channel", "viewer-1", "Kaylee", "I like chess", DateTime.UtcNow.AddMinutes(-4));
        await SeedMessage(PlatformEventSource.Null, "main-channel", "viewer-1", "Kaylee", "I like platformers", DateTime.UtcNow.AddMinutes(-3));
        await SeedMessage(PlatformEventSource.Twitch, "main-channel", "viewer-2", "River", "I like tactics games", DateTime.UtcNow.AddMinutes(-2));

        ChatterMemoryService service = CreateService();

        ChatterMemoryContext? memoryContext = await service.GetMemoryContext(
            PlatformEventSource.Twitch,
            "main-channel",
            "viewer-1");

        Assert.NotNull(memoryContext);
        Assert.Equal("Kaylee", memoryContext.DisplayName);
        Assert.Contains(memoryContext.Facts, fact => fact.Contains("soulslikes", StringComparison.Ordinal));
        Assert.DoesNotContain(memoryContext.Facts, fact => fact.Contains("chess", StringComparison.Ordinal));
        Assert.DoesNotContain(memoryContext.Facts, fact => fact.Contains("platformers", StringComparison.Ordinal));
        Assert.DoesNotContain(memoryContext.Facts, fact => fact.Contains("tactics", StringComparison.Ordinal));
    }

    [Fact]
    public async Task WhenHistoryContainsSensitiveContent_ThenSensitiveFactsAreFiltered()
    {
        await SeedMessage(PlatformEventSource.Twitch, "main-channel", "viewer-1", "Kaylee", "I like pizza", DateTime.UtcNow.AddMinutes(-10));
        await SeedMessage(PlatformEventSource.Twitch, "main-channel", "viewer-1", "Kaylee", "My password is supersecretvalue1234567890", DateTime.UtcNow.AddMinutes(-9));
        await SeedMessage(PlatformEventSource.Twitch, "main-channel", "viewer-1", "Kaylee", "I live at 123 Main Street", DateTime.UtcNow.AddMinutes(-8));
        await SeedMessage(PlatformEventSource.Twitch, "main-channel", "viewer-1", "Kaylee", "Check https://example.com/reset?token=abcdef1234567890", DateTime.UtcNow.AddMinutes(-7));

        ChatterMemoryService service = CreateService();

        ChatterMemoryContext? memoryContext = await service.GetMemoryContext(
            PlatformEventSource.Twitch,
            "main-channel",
            "viewer-1");

        Assert.NotNull(memoryContext);
        Assert.Contains(memoryContext.Facts, fact => fact.Contains("pizza", StringComparison.Ordinal));
        Assert.DoesNotContain(memoryContext.Facts, fact => fact.Contains("password", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(memoryContext.Facts, fact => fact.Contains("street", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(memoryContext.Facts, fact => fact.Contains("token", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WhenResettingSpecificScope_ThenOnlyOlderMemoryForThatScopeIsRemoved()
    {
        DateTimeOffset now = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
        await SeedMessage(PlatformEventSource.Twitch, "main-channel", "viewer-1", "Kaylee", "I like soulslikes", now.UtcDateTime.AddMinutes(-10));
        await SeedMessage(PlatformEventSource.Twitch, "other-channel", "viewer-1", "Kaylee", "I like chess", now.UtcDateTime.AddMinutes(-9));

        ChatterMemoryService service = CreateService(now: now);

        await service.Reset(PlatformEventSource.Twitch, "main-channel", "viewer-1", "operator");

        Assert.Null(await service.GetMemoryContext(PlatformEventSource.Twitch, "main-channel", "viewer-1"));
        Assert.NotNull(await service.GetMemoryContext(PlatformEventSource.Twitch, "other-channel", "viewer-1"));

        await using (ApplicationDbContext dbContext = await CreateDbContextAsync())
        {
            Assert.Equal(2, await dbContext.ChatMessages.CountAsync());
            Assert.Equal(2, await dbContext.PlatformEvents.CountAsync());
        }

        await SeedMessage(PlatformEventSource.Twitch, "main-channel", "viewer-1", "Kaylee", "I like metroidvanias", now.UtcDateTime.AddMinutes(1));

        ChatterMemoryContext? memoryContext = await CreateService(now: now.AddMinutes(2))
            .GetMemoryContext(PlatformEventSource.Twitch, "main-channel", "viewer-1");

        Assert.NotNull(memoryContext);
        Assert.Contains(memoryContext.Facts, fact => fact.Contains("metroidvanias", StringComparison.Ordinal));
        Assert.DoesNotContain(memoryContext.Facts, fact => fact.Contains("soulslikes", StringComparison.Ordinal));
    }

    [Fact]
    public async Task WhenResettingAllMemory_ThenOlderMemoryIsRemovedWithoutDeletingSourceRecords()
    {
        DateTimeOffset now = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
        await SeedMessage(PlatformEventSource.Twitch, "main-channel", "viewer-1", "Kaylee", "I like soulslikes", now.UtcDateTime.AddMinutes(-10));
        await SeedMessage(PlatformEventSource.Null, "yt-channel", "viewer-2", "River", "I like metroidvanias", now.UtcDateTime.AddMinutes(-9));

        ChatterMemoryService service = CreateService(now: now);

        await service.ResetAll("operator");

        Assert.Null(await service.GetMemoryContext(PlatformEventSource.Twitch, "main-channel", "viewer-1"));
        Assert.Null(await service.GetMemoryContext(PlatformEventSource.Null, "yt-channel", "viewer-2"));

        await using ApplicationDbContext dbContext = await CreateDbContextAsync();
        Assert.Equal(2, await dbContext.ChatMessages.CountAsync());
        Assert.Equal(2, await dbContext.PlatformEvents.CountAsync());
    }

    [Fact]
    public async Task WhenNoHistoryExists_ThenMemoryContextIsNull()
    {
        ChatterMemoryService service = CreateService();

        ChatterMemoryContext? memoryContext = await service.GetMemoryContext(
            PlatformEventSource.Twitch,
            "main-channel",
            "missing-user");

        Assert.Null(memoryContext);
    }

    [Fact]
    public async Task WhenMessagesArePositive_ThenSentimentIsPositive()
    {
        await SeedMessage(PlatformEventSource.Twitch, "main-channel", "viewer-1", "Kaylee", "I love this stream, it's amazing!", DateTime.UtcNow.AddMinutes(-10));
        await SeedMessage(PlatformEventSource.Twitch, "main-channel", "viewer-1", "Kaylee", "This is great, thanks so much! I enjoy watching you", DateTime.UtcNow.AddMinutes(-9));
        await SeedMessage(PlatformEventSource.Twitch, "main-channel", "viewer-1", "Kaylee", "I like awesome games, let's go!", DateTime.UtcNow.AddMinutes(-8));

        ChatterMemoryContext? memoryContext = await CreateService()
            .GetMemoryContext(PlatformEventSource.Twitch, "main-channel", "viewer-1");

        Assert.NotNull(memoryContext);
        Assert.Equal(SentimentLabel.Positive, memoryContext.RecentSentiment);
    }

    [Fact]
    public async Task WhenMessagesAreNegative_ThenSentimentIsNegative()
    {
        await SeedMessage(PlatformEventSource.Twitch, "main-channel", "viewer-1", "Kaylee", "I like games but this is terrible and boring", DateTime.UtcNow.AddMinutes(-10));
        await SeedMessage(PlatformEventSource.Twitch, "main-channel", "viewer-1", "Kaylee", "ugh, that was awful and horrible, smh", DateTime.UtcNow.AddMinutes(-9));
        await SeedMessage(PlatformEventSource.Twitch, "main-channel", "viewer-1", "Kaylee", "I enjoy nothing, this is trash and lame", DateTime.UtcNow.AddMinutes(-8));

        ChatterMemoryContext? memoryContext = await CreateService()
            .GetMemoryContext(PlatformEventSource.Twitch, "main-channel", "viewer-1");

        Assert.NotNull(memoryContext);
        Assert.Equal(SentimentLabel.Negative, memoryContext.RecentSentiment);
    }

    [Fact]
    public async Task WhenMessagesAreMixed_ThenSentimentIsNeutral()
    {
        await SeedMessage(PlatformEventSource.Twitch, "main-channel", "viewer-1", "Kaylee", "I love this, it's great and amazing!", DateTime.UtcNow.AddMinutes(-10));
        await SeedMessage(PlatformEventSource.Twitch, "main-channel", "viewer-1", "Kaylee", "I like games but this is terrible and boring", DateTime.UtcNow.AddMinutes(-9));

        ChatterMemoryContext? memoryContext = await CreateService()
            .GetMemoryContext(PlatformEventSource.Twitch, "main-channel", "viewer-1");

        Assert.NotNull(memoryContext);
        Assert.Equal(SentimentLabel.Neutral, memoryContext.RecentSentiment);
    }

    [Fact]
    public async Task WhenMessagesHaveNoSentimentWords_ThenSentimentIsUnknown()
    {
        await SeedMessage(PlatformEventSource.Twitch, "main-channel", "viewer-1", "Kaylee", "I prefer metroidvanias over platformers", DateTime.UtcNow.AddMinutes(-10));
        await SeedMessage(PlatformEventSource.Twitch, "main-channel", "viewer-1", "Kaylee", "My favorite genre is roguelike", DateTime.UtcNow.AddMinutes(-9));

        ChatterMemoryContext? memoryContext = await CreateService()
            .GetMemoryContext(PlatformEventSource.Twitch, "main-channel", "viewer-1");

        Assert.NotNull(memoryContext);
        Assert.Equal(SentimentLabel.Unknown, memoryContext.RecentSentiment);
    }

    private ChatterMemoryService CreateService(int? retentionDays = null, DateTimeOffset? now = null)
    {
        return new ChatterMemoryService(
            DbContextFactory,
            Options.Create(
                new ChatBotOptions
                {
                    AiResponder = new ChatBotAiResponderOptions
                    {
                        ChatterMemoryEnabled = true,
                        ChatterMemoryRetentionDays = retentionDays
                    }
                }),
            new FixedTimeProvider(now ?? DateTimeOffset.UtcNow),
            NullLogger<ChatterMemoryService>.Instance);
    }

    private async Task SeedMessage(
        PlatformEventSource source,
        string channel,
        string platformUserId,
        string displayName,
        string content,
        DateTime sentAt)
    {
        await using ApplicationDbContext dbContext = await CreateDbContextAsync();

        PlatformUser? platformUser = await dbContext.PlatformUsers.SingleOrDefaultAsync(
            user => user.Source == source && user.PlatformUserId == platformUserId);

        if (platformUser is null)
        {
            platformUser = new PlatformUser
            {
                Source = source,
                PlatformUserId = platformUserId,
                DisplayName = displayName,
                LastSeen = sentAt
            };

            dbContext.PlatformUsers.Add(platformUser);
        }
        else
        {
            platformUser.DisplayName = displayName;
            platformUser.LastSeen = sentAt;
        }

        Data.Models.PlatformEvent platformEvent = new()
        {
            Source = source,
            Type = PlatformEventType.ChatMessage,
            SourceEventType = "chat",
            ExternalId = Guid.NewGuid().ToString("N"),
            Author = displayName,
            Channel = channel,
            Summary = content,
            Content = content,
            HtmlContent = $"<span>{content}</span>",
            RawData = "{\"safe\":true}",
            OccurredAt = sentAt
        };

        dbContext.ChatMessages.Add(
            new ChatMessage
            {
                PlatformEvent = platformEvent,
                PlatformUser = platformUser,
                Source = source,
                Content = content,
                HtmlContent = $"<span>{content}</span>",
                RawData = "{\"safe\":true}",
                SentAt = sentAt
            });

        await dbContext.SaveChangesAsync();
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedTimeProvider(DateTimeOffset now)
        {
            _now = now;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _now;
        }
    }
}
