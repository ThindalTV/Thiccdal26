using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Infrastructure.Facebook;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Remote.Facebook;

namespace Thiccdal.Remote.Facebook.Tests;

public sealed class FacebookServiceTests
{
    [Fact]
    public async Task WhenPageAccessTokenIsConfigured_ThenConnectionStateIsAuthorized()
    {
        FacebookService service = CreateService(options: new FacebookOptions
        {
            PageAccessToken = "test-token",
            PageId = "123456789"
        });

        await service.RefreshConnectionState();

        Assert.Equal(FacebookConnectionState.Authorized, service.ConnectionState);
    }

    [Fact]
    public async Task WhenConnectWithoutActiveLiveVideo_ThenStateBecomesError()
    {
        var graphClient = new FakeFacebookGraphClient();
        var logger = new ListLogger<FacebookService>();
        FacebookService service = CreateService(
            graphClient: graphClient,
            logger: logger,
            options: new FacebookOptions
            {
                PageAccessToken = "test-token",
                PageId = "123456789"
            });

        await service.Connect();

        Assert.Equal(FacebookConnectionState.Error, service.ConnectionState);
        Assert.True(logger.Contains(LogLevel.Warning, "No active Facebook live video was found"));
    }

    [Fact]
    public async Task WhenStartRelayCalled_ThenReturnsSecureRelayUrlStoresLiveVideoIdAndRedactsLog()
    {
        var graphClient = new FakeFacebookGraphClient
        {
            CreatedLiveVideo = new FacebookLiveVideo
            {
                Id = "live-1",
                StreamUrl = "rtmp://facebook/live/plain-key",
                SecureStreamUrl = "rtmps://facebook/live/secure-key"
            }
        };
        var logger = new ListLogger<FacebookService>();
        FacebookService service = CreateService(graphClient: graphClient, logger: logger);

        string relayUrl = await service.StartRelay("My title", "My description");

        Assert.Equal("rtmps://facebook/live/secure-key", relayUrl);
        Assert.Equal("live-1", service.LiveVideoId);
        Assert.Equal("EVERYONE", graphClient.LastCreatePrivacy);
        Assert.True(logger.Contains(LogLevel.Information, "rtmps://facebook/live/"));
        Assert.False(logger.Contains(LogLevel.Information, "secure-key"));
    }

    [Fact]
    public async Task WhenStartRelayFails_ThenPlatformOperationExceptionIsThrown()
    {
        var graphClient = new FakeFacebookGraphClient
        {
            CreateLiveVideoException = new HttpRequestException("boom")
        };
        FacebookService service = CreateService(graphClient: graphClient);

        await Assert.ThrowsAsync<PlatformOperationException>(() => service.StartRelay("title", "description"));
    }

    [Fact]
    public async Task WhenStopRelayCalled_ThenEndLiveVideoIsPostedAndStateIsCleared()
    {
        var graphClient = new FakeFacebookGraphClient
        {
            CreatedLiveVideo = new FacebookLiveVideo
            {
                Id = "live-1",
                SecureStreamUrl = "rtmps://facebook/live/key"
            }
        };
        FacebookService service = CreateService(graphClient: graphClient);

        await service.StartRelay("title", "description");
        await service.StopRelay();

        Assert.Equal("live-1", graphClient.EndedLiveVideoId);
        Assert.Null(service.LiveVideoId);
        Assert.False(service.IsStreamLive);
        Assert.Equal(FacebookConnectionState.Disconnected, service.ConnectionState);
    }

