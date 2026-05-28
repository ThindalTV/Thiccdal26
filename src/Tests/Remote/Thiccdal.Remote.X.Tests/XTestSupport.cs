using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Infrastructure.X;
using Thiccdal.Remote.X;

namespace Thiccdal.Remote.X.Tests;

internal static class XTestSupport
{
    public static XService CreateService(
        XOptions? options = null,
        FakeXApiClient? apiClient = null,
        RecordingLogger<XService>? logger = null,
        FakeTimeProvider? timeProvider = null,
        FakeEventBus? eventBus = null)
    {
        options ??= new XOptions
        {
            BearerToken = "test-bearer-token",
            BroadcastTweetId = "broadcast-1",
            Channel = "thindal"
        };

        apiClient ??= new FakeXApiClient();
        logger ??= new RecordingLogger<XService>();
        timeProvider ??= new FakeTimeProvider(DateTimeOffset.Parse("2025-01-01T00:00:00Z"));
        eventBus ??= new FakeEventBus();

        return new XService(
            Options.Create(options),
            apiClient,
            eventBus,
            logger,
            timeProvider,
            false);
    }
}

internal sealed class FakeXApiClient : IXApiClient
{
    public Queue<XReplyPollResult> ReplyResults { get; } = new();

    public Queue<XEngagementPollResult> LikingUserResults { get; } = new();

    public Queue<XEngagementPollResult> RepostedUserResults { get; } = new();

    public int PollRepliesCallCount { get; private set; }

    public int GetLikingUsersCallCount { get; private set; }

    public int GetRepostedUsersCallCount { get; private set; }

    public List<(string TweetId, string Message)> SentReplies { get; } = [];

    public Exception? SendReplyException { get; set; }

    public Task<XReplyPollResult> PollReplies(string conversationId, string? sinceId, CancellationToken cancellationToken = default)
    {
        PollRepliesCallCount++;
        return Task.FromResult(ReplyResults.Count > 0 ? ReplyResults.Dequeue() : new XReplyPollResult());
    }

    public Task<XEngagementPollResult> GetLikingUsers(string tweetId, CancellationToken cancellationToken = default)
    {
        GetLikingUsersCallCount++;
        return Task.FromResult(LikingUserResults.Count > 0 ? LikingUserResults.Dequeue() : new XEngagementPollResult());
    }

    public Task<XEngagementPollResult> GetRepostedUsers(string tweetId, CancellationToken cancellationToken = default)
    {
        GetRepostedUsersCallCount++;
        return Task.FromResult(RepostedUserResults.Count > 0 ? RepostedUserResults.Dequeue() : new XEngagementPollResult());
    }

    public Task SendReply(string tweetId, string message, CancellationToken cancellationToken = default)
    {
        if (SendReplyException is not null)
        {
            throw SendReplyException;
        }

        SentReplies.Add((tweetId, message));
        return Task.CompletedTask;
    }
}

internal sealed class FakeEventBus : IEventBus
{
    public List<PlatformEvent> PublishedEvents { get; } = [];

    public Task Publish(PlatformEvent platformEvent, CancellationToken cancellationToken = default)
    {
        PublishedEvents.Add(platformEvent);
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<PlatformEvent> Subscribe([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield break;
    }
}

internal sealed class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow;

    public FakeTimeProvider(DateTimeOffset utcNow)
    {
        _utcNow = utcNow;
    }

    public override DateTimeOffset GetUtcNow()
    {
        return _utcNow;
    }

    public void Advance(TimeSpan duration)
    {
        _utcNow = _utcNow.Add(duration);
    }
}

internal sealed class RecordingLogger<T> : ILogger<T>
{
    public List<LogEntry> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return NullScope.Instance;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
    }

    internal sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
