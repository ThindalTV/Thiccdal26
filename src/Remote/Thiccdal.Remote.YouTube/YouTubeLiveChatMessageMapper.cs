using System.Text.Json;
using Microsoft.Extensions.Logging;
using Thiccdal.Infrastructure.Bot.Models;

namespace Thiccdal.Remote.YouTube;

public sealed class YouTubeLiveChatMessageMapper
{
    private readonly ILogger<YouTubeLiveChatMessageMapper> _logger;

    public YouTubeLiveChatMessageMapper(ILogger<YouTubeLiveChatMessageMapper> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<PlatformEvent> MapMessages(string rawJson, string channelId)
    {
        var events = new List<PlatformEvent>();

        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;

            if (!root.TryGetProperty("items", out var items))
            {
                return events;
            }

            foreach (var item in items.EnumerateArray())
            {
                try
                {
                    string itemJson = item.GetRawText();
                    PlatformEvent? platformEvent = MapSingleMessage(item, channelId, itemJson);
                    if (platformEvent is not null)
                    {
                        events.Add(platformEvent);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Failed to map YouTube message item");
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to parse YouTube live chat JSON");
        }

        return events;
    }

    private PlatformEvent? MapSingleMessage(JsonElement item, string channelId, string rawJson)
    {
        string messageId = item.GetProperty("id").GetString() ?? string.Empty;
        var snippet = item.GetProperty("snippet");
        var authorDetails = item.GetProperty("authorDetails");

        string type = snippet.GetProperty("type").GetString() ?? string.Empty;
        string authorDisplayName = authorDetails.GetProperty("displayName").GetString() ?? string.Empty;
        DateTime publishedAt = snippet.GetProperty("publishedAt").GetDateTime();

        return type switch
        {
            "textMessageEvent" => MapChatMessage(messageId, snippet, authorDetails, channelId, publishedAt, rawJson),
            "superChatEvent" => MapSuperChatEvent(messageId, snippet, authorDetails, channelId, publishedAt, rawJson),
            "newSponsorEvent" or "memberMilestoneChatEvent" => MapMembershipEvent(messageId, snippet, authorDetails, channelId, publishedAt, type, rawJson),
            _ => MapUnrecognizedEvent(messageId, authorDisplayName, channelId, publishedAt, type, rawJson)
        };
    }

    private static ChatEvent MapChatMessage(
        string messageId,
        JsonElement snippet,
        JsonElement authorDetails,
        string channelId,
        DateTime publishedAt,
        string rawJson)
    {
        string authorDisplayName = authorDetails.GetProperty("displayName").GetString() ?? string.Empty;
        string messageText = snippet.GetProperty("textMessageDetails").GetProperty("messageText").GetString() ?? string.Empty;

        return new ChatEvent
        {
            Source = PlatformEventSource.YouTube,
            Type = PlatformEventType.ChatMessage,
            SourceEventType = "textMessageEvent",
            PlatformUserId = authorDetails.GetProperty("channelId").GetString() ?? string.Empty,
            Author = authorDisplayName,
            Channel = channelId,
            ExternalId = messageId,
            Summary = $"{authorDisplayName}: {messageText}",
            Content = messageText,
            OccurredAt = publishedAt,
            Parts =
            [
                new ChatMessagePart
                {
                    Type = ChatMessagePartType.Text,
                    Text = messageText
                }
            ],
            RawData = rawJson
        };
    }

    private static SuperChatEvent MapSuperChatEvent(
        string messageId,
        JsonElement snippet,
        JsonElement authorDetails,
        string channelId,
        DateTime publishedAt,
        string rawJson)
    {
        string authorDisplayName = authorDetails.GetProperty("displayName").GetString() ?? string.Empty;
        var superChatDetails = snippet.GetProperty("superChatDetails");
        long amountMicros = superChatDetails.GetProperty("amountMicros").GetInt64();
        string currency = superChatDetails.GetProperty("currency").GetString() ?? "USD";
        string amountDisplayString = superChatDetails.TryGetProperty("amountDisplayString", out JsonElement amountDisplayStringProperty)
            ? amountDisplayStringProperty.GetString() ?? string.Empty
            : string.Empty;
        string userComment = superChatDetails.TryGetProperty("userComment", out var commentProp)
            ? commentProp.GetString() ?? string.Empty
            : string.Empty;

        decimal amount = amountMicros / 1_000_000m;
        string displayString = string.IsNullOrWhiteSpace(amountDisplayString)
            ? $"{amount:F2} {currency}"
            : amountDisplayString;

        return new SuperChatEvent
        {
            Source = PlatformEventSource.YouTube,
            Type = PlatformEventType.SuperChat,
            SourceEventType = "superChatEvent",
            Author = authorDisplayName,
            Channel = channelId,
            ExternalId = messageId,
            Summary = string.IsNullOrWhiteSpace(userComment)
                ? $"{authorDisplayName} sent {displayString}"
                : $"{authorDisplayName} sent {displayString}: {userComment}",
            OccurredAt = publishedAt,
            AmountMicros = amountMicros,
            Currency = currency,
            DisplayString = displayString,
            UserComment = string.IsNullOrWhiteSpace(userComment) ? null : userComment,
            RawData = rawJson
        };
    }

    private static MembershipEvent MapMembershipEvent(
        string messageId,
        JsonElement snippet,
        JsonElement authorDetails,
        string channelId,
        DateTime publishedAt,
        string eventType,
        string rawJson)
    {
        string authorDisplayName = authorDetails.GetProperty("displayName").GetString() ?? string.Empty;
        string summary = eventType == "newSponsorEvent"
            ? $"{authorDisplayName} became a member"
            : $"{authorDisplayName} membership milestone";
        JsonElement membershipDetails = eventType == "newSponsorEvent"
            ? default
            : snippet.GetProperty("memberMilestoneChatDetails");
        string levelName = membershipDetails.ValueKind != JsonValueKind.Undefined
            && membershipDetails.TryGetProperty("memberLevelName", out JsonElement levelNameProperty)
            ? levelNameProperty.GetString() ?? string.Empty
            : string.Empty;
        int? monthCount = membershipDetails.ValueKind != JsonValueKind.Undefined
            && membershipDetails.TryGetProperty("memberMonth", out JsonElement monthProperty)
            && monthProperty.TryGetInt32(out int monthValue)
            ? monthValue
            : null;

        return new MembershipEvent
        {
            Source = PlatformEventSource.YouTube,
            Type = PlatformEventType.Membership,
            SourceEventType = eventType,
            Author = authorDisplayName,
            Channel = channelId,
            ExternalId = messageId,
            Summary = summary,
            OccurredAt = publishedAt,
            LevelName = levelName,
            MonthCount = monthCount,
            RawData = rawJson
        };
    }

    private RawEvent MapUnrecognizedEvent(
        string messageId,
        string authorDisplayName,
        string channelId,
        DateTime publishedAt,
        string eventType,
        string rawJson)
    {
        _logger.LogDebug("Unrecognized YouTube event type: {EventType}", eventType);

        return new RawEvent
        {
            Source = PlatformEventSource.YouTube,
            Type = PlatformEventType.Raw,
            SourceEventType = eventType,
            Author = authorDisplayName,
            Channel = channelId,
            ExternalId = messageId,
            Summary = $"Unrecognized YouTube event: {eventType}",
            OccurredAt = publishedAt,
            RawData = rawJson
        };
    }
}
