using System.Net.Http;
using Microsoft.Extensions.Logging;
using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Infrastructure.X;

namespace Thiccdal.Remote.X.Tests;

public class XServiceTests
{
    [Fact]
    public async Task WhenBearerTokenIsConfigured_ThenConnectionStateIsAuthorized()
    {
        XService service = XTestSupport.CreateService();

        await service.RefreshConnectionState();

        Assert.Equal(XConnectionState.Authorized, service.ConnectionState);
    }

    [Fact]
    public async Task WhenServiceConnects_ThenConnectionStateBecomesConnected()
    {
        XService service = XTestSupport.CreateService();

        await service.Connect();

        Assert.True(service.Connected);
        Assert.Equal(XConnectionState.Connected, service.ConnectionState);
    }

    [Fact]
    public async Task WhenXTweetReply_ThenChatMessageWithCorrectContent()
    {
        FakeXApiClient apiClient = new();
        apiClient.ReplyResults.Enqueue(new XReplyPollResult
        {
            Replies =
            [
                new XTweetReply
                {
                    Id = "1234567890",
                    AuthorId = "987654321",
                    Text = "Hello stream!",
                    CreatedAt = DateTimeOffset.Parse("2024-06-01T14:05:00Z"),
                    Author = new XUserProfile
                    {
                        Id = "987654321",
                        Name = "River",
                        Username = "river_handle"
                    }
                }
            ],
            NewestReplyId = "1234567890"
        });

        FakeEventBus eventBus = new();
        XService service = XTestSupport.CreateService(apiClient: apiClient, eventBus: eventBus);
        ChatEvent? capturedEvent = null;
        service.OnChatMessageRecieved += (_, chatEvent) => capturedEvent = chatEvent;

        await service.Connect();
        await service.PollReplies();

        Assert.NotNull(capturedEvent);
        Assert.Equal("Hello stream!", capturedEvent.Content);
        Assert.Equal("River", capturedEvent.Author);
        Assert.Equal("1234567890", capturedEvent.ExternalId);
        Assert.Equal(DateTime.Parse("2024-06-01T14:05:00Z").ToUniversalTime(), capturedEvent.OccurredAt);
        Assert.Contains("\"user_id\":\"987654321\"", capturedEvent.RawData, StringComparison.Ordinal);
        Assert.Single(eventBus.PublishedEvents);
    }

    [Fact]
    public async Task WhenXTweetReply_ThenDisplayNamePreferredOverUsername()
    {
        FakeXApiClient apiClient = new();
        apiClient.ReplyResults.Enqueue(new XReplyPollResult
        {
            Replies =
            [
                new XTweetReply
                {
                    Id = "1",
                    AuthorId = "2",
                    Text = "Hi",
                    CreatedAt = DateTimeOffset.Parse("2024-06-01T14:05:00Z"),
                    Author = new XUserProfile
                    {
                        Id = "2",
                        Name = "Display Name",
                        Username = "handle_name"
                    }
                }
            ],
            NewestReplyId = "1"
        });

        XService service = XTestSupport.CreateService(apiClient: apiClient);
        ChatEvent? capturedEvent = null;
        service.OnChatMessageRecieved += (_, chatEvent) => capturedEvent = chatEvent;

        await service.Connect();
        await service.PollReplies();

        Assert.NotNull(capturedEvent);
        Assert.Equal("Display Name", capturedEvent.Author);
    }

