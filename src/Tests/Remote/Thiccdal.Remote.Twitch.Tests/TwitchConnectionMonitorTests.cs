using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Thiccdal.Data;
using Thiccdal.Data.Models;
using Thiccdal.Infrastructure.Twitch;
using Thiccdal.Remote.Twitch;

namespace Thiccdal.Remote.Twitch.Tests;

public class TwitchConnectionMonitorTests
{
    private static DbContextOptions<ApplicationDbContext> BuildOptions() =>
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    private static TwitchConnectionMonitor BuildMonitor(DbContextOptions<ApplicationDbContext> options)
    {
        var twitchOpts = Options.Create(new TwitchOptions
        {
            ClientId = "test-client-id",
            ClientSecret = "test-secret",
            RedirectUri = "https://localhost/auth/twitch/callback"
        });

        var tokenManager = new TwitchTokenManager(
            twitchOpts,
            new NullLogger<TwitchTokenManager>(),
            new TestHttpClientFactory(),
            new TestDbContextFactory(options));

        return new TwitchConnectionMonitor(
            tokenManager,
            new TestDbContextFactory(options),
            new NullLogger<TwitchConnectionMonitor>());
    }

    [Fact]
    public async Task WhenNoTokenExists_ThenIsConnectedIsFalse()
    {
        var options = BuildOptions();
        var monitor = BuildMonitor(options);

        await monitor.RefreshConnectionState();

        Assert.False(monitor.IsConnected);
    }

    [Fact]
    public async Task WhenValidTokenExists_ThenIsConnectedIsTrue()
    {
        var options = BuildOptions();
        await using (var seed = new ApplicationDbContext(options))
        {
            seed.TwitchTokens.Add(new TwitchToken
            {
                AccessToken = "tok",
                RefreshToken = "refresh",
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            });
            await seed.SaveChangesAsync();
        }

        var monitor = BuildMonitor(options);
        await monitor.RefreshConnectionState();

        Assert.True(monitor.IsConnected);
    }

    [Fact]
    public async Task WhenExpiredTokenExists_ThenIsConnectedIsFalse()
    {
        var options = BuildOptions();
        await using (var seed = new ApplicationDbContext(options))
        {
            seed.TwitchTokens.Add(new TwitchToken
            {
                AccessToken = "old-tok",
                RefreshToken = "refresh",
                ExpiresAt = DateTime.UtcNow.AddHours(-1)
            });
            await seed.SaveChangesAsync();
        }

        var monitor = BuildMonitor(options);
        await monitor.RefreshConnectionState();

        Assert.False(monitor.IsConnected);
    }

    [Fact]
    public async Task WhenStateChangesFromDisconnectedToConnected_ThenConnectionChangedEventRaised()
    {
        var options = BuildOptions();
        var monitor = BuildMonitor(options);
        await monitor.RefreshConnectionState();

        var eventRaised = false;
        monitor.ConnectionChanged += (_, _) => eventRaised = true;

        await using (var seed = new ApplicationDbContext(options))
        {
            seed.TwitchTokens.Add(new TwitchToken
            {
                AccessToken = "tok",
                RefreshToken = "refresh",
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            });
            await seed.SaveChangesAsync();
        }

        await monitor.RefreshConnectionState();

        Assert.True(eventRaised);
    }

    [Fact]
    public async Task WhenStateDoesNotChange_ThenConnectionChangedEventNotRaised()
    {
        var options = BuildOptions();
        var monitor = BuildMonitor(options);
        await monitor.RefreshConnectionState();

        var eventRaised = false;
        monitor.ConnectionChanged += (_, _) => eventRaised = true;

        await monitor.RefreshConnectionState();

        Assert.False(eventRaised);
    }

    [Fact]
    public void WhenGetAuthorizationUrl_ThenDelegatesToTokenManager()
    {
        var options = BuildOptions();
        var monitor = BuildMonitor(options);

        var url = monitor.GetAuthorizationUrl();

        Assert.Contains("test-client-id", url);
        Assert.StartsWith("https://id.twitch.tv/oauth2/authorize", url);
    }

    private sealed class TestHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new HttpClient();
    }

    private sealed class TestDbContextFactory : IDbContextFactory<ApplicationDbContext>
    {
        private readonly DbContextOptions<ApplicationDbContext> _options;

        public TestDbContextFactory(DbContextOptions<ApplicationDbContext> options)
        {
            _options = options;
        }

        public ApplicationDbContext CreateDbContext() => new ApplicationDbContext(_options);

        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ApplicationDbContext(_options));
    }
}
