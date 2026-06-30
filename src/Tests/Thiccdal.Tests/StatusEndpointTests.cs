using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Thiccdal.API.Status;
using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Infrastructure.Operators;
using Thiccdal.Infrastructure.Questions;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Infrastructure.Twitch;
using Thiccdal.Infrastructure.YouTube;
using Thiccdal.Remote.Null;

namespace Thiccdal.Tests;

public sealed class StatusEndpointTests : IClassFixture<ThiccdalApplicationFactory>
{
    private readonly ThiccdalApplicationFactory _applicationFactory;

    public StatusEndpointTests(ThiccdalApplicationFactory applicationFactory)
    {
        _applicationFactory = applicationFactory;
    }

    [Fact]
    public async Task WhenStatusEndpointCalledWhileOffline_ThenJsonShapeIsOffline()
    {
        using HttpClient client = CreateClient(
            null,
            CreateTestPlatformConnection("Null", connected: false),
            new TestPlatformConnection("LinkedIn", PlatformConnectionState.PendingApproval),
            new TestPlatformConnection("TikTok", PlatformConnectionState.PendingApproval));

        using HttpRequestMessage request = new(HttpMethod.Get, "/status");
        request.Headers.Add("Origin", "https://example.com");

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("*", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        StreamStatusResponse? payload = await response.Content.ReadFromJsonAsync<StreamStatusResponse>();

        Assert.NotNull(payload);
        Assert.Equal(StreamStatusStates.Offline, payload.State);
        Assert.Null(payload.Stream);
        Assert.Contains(payload.Platforms, static platform => platform is { Name: "Null", State: "Disconnected" });
        Assert.Contains(payload.Platforms, static platform => platform is { Name: "LinkedIn", State: "PendingApproval", Error: null });
        Assert.Contains(payload.Platforms, static platform => platform is { Name: "TikTok", State: "PendingApproval", Error: null });
    }

    [Fact]
    public async Task WhenStatusEndpointCalledWhileOnline_ThenJsonShapeIsOnline()
    {
        OperatorStreamState activeStream = new()
        {
            Title = "Building Thiccdal Live!",
            Category = "Science & Technology",
            Tags = ["csharp", "dotnet", "blazor"],
            StartedAt = DateTimeOffset.UtcNow.Subtract(TimeSpan.FromHours(1)).Subtract(TimeSpan.FromMinutes(23)).Subtract(TimeSpan.FromSeconds(45))
        };

        using HttpClient client = CreateClient(
            activeStream,
            CreateTestPlatformConnection("Null", connected: true),
            new TestPlatformConnection("LinkedIn", PlatformConnectionState.PendingApproval));

        HttpResponseMessage response = await client.GetAsync("/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        StreamStatusResponse? payload = await response.Content.ReadFromJsonAsync<StreamStatusResponse>();

        Assert.NotNull(payload);
        Assert.Equal(StreamStatusStates.Online, payload.State);
        Assert.NotNull(payload.Stream);
        Assert.Equal("Building Thiccdal Live!", payload.Stream.Title);
        Assert.Equal("Science & Technology", payload.Stream.Category);
        Assert.Equal(["csharp", "dotnet", "blazor"], payload.Stream.Tags);
        Assert.Matches(@"^\d{2}:\d{2}:\d{2}$", payload.Stream.Uptime);
        Assert.Contains(payload.Platforms, static platform => platform is { Name: "Null", State: "Connected" });
    }

    [Fact]
    public async Task WhenPlatformIsInErrorState_ThenStatusIncludesErrorDetailsAndExcludesDisabledPlatforms()
    {
        using HttpClient client = CreateClient(
            null,
            CreateTestPlatformConnection("Null", connected: false),
            new TestPlatformConnection("X", PlatformConnectionState.Error, "Auth token expired"),
            new TestPlatformConnection("Discord", PlatformConnectionState.Disabled));

        HttpResponseMessage response = await client.GetAsync("/status");

        StreamStatusResponse? payload = await response.Content.ReadFromJsonAsync<StreamStatusResponse>();

        Assert.NotNull(payload);
        PlatformStatusDto errorPlatform = Assert.Single(payload.Platforms, static platform => platform.Name == "X");
        Assert.Equal("Error", errorPlatform.State);
        Assert.Equal("Auth token expired", errorPlatform.Error);
        Assert.DoesNotContain(payload.Platforms, static platform => platform.Name == "Discord");
    }

    [Fact]
    public async Task WhenStatusEndpointCalled_ThenJsonUsesCamelCase()
    {
        OperatorStreamState activeStream = new()
        {
            Title = "Camel Case Check",
            Category = "Testing",
            Tags = ["json"],
            StartedAt = DateTimeOffset.UtcNow.Subtract(TimeSpan.FromMinutes(5))
        };

        using HttpClient client = CreateClient(activeStream, CreateTestPlatformConnection("Null", connected: true));

        string json = await client.GetStringAsync("/status");

        Assert.Contains("\"state\"", json, StringComparison.Ordinal);
        Assert.Contains("\"stream\"", json, StringComparison.Ordinal);
        Assert.Contains("\"platforms\"", json, StringComparison.Ordinal);
        Assert.Contains("\"startedAt\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"State\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WhenBadgeEndpointCalledWhileOffline_ThenOfflineSvgAndHeadersAreReturned()
    {
        using HttpClient client = CreateClient(null, CreateTestPlatformConnection("Null", connected: false));

        using HttpRequestMessage request = new(HttpMethod.Get, "/status/badge.svg");
        request.Headers.Add("Origin", "https://example.com");

        HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("*", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Equal("image/svg+xml", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString(), StringComparison.OrdinalIgnoreCase);

        string svg = await response.Content.ReadAsStringAsync();
        Assert.Contains("offline", svg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WhenBadgeEndpointCalledWhileOnline_ThenOnlineSvgAndHeadersAreReturned()
    {
        OperatorStreamState activeStream = new()
        {
            Title = "Badge Test",
            Category = "Testing",
            Tags = [],
            StartedAt = DateTimeOffset.UtcNow.Subtract(TimeSpan.FromMinutes(10))
        };

        using HttpClient client = CreateClient(activeStream, CreateTestPlatformConnection("Null", connected: true));

        HttpResponseMessage response = await client.GetAsync("/status/badge.svg");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/svg+xml", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString(), StringComparison.OrdinalIgnoreCase);

        string svg = await response.Content.ReadAsStringAsync();
        Assert.Contains("online", svg, StringComparison.OrdinalIgnoreCase);
    }

    private HttpClient CreateClient(OperatorStreamState? activeStreamState, params IPlatformConnection[] platformConnections)
    {
        WebApplicationFactory<Program> factory = _applicationFactory.WithWebHostBuilder(
            builder =>
            {
                builder.ConfigureServices(
                    services =>
                    {
                        services.RemoveAll<IOperatorStateService>();
                        services.RemoveAll<IPlatformConnection>();
                        services.RemoveAll<ITwitchService>();
                        services.RemoveAll<IYouTubeService>();
                        services.RemoveAll<IHostedService>();

                        OperatorStateService operatorStateService = new();
                        operatorStateService.SetActiveStreamState(activeStreamState);

                        services.AddSingleton<IOperatorStateService>(operatorStateService);
                        services.AddSingleton<ITwitchService>(new TestTwitchService());
                        services.AddSingleton<IYouTubeService>(new TestYouTubeService());

                        foreach (IPlatformConnection platformConnection in platformConnections)
                        {
                            services.AddSingleton(typeof(IPlatformConnection), platformConnection);
                        }
                    });
            });

        return factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });
    }

    private static TestPlatformConnection CreateTestPlatformConnection(string platformName, bool connected)
    {
        return new TestPlatformConnection(
            platformName,
            connected ? PlatformConnectionState.Connected : PlatformConnectionState.Disconnected);
    }

    private sealed class TestPlatformConnection : IPlatformConnection
    {
        public TestPlatformConnection(string platformName, PlatformConnectionState state, string? lastError = null)
        {
            PlatformName = platformName;
            State = state;
            LastError = lastError;
        }

        public string PlatformName { get; }

        public PlatformConnectionState State { get; private set; }

        public string? LastError { get; }

        public bool Connected => State == PlatformConnectionState.Connected;

        public event EventHandler<ChatEvent>? OnChatMessageReceived
        {
            add { }
            remove { }
        }

        public event EventHandler<PlatformEvent>? OnPlatformEventReceived
        {
            add { }
            remove { }
        }

        public Task Connect(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task Disconnect(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task RefreshConnectionState(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SendMessage(string message, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class TestTwitchService : ITwitchService
    {
        public string PlatformName => "Twitch";

        public TwitchConnectionState ConnectionState => TwitchConnectionState.NotAuthorized;

        public PlatformConnectionState State => PlatformConnectionState.Disconnected;

        public string? LastError => null;

        public bool IsStreamLive => false;

        public TwitchStreamState StreamState => new();

        public bool Connected => false;

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

        public event EventHandler<ChatEvent>? OnChatMessageReceived
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

        public Task RefreshConnectionState(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RefreshStreamState(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SendMessage(string message, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SetTitle(string title, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SetDescription(string description, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SetCategory(string category, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class TestYouTubeService : IYouTubeService
    {
        public string PlatformName => "YouTube";

        public YouTubeConnectionState ConnectionState => YouTubeConnectionState.NotAuthorized;

        public PlatformConnectionState State => PlatformConnectionState.Disconnected;

        public string? LastError => null;

        public bool IsStreamLive => false;

        public YouTubeBroadcastInfo? ActiveBroadcast => null;

        public bool Connected => false;

        public event EventHandler<YouTubeConnectionState>? ConnectionStateChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<bool>? StreamLiveStateChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<ChatEvent>? OnChatMessageReceived
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

        public Task RefreshConnectionState(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RefreshStreamState(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SendMessage(string message, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SetTitle(string title, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SetDescription(string description, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SetCategory(string category, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
