using Microsoft.Extensions.Logging.Abstractions;
using Thiccdal.API.Status;
using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Infrastructure.Operators;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Infrastructure.Twitch;
using Thiccdal.Infrastructure.YouTube;

namespace Thiccdal.Tests;

public sealed class StreamStatusServiceTests
{
    [Fact]
    public async Task WhenOperatorStateIsLive_ThenStatusUsesStoredStreamMetadata()
    {
        using OperatorStateService operatorStateService = new();
        operatorStateService.SetActiveStreamState(
            new OperatorStreamState
            {
                Title = "Building Thiccdal Live!",
                Category = "Science & Technology",
                Tags = ["dotnet", "blazor"],
                StartedAt = DateTimeOffset.UtcNow.Subtract(TimeSpan.FromMinutes(90))
            });

        FakeTwitchService twitchService = new();
        FakeYouTubeService youTubeService = new();
        StreamStatusService service = CreateService(operatorStateService, twitchService, youTubeService);

        StreamStatusResponse response = await service.GetStatus();

        Assert.Equal(StreamStatusStates.Online, response.State);
        Assert.NotNull(response.Stream);
        Assert.Equal("Building Thiccdal Live!", response.Stream.Title);
        Assert.Equal("Science & Technology", response.Stream.Category);
        Assert.Equal(["dotnet", "blazor"], response.Stream.Tags);
        Assert.Matches(@"^\d{2}:\d{2}:\d{2}$", response.Stream.Uptime);
        Assert.Equal(OperatorMode.Live, operatorStateService.Mode);
    }

    [Fact]
    public async Task WhenOperatorStateIsOfflineAndTwitchIsLive_ThenStatusFallsBackToTwitchMetadata()
    {
        using OperatorStateService operatorStateService = new();
        FakeTwitchService twitchService = new()
        {
            ConnectionStateValue = TwitchConnectionState.Connected,
            StateValue = PlatformConnectionState.Connected,
            StreamStateValue = new TwitchStreamState
            {
                IsLive = true,
                Title = "Fallback Twitch Stream",
                Category = "Coding",
                Tags = ["csharp"],
                StartedAt = DateTimeOffset.UtcNow.Subtract(TimeSpan.FromMinutes(30))
            }
        };

        StreamStatusService service = CreateService(operatorStateService, twitchService, new FakeYouTubeService());

        StreamStatusResponse response = await service.GetStatus();

        Assert.Equal(StreamStatusStates.Online, response.State);
        Assert.NotNull(response.Stream);
        Assert.Equal("Fallback Twitch Stream", response.Stream.Title);
        Assert.Equal("Fallback Twitch Stream", operatorStateService.GetActiveStreamState()?.Title);
        Assert.Equal(OperatorMode.Live, operatorStateService.Mode);
    }

    [Fact]
    public async Task WhenPlatformStateIsError_ThenPlatformStatusIncludesLastError()
    {
        using OperatorStateService operatorStateService = new();
        FakeTwitchService twitchService = new()
        {
            ConnectionStateValue = TwitchConnectionState.Error,
            StateValue = PlatformConnectionState.Error,
            LastErrorValue = "Auth token expired"
        };

        StreamStatusService service = CreateService(operatorStateService, twitchService, new FakeYouTubeService());

        StreamStatusResponse response = await service.GetStatus();

        PlatformStatusDto platform = Assert.Single(response.Platforms, static platform => platform.Name == "Twitch");
        Assert.Equal("Error", platform.State);
        Assert.Equal("Auth token expired", platform.Error);
    }

    [Fact]
    public async Task WhenNoLivePlatformMetadataExists_ThenStatusResetsModeToPreLive()
    {
        using OperatorStateService operatorStateService = new();
        operatorStateService.SetMode(OperatorMode.Live);
        StreamStatusService service = CreateService(operatorStateService, new FakeTwitchService(), new FakeYouTubeService());

        StreamStatusResponse response = await service.GetStatus();

        Assert.Equal(StreamStatusStates.Offline, response.State);
        Assert.Equal(OperatorMode.PreLive, operatorStateService.Mode);
        Assert.Null(operatorStateService.GetActiveStreamState());
    }

    private static StreamStatusService CreateService(
        OperatorStateService operatorStateService,
        FakeTwitchService twitchService,
        FakeYouTubeService youTubeService)
    {
        return new StreamStatusService(
            operatorStateService,
            twitchService,
            youTubeService,
            [twitchService, youTubeService],
            NullLogger<StreamStatusService>.Instance);
    }

    private sealed class FakeTwitchService : ITwitchService
    {
        public string PlatformName => "Twitch";

        public TwitchConnectionState ConnectionStateValue { get; set; } = TwitchConnectionState.NotAuthorized;

        public TwitchStreamState StreamStateValue { get; set; } = new();

        public PlatformConnectionState StateValue { get; set; } = PlatformConnectionState.Disconnected;

        public string? LastErrorValue { get; set; }

        public TwitchConnectionState ConnectionState => ConnectionStateValue;

        public PlatformConnectionState State => StateValue;

        public string? LastError => LastErrorValue;

        public bool IsStreamLive => StreamStateValue.IsLive;

        public TwitchStreamState StreamState => StreamStateValue;

        public bool Connected => ConnectionStateValue == TwitchConnectionState.Connected;

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

        public Task RefreshConnectionState(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RefreshStreamState(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Connect(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Disconnect(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SendMessage(string message, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeYouTubeService : IYouTubeService
    {
        public string PlatformName => "YouTube";

        public YouTubeConnectionState ConnectionStateValue { get; set; } = YouTubeConnectionState.NotAuthorized;

        public PlatformConnectionState StateValue { get; set; } = PlatformConnectionState.Disconnected;

        public string? LastErrorValue { get; set; }

        public YouTubeBroadcastInfo? BroadcastValue { get; set; }

        public YouTubeConnectionState ConnectionState => ConnectionStateValue;

        public PlatformConnectionState State => StateValue;

        public string? LastError => LastErrorValue;

        public bool IsStreamLive => BroadcastValue?.IsLive == true;

        public YouTubeBroadcastInfo? ActiveBroadcast => BroadcastValue;

        public bool Connected => ConnectionStateValue == YouTubeConnectionState.Connected;

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

        public Task RefreshConnectionState(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RefreshStreamState(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Connect(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Disconnect(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SendMessage(string message, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SetTitle(string title, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SetDescription(string description, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SetCategory(string category, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
