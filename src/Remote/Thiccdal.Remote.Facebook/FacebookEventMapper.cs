using System.Globalization;
using System.Text.Json;
using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Infrastructure.Facebook;

namespace Thiccdal.Remote.Facebook;

public static class FacebookEventMapper
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static ChatEvent ToChatEvent(FacebookComment comment, string liveVideoId)
    {
        ArgumentNullException.ThrowIfNull(comment);
        ArgumentException.ThrowIfNullOrWhiteSpace(liveVideoId);

        string author = string.IsNullOrWhiteSpace(comment.From.Name)
            ? comment.From.Id
            : comment.From.Name;

        return new ChatEvent
        {
            Source = PlatformEventSource.Facebook,
            Type = PlatformEventType.ChatMessage,
            PlatformUserId = comment.From.Id,
            Author = author,
            Channel = liveVideoId,
            ExternalId = comment.Id,
            Content = comment.Message,
            OccurredAt = ParseCreatedTime(comment.CreatedTime).UtcDateTime,
            Summary = $"{author}: {Truncate(comment.Message)}",
            RawData = BuildCommentRawData(comment, liveVideoId)
        };
    }

    public static PlatformEvent ToReactionEvent(FacebookReaction reaction, string liveVideoId)
    {
        ArgumentNullException.ThrowIfNull(reaction);
        ArgumentException.ThrowIfNullOrWhiteSpace(liveVideoId);

        if (!IsKnownReactionType(reaction.Type))
        {
            return new RawEvent
            {
                Source = PlatformEventSource.Facebook,
                Type = PlatformEventType.Raw,
                Author = string.IsNullOrWhiteSpace(reaction.Name) ? reaction.Id : reaction.Name,
                Channel = liveVideoId,
                ExternalId = reaction.Id,
                OccurredAt = DateTime.UtcNow,
                Summary = $"Unrecognized Facebook reaction type: {reaction.Type}",
                RawData = JsonSerializer.Serialize(reaction, SerializerOptions)
            };
        }

        string author = string.IsNullOrWhiteSpace(reaction.Name)
            ? reaction.Id
            : reaction.Name;

        return new ReactionEvent
        {
            Source = PlatformEventSource.Facebook,
            Type = PlatformEventType.Raw,
            Author = author,
            Channel = liveVideoId,
            ExternalId = reaction.Id,
            EmoteName = reaction.Type,
            EmoteId = null,
            MessageId = liveVideoId,
            OccurredAt = DateTime.UtcNow,
            Summary = $"{author} reacted with {reaction.Type}",
            RawData = JsonSerializer.Serialize(reaction, SerializerOptions)
        };
    }

    public static DateTimeOffset ParseCreatedTime(string createdTime)
    {
        if (DateTimeOffset.TryParse(
                createdTime,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset parsed))
        {
            return parsed;
        }

        return DateTimeOffset.UtcNow;
    }

    private static bool IsKnownReactionType(string reactionType)
    {
        return reactionType is "LIKE" or "LOVE" or "WOW" or "HAHA" or "SAD" or "ANGRY";
    }

    private static string Truncate(string content)
    {
        return content.Length <= 50
            ? content
            : $"{content[..47]}...";
    }

    private static string BuildCommentRawData(FacebookComment comment, string liveVideoId)
    {
        return JsonSerializer.Serialize(
            new
            {
                payload = new
                {
                    @event = new
                    {
                        user_id = comment.From.Id,
                        live_video_id = liveVideoId,
                        comment_id = comment.Id
                    }
                },
                comment
            },
            SerializerOptions);
    }
}
