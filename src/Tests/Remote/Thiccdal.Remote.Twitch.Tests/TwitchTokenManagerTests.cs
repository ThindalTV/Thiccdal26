using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Thiccdal.Data;
using Thiccdal.Data.Models;
using Thiccdal.Infrastructure.Twitch;
using Thiccdal.Remote.Twitch;

namespace Thiccdal.Remote.Twitch.Tests;

public class TwitchTokenManagerTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static TwitchTokenManager CreateManager(string dbName, TwitchOptions? options = null)
    {
        var factory = new TestDbContextFactory(dbName);
        var opts = Options.Create(options ?? new TwitchOptions
        {
            ClientId = "test-client-id",
            ClientSecret = "test-client-secret",
            RedirectUri = "https://localhost/auth/twitch/callback"
        });

        return new TwitchTokenManager(opts, NullLogger<TwitchTokenManager>.Instance, new StubHttpClientFactory(), factory);
    }

    private static async Task SeedToken(string dbName)
    {
        var factory = new TestDbContextFactory(dbName);
        await using var context = factory.CreateDbContext();
        context.TwitchTokens.Add(new TwitchToken
        {
            AccessToken = "access-abc",
            RefreshToken = "refresh-xyz",
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        });
        await context.SaveChangesAsync();
    }

    // ── HasToken ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task WhenNoTokenExists_HasTokenReturnsFalse()
    {
        var manager = CreateManager(nameof(WhenNoTokenExists_HasTokenReturnsFalse));

        var result = await manager.HasToken();

        Assert.False(result);
    }

    [Fact]
    public async Task WhenTokenStored_HasTokenReturnsTrue()
    {
        const string db = nameof(WhenTokenStored_HasTokenReturnsTrue);
        await SeedToken(db);
        var manager = CreateManager(db);

        var result = await manager.HasToken();

        Assert.True(result);
    }

    // ── GetToken ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task WhenNoTokenExists_GetTokenReturnsNull()
    {
        var manager = CreateManager(nameof(WhenNoTokenExists_GetTokenReturnsNull));

        var token = await manager.GetToken();

        Assert.Null(token);
    }

    [Fact]
    public async Task WhenValidTokenExists_GetTokenReturnsAccessToken()
    {
        const string db = nameof(WhenValidTokenExists_GetTokenReturnsAccessToken);
        await SeedToken(db);
        var manager = CreateManager(db);

        var token = await manager.GetToken();

        Assert.Equal("access-abc", token);
    }

    // ── Revoke ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task WhenTokenRevoked_HasTokenReturnsFalse()
    {
        const string db = nameof(WhenTokenRevoked_HasTokenReturnsFalse);
        await SeedToken(db);
        var manager = CreateManager(db);

        await manager.Revoke();

        Assert.False(await manager.HasToken());
    }

    [Fact]
    public async Task WhenNoTokenExists_RevokeIsIdempotent()
    {
        var manager = CreateManager(nameof(WhenNoTokenExists_RevokeIsIdempotent));

        // Revoke on empty DB should not throw
        await manager.Revoke();

        Assert.False(await manager.HasToken());
    }

    // ── GetAuthorizationUrl ───────────────────────────────────────────────────

    [Fact]
    public void GetAuthorizationUrl_ContainsClientId()
    {
        var manager = CreateManager(nameof(GetAuthorizationUrl_ContainsClientId));

        var url = manager.GetAuthorizationUrl();

        Assert.Contains("test-client-id", url);
    }

    [Fact]
    public void GetAuthorizationUrl_ContainsRequiredScopes()
    {
        var manager = CreateManager(nameof(GetAuthorizationUrl_ContainsRequiredScopes));

        var url = manager.GetAuthorizationUrl();

        Assert.Contains("user%3Aread%3Achat", url);              // user:read:chat
        Assert.Contains("user%3Awrite%3Achat", url);             // user:write:chat
        Assert.Contains("moderator%3Aread%3Afollowers", url);    // moderator:read:followers
    }

    [Fact]
    public void GetAuthorizationUrl_PointsToTwitchOAuth()
    {
        var manager = CreateManager(nameof(GetAuthorizationUrl_PointsToTwitchOAuth));

        var url = manager.GetAuthorizationUrl();

        Assert.StartsWith("https://id.twitch.tv/oauth2/authorize", url);
    }

    [Fact]
    public void GetAuthorizationUrl_ContainsStateParameter()
    {
        var manager = CreateManager(nameof(GetAuthorizationUrl_ContainsStateParameter));

        var url = manager.GetAuthorizationUrl();

        Assert.Contains("&state=", url);
    }

    [Fact]
    public void GetAuthorizationUrl_EachCallProducesUniqueState()
    {
        var manager = CreateManager(nameof(GetAuthorizationUrl_EachCallProducesUniqueState));

        var url1 = manager.GetAuthorizationUrl();
        var url2 = manager.GetAuthorizationUrl();

        var state1 = ExtractQueryParam(url1, "state");
        var state2 = ExtractQueryParam(url2, "state");

        Assert.NotEqual(state1, state2);
    }

    // ── ValidateAndConsumeState ────────────────────────────────────────────────

    [Fact]
    public void WhenStateWasIssued_ValidateAndConsumeStateReturnsTrue()
    {
        var manager = CreateManager(nameof(WhenStateWasIssued_ValidateAndConsumeStateReturnsTrue));

        var url = manager.GetAuthorizationUrl();
        var state = ExtractQueryParam(url, "state");

        Assert.True(manager.ValidateAndConsumeState(state));
    }

    [Fact]
    public void WhenStateNeverIssued_ValidateAndConsumeStateReturnsFalse()
    {
        var manager = CreateManager(nameof(WhenStateNeverIssued_ValidateAndConsumeStateReturnsFalse));

        Assert.False(manager.ValidateAndConsumeState("not-a-real-state"));
    }

    [Fact]
    public void WhenStateConsumedTwice_SecondCallReturnsFalse()
    {
        var manager = CreateManager(nameof(WhenStateConsumedTwice_SecondCallReturnsFalse));

        var url = manager.GetAuthorizationUrl();
        var state = ExtractQueryParam(url, "state");

        Assert.True(manager.ValidateAndConsumeState(state));
        Assert.False(manager.ValidateAndConsumeState(state));
    }

    // ── StoreToken upsert ─────────────────────────────────────────────────────

    [Fact]
    public async Task WhenTokenAlreadyExists_StoreTokenReplacesIt()
    {
        const string db = nameof(WhenTokenAlreadyExists_StoreTokenReplacesIt);
        await SeedToken(db);

        var factory = new TestDbContextFactory(db);
        var opts = Options.Create(new TwitchOptions
        {
            ClientId = "test-client-id",
            ClientSecret = "test-client-secret",
            RedirectUri = "https://localhost/auth/twitch/callback"
        });

        var fakeResponse = """{"access_token":"new-access","refresh_token":"new-refresh","expires_in":14400,"token_type":"bearer"}""";
        var manager = new TwitchTokenManager(opts, NullLogger<TwitchTokenManager>.Instance, new StubHttpClientFactory(fakeResponse), factory);

        await manager.StoreToken("any-code");

        await using var context = factory.CreateDbContext();
        var tokens = await context.TwitchTokens.ToListAsync();
        Assert.Single(tokens);
        Assert.Equal("new-access", tokens[0].AccessToken);
    }

    // ── Test Infrastructure ──────────────────────────────────────────────────

    private static string ExtractQueryParam(string url, string name)
    {
        var query = new Uri(url).Query.TrimStart('?');
        foreach (var pair in query.Split('&'))
        {
            var idx = pair.IndexOf('=');
            if (idx < 0) continue;
            var key = Uri.UnescapeDataString(pair[..idx]);
            if (key == name)
                return Uri.UnescapeDataString(pair[(idx + 1)..]);
        }
        return string.Empty;
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

        public ApplicationDbContext CreateDbContext() => new ApplicationDbContext(_options);

        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly string? _responseBody;

        public StubHttpClientFactory(string? responseBody = null)
        {
            _responseBody = responseBody;
        }

        public HttpClient CreateClient(string name)
        {
            if (_responseBody == null)
                return new HttpClient();

            var handler = new StubHttpMessageHandler(_responseBody);
            return new HttpClient(handler);
        }
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _body;

        public StubHttpMessageHandler(string body) => _body = body;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(_body, System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}