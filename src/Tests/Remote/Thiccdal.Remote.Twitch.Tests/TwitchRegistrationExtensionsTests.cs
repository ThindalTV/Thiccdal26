using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text;
using System.Runtime.CompilerServices;
using Thiccdal.Data;
using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Infrastructure.Integrations;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Infrastructure.Twitch;
using Thiccdal.Remote.Twitch;

namespace Thiccdal.Remote.Twitch.Tests;

public class TwitchRegistrationExtensionsTests
{
    [Fact]
    public void WhenAddingTwitchIntegration_ThenRegistersSharedTwitchServices()
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(            new Dictionary<string, string?>
            {
                [$"{TwitchOptions.SectionName}:ClientId"] = "client-id",
                [$"{TwitchOptions.SectionName}:ClientSecret"] = "client-secret",
                [$"{TwitchOptions.SectionName}:RedirectUri"] = "https://localhost/auth/twitch/callback",
                [$"{TwitchOptions.SectionName}:OAuthBaseAddress"] = "https://id.twitch.tv/oauth2/",
                [$"{TwitchOptions.SectionName}:Helix:BaseAddress"] = "https://api.twitch.tv/helix/",
                [$"{TwitchOptions.SectionName}:Helix:StreamStateRefreshSeconds"] = "45",
                [$"{TwitchOptions.SectionName}:Helix:SendChatMessagesViaHelix"] = "true",
                [$"{TwitchOptions.SectionName}:EventSub:WebSocketUrl"] = "wss://eventsub.wss.twitch.tv/ws",
                [$"{TwitchOptions.SectionName}:EventSub:ReconnectDelaySeconds"] = "9",
                [$"{TwitchOptions.SectionName}:EventSub:RequireModeratorAccess"] = "true",
                [$"{TwitchOptions.SectionName}:EventSub:UseAnimatedEmotes"] = "true"
            })
            .Build();

        services.AddLogging();
        services.AddDbContextFactory<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddSingleton<IEventPersistenceService, NoOpEventPersistenceService>();
        services.AddSingleton<IEventBus, NoOpEventBus>();
        services.AddTwitchIntegration(configuration);

        using ServiceProvider provider = services.BuildServiceProvider();

        IChatSource chatSource = provider.GetRequiredService<IChatSource>();
        IPlatformConnection platformConnection = provider.GetRequiredService<IPlatformConnection>();
        IStreamTarget streamTarget = provider.GetRequiredService<IStreamTarget>();
        IEventSource eventSource = provider.GetRequiredService<IEventSource>();
        ITwitchService twitchService = provider.GetRequiredService<ITwitchService>();
        ITwitchConnectionMonitor monitor = provider.GetRequiredService<ITwitchConnectionMonitor>();
        IIntegrationConnectionMonitor integrationMonitor = provider.GetRequiredService<IIntegrationConnectionMonitor>();
        IStreamInfoProvider streamInfoProvider = provider.GetRequiredService<IStreamInfoProvider>();
        ITwitchTargetChannelService targetChannelService = provider.GetRequiredService<ITwitchTargetChannelService>();
        ITwitchHelixClient typedHelixClient = provider.GetRequiredService<ITwitchHelixClient>();
        ITwitchEventSubClient eventSubClient = provider.GetRequiredService<ITwitchEventSubClient>();
        TwitchOptions options = provider.GetRequiredService<IOptions<TwitchOptions>>().Value;
        TwitchHelixOptions helixOptions = provider.GetRequiredService<IOptions<TwitchHelixOptions>>().Value;
        TwitchEventSubOptions eventSubOptions = provider.GetRequiredService<IOptions<TwitchEventSubOptions>>().Value;
        IHttpClientFactory httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
        HttpClient oauthClient = httpClientFactory.CreateClient(TwitchClientNames.OAuth);
        HttpClient helixClient = httpClientFactory.CreateClient(TwitchClientNames.Helix);