    [Fact]
    public async Task WhenNewLikingUser_ThenXLikeEventEmitted()
    {
        FakeXApiClient apiClient = new();
        FakeTimeProvider timeProvider = new(DateTimeOffset.Parse("2025-01-01T00:00:00Z"));
        apiClient.LikingUserResults.Enqueue(new XEngagementPollResult
        {
            Users =
            [
                new XUserProfile
                {
                    Id = "existing-like",
                    Name = "Existing Like",
                    Username = "existing_like"
                }
            ]
        });
        apiClient.LikingUserResults.Enqueue(new XEngagementPollResult
        {
            Users =
            [
                new XUserProfile
                {
                    Id = "existing-like",
                    Name = "Existing Like",
                    Username = "existing_like"
                },
                new XUserProfile
                {
                    Id = "new-like",
                    Name = "New Like",
                    Username = "new_like"
                }
            ]
        });
        apiClient.RepostedUserResults.Enqueue(new XEngagementPollResult());
        apiClient.RepostedUserResults.Enqueue(new XEngagementPollResult());

        FakeEventBus eventBus = new();
        XService service = XTestSupport.CreateService(apiClient: apiClient, timeProvider: timeProvider, eventBus: eventBus);

        await service.Connect();
        await service.PollEngagements();
        timeProvider.Advance(TimeSpan.FromMilliseconds(30001));
        await service.PollEngagements();

        PlatformEvent likeEvent = Assert.Single(eventBus.PublishedEvents);
        Assert.Equal(PlatformEventSource.X, likeEvent.Source);
        Assert.Equal(PlatformEventType.Raw, likeEvent.Type);
        Assert.Contains("XLikeEvent", likeEvent.RawData, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WhenAlreadySeenLikingUser_ThenNoEventEmitted()
    {
        FakeXApiClient apiClient = new();
        FakeTimeProvider timeProvider = new(DateTimeOffset.Parse("2025-01-01T00:00:00Z"));
        apiClient.LikingUserResults.Enqueue(new XEngagementPollResult
        {
            Users =
            [
                new XUserProfile
                {
                    Id = "same-like",
                    Name = "Same Like",
                    Username = "same_like"
                }
            ]
        });
        apiClient.LikingUserResults.Enqueue(new XEngagementPollResult
        {
            Users =
            [
                new XUserProfile
                {
                    Id = "same-like",
                    Name = "Same Like",
                    Username = "same_like"
                }
            ]
        });
        apiClient.RepostedUserResults.Enqueue(new XEngagementPollResult());
        apiClient.RepostedUserResults.Enqueue(new XEngagementPollResult());

        FakeEventBus eventBus = new();
        XService service = XTestSupport.CreateService(apiClient: apiClient, timeProvider: timeProvider, eventBus: eventBus);

        await service.Connect();
        await service.PollEngagements();
        timeProvider.Advance(TimeSpan.FromMilliseconds(30001));
        await service.PollEngagements();

        Assert.Empty(eventBus.PublishedEvents);
    }

    [Fact]
    public async Task WhenNewRetweetingUser_ThenXRepostEventEmitted()
    {
        FakeXApiClient apiClient = new();
        FakeTimeProvider timeProvider = new(DateTimeOffset.Parse("2025-01-01T00:00:00Z"));
        apiClient.LikingUserResults.Enqueue(new XEngagementPollResult());
        apiClient.LikingUserResults.Enqueue(new XEngagementPollResult());
        apiClient.RepostedUserResults.Enqueue(new XEngagementPollResult
        {
            Users =
            [
                new XUserProfile
                {
                    Id = "existing-repost",
                    Name = "Existing Repost",
                    Username = "existing_repost"
                }
            ]
        });
        apiClient.RepostedUserResults.Enqueue(new XEngagementPollResult
        {
            Users =
            [
                new XUserProfile
                {
                    Id = "existing-repost",
                    Name = "Existing Repost",
                    Username = "existing_repost"
                },
                new XUserProfile
                {
                    Id = "new-repost",
                    Name = "New Repost",
                    Username = "new_repost"
                }
            ]
        });

        FakeEventBus eventBus = new();
        XService service = XTestSupport.CreateService(apiClient: apiClient, timeProvider: timeProvider, eventBus: eventBus);

        await service.Connect();
        await service.PollEngagements();
        timeProvider.Advance(TimeSpan.FromMilliseconds(30001));
        await service.PollEngagements();

        PlatformEvent repostEvent = Assert.Single(eventBus.PublishedEvents);
        Assert.Contains("XRepostEvent", repostEvent.RawData, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WhenRateLimitRemainingIsZero_ThenPollingBacksOffToResetTime()
    {
        FakeXApiClient apiClient = new();
        FakeTimeProvider timeProvider = new(DateTimeOffset.Parse("2025-01-01T00:00:00Z"));
        DateTimeOffset resetAt = timeProvider.GetUtcNow().AddMinutes(1);
        apiClient.ReplyResults.Enqueue(new XReplyPollResult
        {
            RateLimit = new XApiRateLimit
            {
                Remaining = 0,
                ResetAt = resetAt
            }
        });

        XService service = XTestSupport.CreateService(apiClient: apiClient, timeProvider: timeProvider);

        await service.Connect();
        await service.PollReplies();

        Assert.Equal(resetAt, service.NextReplyPollAt);
    }

    [Fact]
    public async Task WhenRateLimitRemainingIsZero_ThenWarningIsLogged()
    {
        FakeXApiClient apiClient = new();
        RecordingLogger<XService> logger = new();
        FakeTimeProvider timeProvider = new(DateTimeOffset.Parse("2025-01-01T00:00:00Z"));
        apiClient.ReplyResults.Enqueue(new XReplyPollResult
        {
            RateLimit = new XApiRateLimit
            {
                Remaining = 0,
                ResetAt = timeProvider.GetUtcNow().AddMinutes(1)
            }
        });

        XService service = XTestSupport.CreateService(apiClient: apiClient, logger: logger, timeProvider: timeProvider);

        await service.Connect();
        await service.PollReplies();

        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("rate-limited", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task WhenRateLimitResetPasses_ThenPollingResumes()
    {
        FakeXApiClient apiClient = new();
        FakeTimeProvider timeProvider = new(DateTimeOffset.Parse("2025-01-01T00:00:00Z"));
        DateTimeOffset resetAt = timeProvider.GetUtcNow().AddMinutes(1);
        apiClient.ReplyResults.Enqueue(new XReplyPollResult
        {
            RateLimit = new XApiRateLimit
            {
                Remaining = 0,
                ResetAt = resetAt
            }
        });
        apiClient.ReplyResults.Enqueue(new XReplyPollResult());

        XService service = XTestSupport.CreateService(apiClient: apiClient, timeProvider: timeProvider);

        await service.Connect();
        await service.PollReplies();
        timeProvider.Advance(TimeSpan.FromSeconds(30));
        await service.PollReplies();
        timeProvider.Advance(TimeSpan.FromSeconds(31));
        await service.PollReplies();

        Assert.Equal(2, apiClient.PollRepliesCallCount);
    }

    [Fact]
    public async Task WhenBroadcastTweetIdIsMissing_ThenSendMessageLogsWarningAndSkipsApiCall()
    {
        FakeXApiClient apiClient = new();
        RecordingLogger<XService> logger = new();
        XService service = XTestSupport.CreateService(
            options: new XOptions
            {
                BearerToken = "test-bearer-token"
            },
            apiClient: apiClient,
            logger: logger);

        await service.Connect();
        await service.SendMessage("Hello X");

        Assert.Empty(apiClient.SentReplies);
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("BroadcastTweetId", StringComparison.Ordinal));
    }

    [Fact]
    public async Task WhenSendReplyFails_ThenPlatformOperationExceptionIsThrown()
    {
        FakeXApiClient apiClient = new()
        {
            SendReplyException = new HttpRequestException("boom")
        };

        XService service = XTestSupport.CreateService(apiClient: apiClient);

        await service.Connect();

        await Assert.ThrowsAsync<PlatformOperationException>(() => service.SendMessage("Hello X"));
    }
}