    [Fact]
    public async Task WhenPollingCommentsAndReactions_ThenEventsArePublishedUsersAreUpsertedAndSinceIsTracked()
    {
        var graphClient = new FakeFacebookGraphClient
        {
            ActiveLiveVideo = new FacebookLiveVideo
            {
                Id = "live-1",
                Status = "LIVE"
            }
        };
        graphClient.Comments.Add(FacebookTestData.CreateComment(
            id: "comment-1",
            message: "Hello Facebook!",
            userId: "psid-42",
            displayName: "Viewer One",
            createdTime: "2024-06-01T14:05:00+0000"));
        graphClient.Reactions.Add(FacebookTestData.CreateReaction(
            id: "reaction-1",
            type: "LIKE",
            name: "Viewer One"));
        var eventBus = new FakeEventBus();
        var platformUserService = new FakePlatformUserService();
        var logger = new ListLogger<FacebookService>();
        FacebookService service = CreateService(
            graphClient: graphClient,
            eventBus: eventBus,
            platformUserService: platformUserService,
            logger: logger,
            options: new FacebookOptions
            {
                PageAccessToken = "test-token",
                PageId = "123456789",
                PollIntervalMs = 10
            });

        await service.Connect();
        await graphClient.WaitForCommentPolls(2);
        await eventBus.WaitForCount(2);
        await service.Disconnect();

        ChatEvent chatEvent = Assert.IsType<ChatEvent>(eventBus.Published.Single(static platformEvent => platformEvent is ChatEvent));
        ReactionEvent reactionEvent = Assert.IsType<ReactionEvent>(eventBus.Published.Single(static platformEvent => platformEvent is ReactionEvent));

        Assert.Equal("Hello Facebook!", chatEvent.Content);
        Assert.Equal("Viewer One", chatEvent.Author);
        Assert.Equal("LIKE", reactionEvent.EmoteName);
        Assert.Single(platformUserService.Upserts);
        Assert.Equal("psid-42", platformUserService.Upserts[0].PlatformUserId);
        Assert.Equal("Viewer One", platformUserService.Upserts[0].DisplayName);
        Assert.Null(graphClient.SinceHistory[0]);
        Assert.Equal(DateTimeOffset.Parse("2024-06-01T14:05:00+00:00"), graphClient.SinceHistory[1]);
        Assert.True(logger.Contains(LogLevel.Warning, "Facebook follower events are not emitted"));
    }

    [Fact]
    public async Task WhenSendMessageCalled_ThenCommentIsPostedToActiveLiveVideo()
    {
        var graphClient = new FakeFacebookGraphClient
        {
            CreatedLiveVideo = new FacebookLiveVideo
            {
                Id = "live-1",
                SecureStreamUrl = "rtmps://facebook/live/key"
            }
        };
        FacebookService service = CreateService(graphClient: graphClient);

        await service.StartRelay("title", "description");
        await service.SendMessage("hello world");

        Assert.Equal("live-1", graphClient.LastPostedLiveVideoId);
        Assert.Equal("hello world", graphClient.LastPostedMessage);
    }

