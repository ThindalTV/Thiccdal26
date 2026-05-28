using System.Text.Json;
using Discord;
using Discord.WebSocket;
using Thiccdal.Infrastructure.Bot.Models;

namespace Thiccdal.Remote.Discord;

/// <summary>
/// Maps Discord.Net gateway events to normalized PlatformEvent records.
/// </summary>
public static class DiscordEventMapper
{
    /// <summary>
    /// Converts a Discord socket message to a normalized ChatEvent.
    /// </summary>
    public static ChatEvent ToChatEvent(SocketMessage message, string channelId, string channelName)
    {
        return new ChatEvent
        {
            Source = PlatformEventSource.Discord,
            Type = PlatformEventType.ChatMessage,
            PlatformUserId = message.Author.Id.ToString(),
            Author = message.Author.GlobalName ?? message.Author.Username,
            Channel = channelId,
            ExternalId = message.Id.ToString(),
            Content = message.Content,
            OccurredAt = message.Timestamp.UtcDateTime,
            Summary = $"{message.Author.Username}: {TruncateContent(message.Content)}",
            RawData = JsonSerializer.Serialize(new
            {
                payload = new
                {
                    @event = new
                    {
                        user_id = message.Author.Id.ToString(),
                        channel_id = channelId,
                        channel_name = channelName
                    }
                },
                message = new
                {
                    id = message.Id.ToString(),
                    content = message.Content
                },
                author = new
                {
                    id = message.Author.Id.ToString(),
                    username = message.Author.Username,
                    global_name = message.Author.GlobalName
                }
            })
        };
    }

    /// <summary>
    /// Converts a Discord reaction to a normalized ReactionEvent.
    /// </summary>
    public static ReactionEvent ToReactionEvent(SocketReaction reaction, string channelName, string? userName)
    {
        return new ReactionEvent
        {
            Source = PlatformEventSource.Discord,
            Type = PlatformEventType.Raw,
            Author = userName ?? "Unknown",
            Channel = channelName,
            ExternalId = reaction.MessageId.ToString(),
            EmoteName = reaction.Emote.Name,
            EmoteId = reaction.Emote is Emote customEmote ? customEmote.Id.ToString() : null,
            MessageId = reaction.MessageId.ToString(),
            OccurredAt = DateTime.UtcNow,
            Summary = $"{userName} reacted with {reaction.Emote.Name}",
            RawData = string.Empty
        };
    }

    /// <summary>
    /// Creates a base PlatformEvent for Discord user joined event.
    /// </summary>
    public static PlatformEvent ToUserJoinedEvent(SocketGuildUser user, string guildName)
    {
        return new PlatformEvent
        {
            Source = PlatformEventSource.Discord,
            Type = PlatformEventType.Raw,
            Author = user.GlobalName ?? user.Username,
            Channel = guildName,
            ExternalId = user.Id.ToString(),
            OccurredAt = DateTime.UtcNow,
            Summary = $"{user.Username} joined the server",
            RawData = $"DiscordUserJoined:{user.Id}"
        };
    }

    /// <summary>
    /// Creates a base PlatformEvent for Discord user left event.
    /// </summary>
    public static PlatformEvent ToUserLeftEvent(SocketUser user, SocketGuild guild)
    {
        return new PlatformEvent
        {
            Source = PlatformEventSource.Discord,
            Type = PlatformEventType.Raw,
            Author = user.GlobalName ?? user.Username,
            Channel = guild.Name,
            ExternalId = user.Id.ToString(),
            OccurredAt = DateTime.UtcNow,
            Summary = $"{user.Username} left the server",
            RawData = $"DiscordUserLeft:{user.Id}"
        };
    }

    /// <summary>
    /// Creates a base PlatformEvent for Discord message deleted event.
    /// </summary>
    public static PlatformEvent ToMessageDeletedEvent(ulong messageId, Cacheable<IMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel)
    {
        return new PlatformEvent
        {
            Source = PlatformEventSource.Discord,
            Type = PlatformEventType.Raw,
            Author = "System",
            Channel = channel.HasValue ? channel.Value.Name : "Unknown",
            ExternalId = messageId.ToString(),
            OccurredAt = DateTime.UtcNow,
            Summary = "Message deleted",
            RawData = $"DiscordMessageDeleted:{messageId}"
        };
    }

    private static string TruncateContent(string content, int maxLength = 50)
    {
        if (content.Length <= maxLength)
        {
            return content;
        }

        return content[..(maxLength - 3)] + "...";
    }
}
