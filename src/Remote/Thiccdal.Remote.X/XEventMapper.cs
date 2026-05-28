using System.Text.Json;
using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Infrastructure.X;

namespace Thiccdal.Remote.X;

internal static class XEventMapper
{
    public static ChatEvent ToChatEvent(XTweetReply tweet, string channel)
    {
        string displayName = GetDisplayName(tweet.Author);

        return new ChatEvent
        {
            Source = PlatformEventSource.X,
            Type = PlatformEventType.ChatMessage,
            PlatformUserId = tweet.AuthorId,
            Author = displayName,
            Channel = channel,
            ExternalId = tweet.Id,
            Content = tweet.Text,
            Summary = $"{displayName}: {tweet.Text}",
            OccurredAt = tweet.CreatedAt.UtcDateTime,
            RawData = JsonSerializer.Serialize(new
            {
                payload = new
                {
                    @event = new
                    {
                        user_id = tweet.AuthorId,
                        display_name = displayName,
                        username = tweet.Author.Username,
                        tweet_id = tweet.Id
                    }
                },
                tweet = new
                {
                    id = tweet.Id,
                    author_id = tweet.AuthorId,
                    text = tweet.Text,
                    created_at = tweet.CreatedAt
                },
                author = new
                {
                    id = tweet.Author.Id,
                    name = tweet.Author.Name,
                    username = tweet.Author.Username
                }
            })
        };
    }

    public static PlatformEvent ToLikeEvent(string tweetId, XUserProfile user, DateTimeOffset occurredAt, string channel)
    {
        string displayName = GetDisplayName(user);

        return new PlatformEvent
        {
            Source = PlatformEventSource.X,
            Type = PlatformEventType.Raw,
            Author = displayName,
            Channel = channel,
            ExternalId = $"like:{tweetId}:{user.Id}",
            Summary = $"{displayName} liked the tracked X post",
            OccurredAt = occurredAt.UtcDateTime,
            RawData = JsonSerializer.Serialize(new
            {
                payload = new
                {
                    @event = new
                    {
                        kind = "XLikeEvent",
                        user_id = user.Id,
                        tweet_id = tweetId
                    }
                },
                user = new
                {
                    id = user.Id,
                    name = user.Name,
                    username = user.Username
                }
            })
        };
    }

    public static PlatformEvent ToRepostEvent(string tweetId, XUserProfile user, DateTimeOffset occurredAt, string channel)
    {
        string displayName = GetDisplayName(user);

        return new PlatformEvent
        {
            Source = PlatformEventSource.X,
            Type = PlatformEventType.Raw,
            Author = displayName,
            Channel = channel,
            ExternalId = $"repost:{tweetId}:{user.Id}",
            Summary = $"{displayName} reposted the tracked X post",
            OccurredAt = occurredAt.UtcDateTime,
            RawData = JsonSerializer.Serialize(new
            {
                payload = new
                {
                    @event = new
                    {
                        kind = "XRepostEvent",
                        user_id = user.Id,
                        tweet_id = tweetId
                    }
                },
                user = new
                {
                    id = user.Id,
                    name = user.Name,
                    username = user.Username
                }
            })
        };
    }

    private static string GetDisplayName(XUserProfile user)
    {
        if (!string.IsNullOrWhiteSpace(user.Name))
        {
            return user.Name;
        }

        if (!string.IsNullOrWhiteSpace(user.Username))
        {
            return user.Username;
        }

        return user.Id;
    }
}
