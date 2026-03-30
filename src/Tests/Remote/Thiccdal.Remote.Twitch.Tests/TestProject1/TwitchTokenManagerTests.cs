using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using Thiccdal.Data;
using Thiccdal.Data.Models;
using Thiccdal.Infrastructure.Twitch;
using Thiccdal.Remote.Twitch;

namespace Thiccdal.Remote.Twitch.Tests;

public class TwitchTokenManagerTests : IDisposable
{
    private readonly string _dbName = Guid.NewGuid().ToString();
    private readonly AutoMocker _mocker = new AutoMocker();
    private readonly TwitchOptions _options = new TwitchOptions
    {
        ClientId = "test-client-id",
        ClientSecret = "test-client-secret",
        Channel = "testchannel",
        Username = "testbot",
        RedirectUri = "https://localhost/callback"
    };

    private FakeHttpMessageHandler _httpHandler = new FakeHttpMessageHandler(HttpStatusCode.OK, "{}");

    public TwitchTokenManagerTests()
    {
        _mocker.Use(Options.Create(_options));
        _mocker.Use<IDbContextFactory<ApplicationDbContext>>(new InMemoryDbContextFactory(_dbName));
    }

    private TwitchTokenManager BuildManager()
    {
        var httpClientFactory = _mocker.GetMock<IHttpClientFactory>();
        httpClientFactory
            .Setup(f => f.CreateClient("Twitch"))
            .Returns(new HttpClient(_httpHandler));

        return _mocker.CreateInstance<TwitchTokenManager>();
    }

    private ApplicationDbContext CreateSeedContext() =>
        new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(_dbName)
                .Options);

    [Fact]
    public async Task WhenValidTokenExists_ThenGetTokenReturnsStoredAccessToken()
    {
        await using var ctx = CreateSeedContext();
        ctx.TwitchTokens.Add(new TwitchToken
        {
            AccessToken = "valid-token",
            RefreshToken = "refresh-token",
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        });
        await ctx.SaveChangesAsync();

        var result = await BuildManager().GetToken();

        Assert.Equal("valid-token", result);
    }

    [Fact]
    public async Task WhenNoTokenExists_ThenGetTokenThrowsInvalidOperationException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => BuildManager().GetToken());
    }

    [Fact]
    public async Task WhenTokenExpired_ThenGetTokenCallsRefreshAndReturnsNewToken()
    {
        await using var ctx = CreateSeedContext();
        ctx.TwitchTokens.Add(new TwitchToken
        {
            AccessToken = "old-token",
            RefreshToken = "old-refresh",
            ExpiresAt = DateTime.UtcNow.AddHours(-1)
        });
        await ctx.SaveChangesAsync();
        _httpHandler = new FakeHttpMessageHandler(HttpStatusCode.OK, BuildTokenJson("new-token", "new-refresh", 3600));

        var result = await BuildManager().GetToken();

        Assert.Equal("new-token", result);
    }

    [Fact]
    public async Task WhenTokenExpiredAndRefreshFails_ThenGetTokenThrows()
    {
        await using var ctx = CreateSeedContext();
        ctx.TwitchTokens.Add(new TwitchToken
        {
            AccessToken = "old-token",
            RefreshToken = "old-refresh",
            ExpiresAt = DateTime.UtcNow.AddHours(-1)
        });
        await ctx.SaveChangesAsync();
        _httpHandler = new FakeHttpMessageHandler(HttpStatusCode.Unauthorized, "Unauthorized");

        await Assert.ThrowsAsync<InvalidOperationException>(() => BuildManager().GetToken());
    }

    [Fact]
    public async Task WhenStoreToken_ThenTokenIsPersistedToDatabase()
    {
        _httpHandler = new FakeHttpMessageHandler(HttpStatusCode.OK, BuildTokenJson("stored-token", "stored-refresh", 3600));

        await BuildManager().StoreToken("auth-code");

        await using var ctx = CreateSeedContext();
        var token = await ctx.TwitchTokens.FirstOrDefaultAsync();
        Assert.NotNull(token);
        Assert.Equal("stored-token", token.AccessToken);
        Assert.Equal("stored-refresh", token.RefreshToken);
    }

    [Fact]
    public async Task WhenStoreTokenHttpFails_ThenThrowsInvalidOperationException()
    {
        _httpHandler = new FakeHttpMessageHandler(HttpStatusCode.BadRequest, "bad request");

        await Assert.ThrowsAsync<InvalidOperationException>(() => BuildManager().StoreToken("bad-code"));
    }

    [Fact]
    public void WhenGetAuthorizationUrl_ThenUrlContainsClientId()
    {
        var url = BuildManager().GetAuthorizationUrl();

        Assert.Contains("test-client-id", url);
    }

    [Fact]
    public void WhenGetAuthorizationUrl_ThenUrlContainsEncodedRedirectUri()
    {
        var url = BuildManager().GetAuthorizationUrl();

        Assert.Contains(Uri.EscapeDataString("https://localhost/callback"), url);
    }

    [Fact]
    public async Task WhenRefreshToken_ThenNoExceptionWhenNoTokenExists()
    {
        var exception = await Record.ExceptionAsync(() => BuildManager().RefreshToken());

        Assert.Null(exception);
    }

    private static string BuildTokenJson(string accessToken, string refreshToken, int expiresIn) =>
        JsonSerializer.Serialize(new
        {
            access_token = accessToken,
            refresh_token = refreshToken,
            expires_in = expiresIn,
            token_type = "bearer"
        });

    public void Dispose() { }
}

internal sealed class FakeHttpMessageHandler(HttpStatusCode statusCode, string content) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
        });
}

internal sealed class InMemoryDbContextFactory(string dbName) : IDbContextFactory<ApplicationDbContext>
{
    private readonly DbContextOptions<ApplicationDbContext> _options =
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

    public ApplicationDbContext CreateDbContext() => new ApplicationDbContext(_options);

    public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(CreateDbContext());
}
