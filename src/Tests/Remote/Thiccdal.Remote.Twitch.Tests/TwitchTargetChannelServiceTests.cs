using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Thiccdal.Data;
using Thiccdal.Data.Models;
using Thiccdal.Infrastructure.Twitch;
using Thiccdal.Remote.Twitch;

namespace Thiccdal.Remote.Twitch.Tests;

public class TwitchTargetChannelServiceTests
{
    [Fact]
    public async Task WhenNoOverrideExists_ThenProfileUsesStoredTokenUserInfo()
    {
        var dbName = nameof(WhenNoOverrideExists_ThenProfileUsesStoredTokenUserInfo);
        var factory = new TestDbContextFactory(dbName);
        
        await using (var context = factory.CreateDbContext())
        {
            context.TwitchTokens.Add(new TwitchToken
            {
                AccessToken = "test-token",
                RefreshToken = "test-refresh",
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                Username = "riverbot",
                UserId = "24680"
            });
            await context.SaveChangesAsync();
        }

        var service = CreateService(dbName);

        TwitchChatConnectionProfile profile = await service.GetConnectionProfile();

        Assert.Equal("riverbot", profile.BotUsername);
        Assert.Equal("24680", profile.BotUserId);
        Assert.Equal(string.Empty, profile.TargetChannel);
        Assert.Equal(string.Empty, profile.BroadcasterId);
    }

    [Fact]
    public async Task WhenTargetChannelUpdated_ThenOverridePersistsAndBotIdentityStaysSeparate()
    {
        var dbName = nameof(WhenTargetChannelUpdated_ThenOverridePersistsAndBotIdentityStaysSeparate);
        var factory = new TestDbContextFactory(dbName);
        
        await using (var context = factory.CreateDbContext())
        {
            context.TwitchTokens.Add(new TwitchToken
            {
                AccessToken = "test-token",
                RefreshToken = "test-refresh",
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                Username = "riverbot",
                UserId = "24680"
            });
            await context.SaveChangesAsync();
        }

        var service = CreateService(dbName);

        TwitchChatConnectionProfile profile = await service.UpdateTargetChannel(new TwitchTargetChannelSettings("@GuestCaster", "98765"));

        Assert.Equal("riverbot", profile.BotUsername);
        Assert.Equal("24680", profile.BotUserId);
        Assert.Equal("guestcaster", profile.TargetChannel);
        Assert.Equal("98765", profile.BroadcasterId);

        TwitchChatConnectionProfile persistedProfile = await service.GetConnectionProfile();
        Assert.Equal(profile, persistedProfile);
    }

    [Fact]
    public async Task WhenTargetChannelChanges_ThenConnectionProfileChangedIsRaisedOnce()
    {
        var dbName = nameof(WhenTargetChannelChanges_ThenConnectionProfileChangedIsRaisedOnce);
        var factory = new TestDbContextFactory(dbName);
        
        await using (var context = factory.CreateDbContext())
        {
            context.TwitchTokens.Add(new TwitchToken
            {
                AccessToken = "test-token",
                RefreshToken = "test-refresh",
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                Username = "riverbot",
                UserId = "24680"
            });
            await context.SaveChangesAsync();
        }

        var service = CreateService(dbName);

        int eventCount = 0;
        TwitchChatConnectionProfile? raisedProfile = null;
        service.ConnectionProfileChanged += (_, profile) =>
        {
            eventCount++;
            raisedProfile = profile;
        };

        await service.UpdateTargetChannel(new TwitchTargetChannelSettings("guestcaster", "98765"));
        await service.UpdateTargetChannel(new TwitchTargetChannelSettings("guestcaster", "98765"));

        Assert.Equal(1, eventCount);
        Assert.NotNull(raisedProfile);
        Assert.Equal("guestcaster", raisedProfile!.TargetChannel);
        Assert.Equal("riverbot", raisedProfile.BotUsername);
        Assert.Equal("24680", raisedProfile.BotUserId);
    }

    [Fact]
    public async Task WhenTargetChannelContainsInvalidCharacters_ThenUpdateRejectsIt()
    {
        var service = CreateService(nameof(WhenTargetChannelContainsInvalidCharacters_ThenUpdateRejectsIt));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdateTargetChannel(new TwitchTargetChannelSettings("guest-channel", "98765")));
    }

    private static TwitchTargetChannelService CreateService(string databaseName)
    {
        return new TwitchTargetChannelService(
            new TestDbContextFactory(databaseName),
            NullLogger<TwitchTargetChannelService>.Instance);
    }

    private sealed class TestDbContextFactory : IDbContextFactory<ApplicationDbContext>
    {
        private readonly DbContextOptions<ApplicationDbContext> _options;

        public TestDbContextFactory(string databaseName)
        {
            _options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName)
                .Options;
        }

        public ApplicationDbContext CreateDbContext() => new(_options);

        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }
}