        Assert.NotNull(provider.GetRequiredService<ITwitchTokenManager>());
        Assert.NotNull(typedHelixClient);
        Assert.NotNull(eventSubClient);
        Assert.NotNull(targetChannelService);
        Assert.Same(twitchService, chatSource);
        Assert.Same(twitchService, platformConnection);
        Assert.Same(twitchService, streamTarget);
        Assert.Same(twitchService, eventSource);
        Assert.Same(twitchService, streamInfoProvider);
        Assert.Same(monitor, integrationMonitor);
        Assert.Equal(45, helixOptions.StreamStateRefreshSeconds);
        Assert.True(helixOptions.SendChatMessagesViaHelix);
        Assert.Equal(9, eventSubOptions.ReconnectDelaySeconds);
        Assert.True(eventSubOptions.RequireModeratorAccess);
        Assert.True(eventSubOptions.UseAnimatedEmotes);
        Assert.Equal(helixOptions.BaseAddress, options.Helix.BaseAddress);
        Assert.Equal(eventSubOptions.WebSocketUrl, options.EventSub.WebSocketUrl);
        Assert.Equal(new Uri("https://id.twitch.tv/oauth2/"), oauthClient.BaseAddress);
        Assert.Equal(new Uri("https://api.twitch.tv/helix/"), helixClient.BaseAddress);
    }

    [Fact]
    public void WhenEventSubConfigurationIsInvalid_ThenOptionsValidationFails()
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{TwitchOptions.SectionName}:EventSub:WebSocketUrl"] = "https://eventsub.twitch.tv/ws"
            })
            .Build();

        services.AddLogging();
        services.AddDbContextFactory<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddSingleton<IEventPersistenceService, NoOpEventPersistenceService>();
        services.AddSingleton<IEventBus, NoOpEventBus>();
        services.AddTwitchIntegration(configuration);

        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<TwitchOptions>>().Value);
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<TwitchEventSubOptions>>().Value);
    }

    [Fact]
    public async Task WhenPlatformClientsSeeTransientFailures_ThenResilienceRetriesOAuthAndHelixCalls()
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{TwitchOptions.SectionName}:ClientId"] = "client-id",
                [$"{TwitchOptions.SectionName}:ClientSecret"] = "client-secret",
                [$"{TwitchOptions.SectionName}:RedirectUri"] = "https://localhost/auth/twitch/callback",
                [$"{TwitchOptions.SectionName}:OAuthBaseAddress"] = "https://id.twitch.tv/oauth2/",
                [$"{TwitchOptions.SectionName}:Helix:BaseAddress"] = "https://api.twitch.tv/helix/",
                [$"{TwitchOptions.SectionName}:Helix:StreamStateRefreshSeconds"] = "45",
                [$"{TwitchOptions.SectionName}:Helix:SendChatMessagesViaHelix"] = "true",
                [$"{TwitchOptions.SectionName}:EventSub:WebSocketUrl"] = "wss://eventsub.wss.twitch.tv/ws",
                [$"{TwitchOptions.SectionName}:EventSub:ReconnectDelaySeconds"] = "9"
            })
            .Build();

        CountingHttpMessageHandler oauthHandler = new();
        CountingHttpMessageHandler helixHandler = new();

        services.AddLogging();
        services.AddDbContextFactory<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddSingleton<IEventPersistenceService, NoOpEventPersistenceService>();
        services.AddSingleton<IEventBus, NoOpEventBus>();
        services.AddTwitchIntegration(configuration);
        services.AddHttpClient(TwitchClientNames.OAuth)
            .ConfigurePrimaryHttpMessageHandler(() => oauthHandler);
        services.AddHttpClient(TwitchClientNames.Helix)
            .ConfigurePrimaryHttpMessageHandler(() => helixHandler);

        using ServiceProvider provider = services.BuildServiceProvider();
        IHttpClientFactory httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();

        using HttpResponseMessage oauthResponse = await httpClientFactory.CreateClient(TwitchClientNames.OAuth).GetAsync("token");
        using HttpResponseMessage helixResponse = await httpClientFactory.CreateClient(TwitchClientNames.Helix).GetAsync("users");

        Assert.Equal(HttpStatusCode.OK, oauthResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, helixResponse.StatusCode);
        Assert.Equal(6, oauthHandler.AttemptCount);
        Assert.Equal(6, helixHandler.AttemptCount);
    }

    [Fact]
    public async Task WhenCallbackSucceeds_ThenStoresTokenRefreshesStateAndRedirects()
    {
        FakeTokenManager tokenManager = new() { ShouldValidateState = true };
        FakeTwitchService twitchService = new();
        FakeTwitchConnectionMonitor connectionMonitor = new();

        await using WebApplication app = await BuildApp(tokenManager, twitchService, connectionMonitor);
        HttpClient client = app.GetTestClient();

        HttpResponseMessage response = await client.GetAsync("/auth/twitch/callback?code=auth-code&state=valid-state");

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/dashboard", response.Headers.Location?.OriginalString);
        Assert.Equal("auth-code", tokenManager.StoredCode);
        Assert.Equal("valid-state", tokenManager.ValidatedState);
        Assert.True(twitchService.RefreshConnectionStateCalled);
        Assert.True(connectionMonitor.RefreshConnectionStateCalled);
    }

    [Fact]
    public async Task WhenCallbackStateIsInvalid_ThenRejectsRequestWithoutPersistingToken()
    {
        FakeTokenManager tokenManager = new() { ShouldValidateState = false };
        FakeTwitchService twitchService = new();
        FakeTwitchConnectionMonitor connectionMonitor = new();

        await using WebApplication app = await BuildApp(tokenManager, twitchService, connectionMonitor);
        HttpClient client = app.GetTestClient();

        HttpResponseMessage response = await client.GetAsync("/auth/twitch/callback?code=auth-code&state=invalid-state");

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/dashboard?twitch_error=invalid_state", response.Headers.Location?.OriginalString);
        Assert.Null(tokenManager.StoredCode);
        Assert.Equal("invalid-state", tokenManager.ValidatedState);
        Assert.False(twitchService.RefreshConnectionStateCalled);
        Assert.False(connectionMonitor.RefreshConnectionStateCalled);
    }

    private static async Task<WebApplication> BuildApp(
        FakeTokenManager tokenManager,
        FakeTwitchService twitchService,
        FakeTwitchConnectionMonitor connectionMonitor)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddLogging();
        builder.Services.AddSingleton<ITwitchTokenManager>(tokenManager);
        builder.Services.AddSingleton<ITwitchService>(twitchService);
        builder.Services.AddSingleton<ITwitchConnectionMonitor>(connectionMonitor);

        WebApplication app = builder.Build();
        app.MapTwitchEndpoints();
        await app.StartAsync();

        return app;
    }

    private sealed class NoOpEventPersistenceService : IEventPersistenceService
    {
        public Task Persist(PlatformEvent platformEvent, CancellationToken cancellationToken = default)
        {
            platformEvent.PersistedRecordId = 1;
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpEventBus : IEventBus
    {
        public Task Publish(PlatformEvent platformEvent, CancellationToken cancellationToken = default)
        {
            platformEvent.PersistedRecordId = 1;
            return Task.CompletedTask;
        }

        public async IAsyncEnumerable<PlatformEvent> Subscribe([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class CountingHttpMessageHandler : HttpMessageHandler
    {
        public int AttemptCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            AttemptCount++;

            HttpStatusCode statusCode = AttemptCount < 6
                ? HttpStatusCode.ServiceUnavailable
                : HttpStatusCode.OK;

            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
                RequestMessage = request
            });
        }
    }

    private sealed class FakeTokenManager : ITwitchTokenManager
    {
        public string? StoredCode { get; private set; }

        public string? ValidatedState { get; private set; }

        public bool ShouldValidateState { get; init; }

        public Task<string?> GetToken(CancellationToken cancellationToken = default) => Task.FromResult<string?>("token");

        public Task<bool> HasToken(CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task RefreshToken(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StoreToken(string code, CancellationToken cancellationToken = default)
        {
            StoredCode = code;
            return Task.CompletedTask;
        }

        public Task Revoke(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public string GetAuthorizationUrl() => "https://id.twitch.tv/oauth2/authorize";

        public bool ValidateAndConsumeState(string state)
        {
            ValidatedState = state;
            return ShouldValidateState;
        }
    }

    private sealed class FakeTwitchService : ITwitchService
    {
        public string PlatformName => "Twitch";

        public TwitchConnectionState ConnectionState => TwitchConnectionState.Authorized;

        public PlatformConnectionState State => PlatformConnectionState.Disconnected;

        public string? LastError => null;

        public bool IsStreamLive => false;

        public TwitchStreamState StreamState => new();

        public bool Connected => false;

        public bool RefreshConnectionStateCalled { get; private set; }

        public event EventHandler<TwitchConnectionState>? ConnectionStateChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<bool>? StreamLiveStateChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<ChatEvent>? OnChatMessageRecieved
        {
            add { }
            remove { }
        }

        public event EventHandler<PlatformEvent>? OnPlatformEventReceived
        {
            add { }
            remove { }
        }

        public Task Connect(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Disconnect(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RefreshConnectionState(CancellationToken cancellationToken = default)
        {
            RefreshConnectionStateCalled = true;
            return Task.CompletedTask;
        }

        public Task RefreshStreamState(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SendMessage(string message, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeTwitchConnectionMonitor : ITwitchConnectionMonitor
    {
        public string PlatformName => "Twitch";

        public bool IsConnected => false;

        public bool RefreshConnectionStateCalled { get; private set; }

        public event EventHandler? ConnectionChanged
        {
            add { }
            remove { }
        }

        public string GetAuthorizationUrl() => "https://id.twitch.tv/oauth2/authorize";

        public Task RefreshConnectionState(CancellationToken cancellationToken = default)
        {
            RefreshConnectionStateCalled = true;
            return Task.CompletedTask;
        }
    }
}