    [Fact]
    public async Task WhenSetTitleCalledBeforeLiveVideoExists_ThenInvalidOperationExceptionIsThrown()
    {
        FacebookService service = CreateService();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SetTitle("title"));
    }

    [Fact]
    public async Task WhenSetDescriptionCalled_ThenLiveVideoIsUpdated()
    {
        var graphClient = new FakeFacebookGraphClient
        {
            CreatedLiveVideo = new FacebookLiveVideo
            {
                Id = "live-1",
                SecureStreamUrl = "rtmps://facebook/live/key"
            }
        };
        FacebookService service = CreateService(graphClient: graphClient);

        await service.StartRelay("title", "description");
        await service.SetDescription("Updated description");

        Assert.Single(graphClient.UpdateCalls);
        Assert.Equal("live-1", graphClient.UpdateCalls[0].LiveVideoId);
        Assert.Null(graphClient.UpdateCalls[0].Title);
        Assert.Equal("Updated description", graphClient.UpdateCalls[0].Description);
    }

    [Fact]
    public async Task WhenSetCategoryCalled_ThenOperatorWarningIsLogged()
    {
        var logger = new ListLogger<FacebookService>();
        FacebookService service = CreateService(logger: logger);

        await service.SetCategory("Gaming");

        Assert.True(logger.Contains(LogLevel.Warning, "requested category Gaming was ignored"));
    }

    private static FacebookService CreateService(
        FacebookOptions? options = null,
        FakeFacebookGraphClient? graphClient = null,
        FakeEventBus? eventBus = null,
        FakePlatformUserService? platformUserService = null,
        ListLogger<FacebookService>? logger = null)
    {
        return new FacebookService(
            Options.Create(options ?? new FacebookOptions
            {
                PageAccessToken = "test-token",
                PageId = "123456789"
            }),
            graphClient ?? new FakeFacebookGraphClient(),
            eventBus ?? new FakeEventBus(),
            logger ?? new ListLogger<FacebookService>(),
            platformUserService);
    }

    private sealed class FakeFacebookGraphClient : IFacebookGraphClient
    {
        private readonly TaskCompletionSource<bool> _secondCommentPoll = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public FacebookLiveVideo? ActiveLiveVideo { get; set; }

        public FacebookLiveVideo? CreatedLiveVideo { get; set; }

        public Exception? CreateLiveVideoException { get; set; }

        public List<FacebookComment> Comments { get; } = [];

        public List<FacebookReaction> Reactions { get; } = [];

        public List<DateTimeOffset?> SinceHistory { get; } = [];

        public string LastCreatePrivacy { get; private set; } = string.Empty;

        public string LastPostedLiveVideoId { get; private set; } = string.Empty;

        public string LastPostedMessage { get; private set; } = string.Empty;

        public string EndedLiveVideoId { get; private set; } = string.Empty;

        public List<(string LiveVideoId, string? Title, string? Description)> UpdateCalls { get; } = [];

        public async Task WaitForCommentPolls(int count)
        {
            if (count <= 1 && SinceHistory.Count >= count)
            {
                return;
            }

            await _secondCommentPoll.Task.WaitAsync(TimeSpan.FromSeconds(2));
        }

        public Task<FacebookLiveVideo> CreateLiveVideo(
            string pageId,
            string pageAccessToken,
            string title,
            string description,
            string privacy,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (CreateLiveVideoException is not null)
            {
                throw CreateLiveVideoException;
            }

            LastCreatePrivacy = privacy;
            return Task.FromResult(CreatedLiveVideo ?? new FacebookLiveVideo());
        }

        public Task EndLiveVideo(string liveVideoId, string pageAccessToken, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EndedLiveVideoId = liveVideoId;
            return Task.CompletedTask;
        }

        public Task<FacebookLiveVideo?> GetActiveLiveVideo(
            string pageId,
            string pageAccessToken,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(ActiveLiveVideo);
        }

        public Task<IReadOnlyList<FacebookComment>> GetComments(
            string liveVideoId,
            string pageAccessToken,
            DateTimeOffset? since,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SinceHistory.Add(since);

            if (SinceHistory.Count >= 2)
            {
                _secondCommentPoll.TrySetResult(true);
            }

            return Task.FromResult<IReadOnlyList<FacebookComment>>(Comments);
        }

        public Task PostComment(
            string liveVideoId,
            string pageAccessToken,
            string message,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastPostedLiveVideoId = liveVideoId;
            LastPostedMessage = message;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<FacebookReaction>> GetReactions(
            string liveVideoId,
            string pageAccessToken,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<FacebookReaction>>(Reactions);
        }

        public Task UpdateLiveVideo(
            string liveVideoId,
            string pageAccessToken,
            string? title,
            string? description,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            UpdateCalls.Add((liveVideoId, title, description));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeEventBus : IEventBus
    {
        private readonly TaskCompletionSource<bool> _expectedCountReached = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<PlatformEvent> Published { get; } = [];

        public Task Publish(PlatformEvent platformEvent, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Published.Add(platformEvent);

            if (Published.Count >= 2)
            {
                _expectedCountReached.TrySetResult(true);
            }

            return Task.CompletedTask;
        }

        public async Task WaitForCount(int count)
        {
            if (Published.Count >= count)
            {
                return;
            }

            await _expectedCountReached.Task.WaitAsync(TimeSpan.FromSeconds(2));
        }

        public async IAsyncEnumerable<PlatformEvent> Subscribe([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class FakePlatformUserService : IPlatformUserService
    {
        public List<(PlatformEventSource Source, string PlatformUserId, string DisplayName, DateTime LastSeen)> Upserts { get; } = [];

        public Task<long> Upsert(
            PlatformEventSource source,
            string platformUserId,
            string displayName,
            DateTime lastSeen,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Upserts.Add((source, platformUserId, displayName, lastSeen));
            return Task.FromResult<long>(Upserts.Count);
        }
    }
}
