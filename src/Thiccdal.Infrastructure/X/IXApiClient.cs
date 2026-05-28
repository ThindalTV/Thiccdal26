namespace Thiccdal.Infrastructure.X;

public interface IXApiClient
{
    Task<XReplyPollResult> PollReplies(string conversationId, string? sinceId, CancellationToken cancellationToken = default);

    Task<XEngagementPollResult> GetLikingUsers(string tweetId, CancellationToken cancellationToken = default);

    Task<XEngagementPollResult> GetRepostedUsers(string tweetId, CancellationToken cancellationToken = default);

    Task SendReply(string tweetId, string message, CancellationToken cancellationToken = default);
}

public sealed record XReplyPollResult
{
    public IReadOnlyList<XTweetReply> Replies { get; init; } = [];

    public string? NewestReplyId { get; init; }

    public XApiRateLimit RateLimit { get; init; } = new();
}

public sealed record XEngagementPollResult
{
    public IReadOnlyList<XUserProfile> Users { get; init; } = [];

    public XApiRateLimit RateLimit { get; init; } = new();
}

public sealed record XApiRateLimit
{
    public int? Remaining { get; init; }

    public DateTimeOffset? ResetAt { get; init; }
}

public sealed record XTweetReply
{
    public required string Id { get; init; }

    public required string AuthorId { get; init; }

    public required string Text { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required XUserProfile Author { get; init; }
}

public sealed record XUserProfile
{
    public required string Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Username { get; init; } = string.Empty;
}
