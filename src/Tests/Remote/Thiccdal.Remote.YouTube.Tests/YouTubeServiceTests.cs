using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Infrastructure.YouTube;
using Thiccdal.Remote.YouTube;

namespace Thiccdal.Remote.YouTube.Tests;

public sealed class YouTubeServiceTests
{
    [Fact]
    public async Task WhenSendingMessageWhileConnected_ThenMessageIsPostedToActiveLiveChat()
    {
        RecordingYouTubeApiClient apiClient = new()
        {
            ActiveBroadcast = new YouTubeBroadcastInfo
            {
                BroadcastId = "broadcast-123",
                LiveChatId = "live-chat-456",
                IsLive = true
            }
        };
        YouTubeService service = CreateService(apiClient);

        await service.Connect();
        await service.SendMessage("Hello chat!");

        Assert.Single(apiClient.SentMessages);
        Assert.Equal(("live-chat-456", "Hello chat!"), apiClient.SentMessages[0]);

        await service.Disconnect();
    }

    [Fact]
    public async Task WhenCachedLiveChatIdIsMissing_ThenServiceRefreshesBroadcastBeforeSending()
    {
        RecordingYouTubeApiClient apiClient = new()
        {
            ActiveBroadcast = new YouTubeBroadcastInfo
            {
                BroadcastId = "broadcast-123",
                LiveChatId = "live-chat-456",
                IsLive = true
            }
        };
        YouTubeService service = CreateService(apiClient);

        await service.Connect();
        apiClient.ActiveBroadcast = new YouTubeBroadcastInfo
        {
            BroadcastId = "broadcast-123",
            LiveChatId = "live-chat-789",
            IsLive = true
        };
        typeof(YouTubeService)
            .GetField("_currentLiveChatId", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(service, null);

        await service.SendMessage("Hello refreshed chat!");

        Assert.Equal(("live-chat-789", "Hello refreshed chat!"), apiClient.SentMessages[^1]);

        await service.Disconnect();
    }

    [Fact]
    public async Task WhenConnectingWithoutActiveBroadcast_ThenStateTransitionsToError()
    {
        RecordingYouTubeApiClient apiClient = new();
        YouTubeService service = CreateService(apiClient);

        await service.Connect();

        Assert.Equal(YouTubeConnectionState.Error, service.ConnectionState);
        Assert.Equal("No active YouTube broadcast with live chat was found.", service.LastError);
    }

    [Fact]
    public async Task WhenPollingFails_ThenStateTransitionsToError()
    {
        TaskCompletionSource<bool> errorObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
        RecordingYouTubeApiClient apiClient = new()
        {
            ActiveBroadcast = new YouTubeBroadcastInfo
            {
                BroadcastId = "broadcast-123",
                LiveChatId = "live-chat-456",
                IsLive = true
            },
            PollException = new HttpRequestException("poll failed")
        };
        YouTubeService service = CreateService(apiClient);
        service.ConnectionStateChanged += (_, state) =>
        {
            if (state == YouTubeConnectionState.Error)
            {
                errorObserved.TrySetResult(true);
            }
        };

        await service.Connect();
        await errorObserved.Task.WaitAsync(TimeSpan.FromSeconds(3));

        Assert.Equal(YouTubeConnectionState.Error, service.ConnectionState);
        Assert.Equal("poll failed", service.LastError);

        await service.Disconnect();
    }

    [Fact]
    public async Task WhenSettingTitle_ThenBroadcastInfoIsUpdated()
    {
        RecordingYouTubeApiClient apiClient = new()
        {
            ActiveBroadcast = new YouTubeBroadcastInfo
            {
                BroadcastId = "broadcast-123",
                LiveChatId = "live-chat-456",
                Title = "Old title",
                Description = "Old description",
                IsLive = true
            }
        };
        YouTubeService service = CreateService(apiClient);

        await service.SetTitle("New title");

        Assert.Equal(("broadcast-123", "New title", "Old description"), Assert.Single(apiClient.BroadcastUpdates));
    }

    [Fact]
    public async Task WhenSettingDescription_ThenBroadcastInfoIsUpdated()
    {
        RecordingYouTubeApiClient apiClient = new()
        {
            ActiveBroadcast = new YouTubeBroadcastInfo
            {
                BroadcastId = "broadcast-123",
                LiveChatId = "live-chat-456",
                Title = "Old title",
                Description = "Old description",
                IsLive = true
            }
        };
        YouTubeService service = CreateService(apiClient);

        await service.SetDescription("New description");

        Assert.Equal(("broadcast-123", "Old title", "New description"), Assert.Single(apiClient.BroadcastUpdates));
    }

    [Fact]
    public async Task WhenSettingTitleFails_ThenPlatformOperationExceptionIsThrown()
    {
        RecordingYouTubeApiClient apiClient = new()
        {
            ActiveBroadcast = new YouTubeBroadcastInfo
            {
                BroadcastId = "broadcast-123",
                LiveChatId = "live-chat-456",
                Title = "Old title",
                Description = "Old description",
                IsLive = true
            },
            UpdateException = new InvalidOperationException("boom")
        };
        YouTubeService service = CreateService(apiClient);

        PlatformOperationException exception = await Assert.ThrowsAsync<PlatformOperationException>(() => service.SetTitle("New title"));

        Assert.Equal("YouTube title update failed.", exception.Message);
    }

    [Fact]
    public async Task WhenSettingCategory_ThenPlatformOperationExceptionIsThrown()
    {
        YouTubeService service = CreateService(new RecordingYouTubeApiClient());

        PlatformOperationException exception = await Assert.ThrowsAsync<PlatformOperationException>(() => service.SetCategory("Gaming"));

        Assert.Equal("YouTube category updates are not supported by the current live broadcast API.", exception.Message);
    }

    [Fact]
    public async Task WhenUpdatingStreamInfo_ThenResultSurfacesUnsupportedFields()
    {
        RecordingYouTubeApiClient apiClient = new()
        {
            ActiveBroadcast = new YouTubeBroadcastInfo
            {
                BroadcastId = "broadcast-123",
                LiveChatId = "live-chat-456",
                Title = "Old title",
                Description = "Old description",
                IsLive = true
            }
        };
        YouTubeService service = CreateService(apiClient);

        StreamInfoUpdateResult result = await service.UpdateStreamInfo(new StreamInfoUpdateRequest
        {
            Title = "New title",
            Category = "Gaming",
            Tags = ["dotnet"]
        });

        Assert.Equal(StreamInfoUpdateStatus.PartiallySucceeded, result.Status);
        Assert.Contains("Updated YouTube title.", result.Message);
        Assert.Contains("Category updates are not supported", result.Message);
        Assert.Contains("Tag updates are not supported", result.Message);
    }

    [Fact]
    public async Task WhenUpdatingStreamInfoWithUnsupportedFieldsOnly_ThenResultIsUnsupported()
    {
        YouTubeService service = CreateService(new RecordingYouTubeApiClient());

        StreamInfoUpdateResult result = await service.UpdateStreamInfo(new StreamInfoUpdateRequest
        {
            Category = "Gaming",
            Tags = ["dotnet"]
        });

        Assert.Equal(StreamInfoUpdateStatus.Unsupported, result.Status);
    }

    private static YouTubeService CreateService(IYouTubeApiClient apiClient)
    {
        return new YouTubeService(
            Options.Create(new YouTubeOptions()),
            new StubTokenManager(),
            apiClient,
            new YouTubeLiveChatMessageMapper(NullLogger<YouTubeLiveChatMessageMapper>.Instance),
            new StubEventBus(),
            NullLogger<YouTubeService>.Instance);
    }

    private sealed class RecordingYouTubeApiClient : IYouTubeApiClient
    {
        public YouTubeBroadcastInfo? ActiveBroadcast { get; set; }

        public List<(string LiveChatId, string Message)> SentMessages { get; } = [];

        public List<(string BroadcastId, string Title, string Description)> BroadcastUpdates { get; } = [];

        public Exception? PollException { get; init; }

        public Exception? UpdateException { get; init; }

        public Task<YouTubeBroadcastInfo?> GetActiveBroadcast(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ActiveBroadcast);
        }

        public Task<YouTubeLiveChatPollResult> PollLiveChat(
            string liveChatId,
            string? pageToken,
            CancellationToken cancellationToken = default)
        {
            if (PollException is not null)
            {
                throw PollException;
            }

            return Task.FromResult(new YouTubeLiveChatPollResult
            {
                NextPageToken = string.Empty,
                PollingIntervalMillis = 60000,
                RawJson = "{}"
            });
        }

        public Task SendLiveChatMessage(string liveChatId, string message, CancellationToken cancellationToken = default)
        {
            SentMessages.Add((liveChatId, message));
            return Task.CompletedTask;
        }

        public Task UpdateBroadcastInfo(string broadcastId, string title, string description, CancellationToken cancellationToken = default)
        {
            if (UpdateException is not null)
            {
                throw UpdateException;
            }

            BroadcastUpdates.Add((broadcastId, title, description));
            return Task.CompletedTask;
        }
    }

    private sealed class StubTokenManager : IYouTubeTokenManager
    {
        public string GetAuthorizationUrl() => string.Empty;

        public bool ValidateAndConsumeState(string state) => true;

        public Task StoreToken(string authorizationCode, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<string?> GetToken(CancellationToken cancellationToken = default) => Task.FromResult<string?>("access-token");

        public Task<bool> HasToken(CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task RevokeToken(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubEventBus : IEventBus
    {
        public Task Publish(PlatformEvent platformEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public async IAsyncEnumerable<PlatformEvent> Subscribe([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
