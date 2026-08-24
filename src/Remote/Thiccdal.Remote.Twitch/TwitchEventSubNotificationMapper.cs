using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Infrastructure.Twitch;

namespace Thiccdal.Remote.Twitch;

public sealed class TwitchEventSubNotificationMapper
{
    private readonly IEmoteRenderingOptions _emoteRenderingOptions;

    public TwitchEventSubNotificationMapper(IEmoteRenderingOptions emoteRenderingOptions)
    {
        _emoteRenderingOptions = emoteRenderingOptions;
    }

    public PlatformEvent Map(string rawPayload)
    {
        using JsonDocument document = JsonDocument.Parse(rawPayload);
        JsonElement root = document.RootElement;

        string subscriptionType = GetString(root, "metadata", "subscription_type");
        if (string.IsNullOrWhiteSpace(subscriptionType))
        {
            subscriptionType = GetString(root, "payload", "subscription", "type");
        }

        JsonElement eventElement = TryGetElement(root, out JsonElement payloadElement, "payload") &&
                                   TryGetElement(payloadElement, out JsonElement mappedEventElement, "event")
            ? mappedEventElement
            : default;

        return subscriptionType switch
        {
            "channel.chat.message" when eventElement.ValueKind == JsonValueKind.Object => MapChatEvent(eventElement, rawPayload, root, subscriptionType),
            "channel.follow" when eventElement.ValueKind == JsonValueKind.Object => MapFollowEvent(eventElement, rawPayload, root, subscriptionType),
            "channel.subscribe" when eventElement.ValueKind == JsonValueKind.Object => MapSubscribeEvent(eventElement, rawPayload, root, subscriptionType),
            "channel.subscription.message" when eventElement.ValueKind == JsonValueKind.Object => MapSubscribeEvent(eventElement, rawPayload, root, subscriptionType),
            "channel.subscription.gift" when eventElement.ValueKind == JsonValueKind.Object => MapGiftSubscriptionEvent(eventElement, rawPayload, root, subscriptionType),
            "channel.cheer" when eventElement.ValueKind == JsonValueKind.Object => MapCheerEvent(eventElement, rawPayload, root, subscriptionType),
            "channel.raid" when eventElement.ValueKind == JsonValueKind.Object => MapRaidEvent(eventElement, rawPayload, root, subscriptionType),
            "channel.channel_points_custom_reward_redemption.add" when eventElement.ValueKind == JsonValueKind.Object => MapRedeemEvent(eventElement, rawPayload, root, subscriptionType),
            _ => MapRawEvent(rawPayload, root, subscriptionType)
        };
    }

    private ChatEvent MapChatEvent(JsonElement eventElement, string rawPayload, JsonElement root, string subscriptionType)
    {
        string author = FirstNonEmpty(
            GetString(eventElement, "chatter_user_name"),
            GetString(eventElement, "chatter_user_login"),
            GetString(eventElement, "chatter_user_id"),
            "unknown");
        string channel = FirstNonEmpty(
            GetString(eventElement, "broadcaster_user_login"),
            GetString(eventElement, "broadcaster_user_name"),
            GetString(eventElement, "broadcaster_user_id"),
            "twitch");
        string content = GetString(eventElement, "message", "text");
        IReadOnlyList<ChatMessagePart> parts = GetMessageParts(eventElement);

        return new ChatEvent
        {
            Source = PlatformEventSource.Twitch,
            Type = PlatformEventType.ChatMessage,
            SourceEventType = subscriptionType,
            PlatformUserId = GetString(eventElement, "chatter_user_id"),
            Author = author,
            Channel = channel,
            ExternalId = FirstNonEmpty(GetString(eventElement, "message_id"), GetString(root, "metadata", "message_id")),
            Summary = content,
            OccurredAt = GetOccurredAt(root, eventElement),
            RawData = rawPayload,
            Content = content,
            HtmlContent = BuildHtml(parts),
            Color = GetString(eventElement, "color"),
            Parts = parts,
            Badges = GetBadges(eventElement)
        };
    }

    private TwitchFollowEvent MapFollowEvent(JsonElement eventElement, string rawPayload, JsonElement root, string subscriptionType)
    {
        string author = FirstNonEmpty(GetString(eventElement, "user_name"), GetString(eventElement, "user_login"), GetString(eventElement, "user_id"), "unknown");
        string channel = FirstNonEmpty(GetString(eventElement, "broadcaster_user_login"), GetString(eventElement, "broadcaster_user_name"), GetString(eventElement, "broadcaster_user_id"), "twitch");

        return new TwitchFollowEvent
        {
            Source = PlatformEventSource.Twitch,
            Type = PlatformEventType.Follow,
            SourceEventType = subscriptionType,
            Author = author,
            Channel = channel,
            ExternalId = FirstNonEmpty(GetString(root, "metadata", "message_id"), GetString(eventElement, "user_id")),
            Summary = $"{author} followed {channel}",
            OccurredAt = GetOccurredAt(root, eventElement),
            RawData = rawPayload,
            FollowerUserId = GetString(eventElement, "user_id")
        };
    }

    private TwitchSubscribeEvent MapSubscribeEvent(JsonElement eventElement, string rawPayload, JsonElement root, string subscriptionType)
    {
        string author = FirstNonEmpty(GetString(eventElement, "user_name"), GetString(eventElement, "user_login"), GetString(eventElement, "user_id"), "unknown");
        string channel = FirstNonEmpty(GetString(eventElement, "broadcaster_user_login"), GetString(eventElement, "broadcaster_user_name"), GetString(eventElement, "broadcaster_user_id"), "twitch");
        bool isGift = GetBoolean(eventElement, "is_gift");
        int? cumulativeMonths = GetNullableInt(eventElement, "cumulative_months");
        string tier = GetString(eventElement, "tier");
        string gifterName = FirstNonEmpty(GetString(eventElement, "gifter_user_name"), GetString(eventElement, "gifter_user_login"));
        string summary = isGift
            ? $"{author} received a gifted Tier {tier} sub{BuildMonthSuffix(cumulativeMonths)}"
            : $"{author} subscribed at Tier {tier}{BuildMonthSuffix(cumulativeMonths)}";

        if (!string.IsNullOrWhiteSpace(gifterName))
        {
            summary = $"{summary} from {gifterName}";
        }

        return new TwitchSubscribeEvent
        {
            Source = PlatformEventSource.Twitch,
            Type = PlatformEventType.Subscribe,
            SourceEventType = subscriptionType,
            Author = author,
            Channel = channel,
            ExternalId = FirstNonEmpty(GetString(root, "metadata", "message_id"), GetString(eventElement, "user_id")),
            Summary = summary,
            OccurredAt = GetOccurredAt(root, eventElement),
            RawData = rawPayload,
            Tier = tier,
            IsGift = isGift,
            GifterUserId = GetString(eventElement, "gifter_user_id"),
            CumulativeMonths = cumulativeMonths
        };
    }

    private TwitchSubscribeEvent MapGiftSubscriptionEvent(JsonElement eventElement, string rawPayload, JsonElement root, string subscriptionType)
    {
        bool isAnonymous = GetBoolean(eventElement, "is_anonymous");
        string author = isAnonymous
            ? "Anonymous"
            : FirstNonEmpty(GetString(eventElement, "user_name"), GetString(eventElement, "user_login"), GetString(eventElement, "user_id"), "Anonymous");
        string channel = FirstNonEmpty(GetString(eventElement, "broadcaster_user_login"), GetString(eventElement, "broadcaster_user_name"), GetString(eventElement, "broadcaster_user_id"), "twitch");
        int giftCount = GetNullableInt(eventElement, "total") ?? 1;
        string tier = GetString(eventElement, "tier");

        return new TwitchSubscribeEvent
        {
            Source = PlatformEventSource.Twitch,
            Type = PlatformEventType.Subscribe,
            SourceEventType = subscriptionType,
            Author = author,
            Channel = channel,
            ExternalId = FirstNonEmpty(GetString(root, "metadata", "message_id"), GetString(eventElement, "user_id")),
            Summary = $"{author} gifted {giftCount} Tier {tier} sub{(giftCount == 1 ? string.Empty : "s")}",
            OccurredAt = GetOccurredAt(root, eventElement),
            RawData = rawPayload,
            Tier = tier,
            IsGift = true,
            GifterUserId = isAnonymous ? string.Empty : GetString(eventElement, "user_id"),
            GiftCount = giftCount
        };
    }

    private TwitchCheerEvent MapCheerEvent(JsonElement eventElement, string rawPayload, JsonElement root, string subscriptionType)
    {
        string author = FirstNonEmpty(GetString(eventElement, "user_name"), GetString(eventElement, "user_login"), GetString(eventElement, "user_id"), "Anonymous");
        string channel = FirstNonEmpty(GetString(eventElement, "broadcaster_user_login"), GetString(eventElement, "broadcaster_user_name"), GetString(eventElement, "broadcaster_user_id"), "twitch");
        int bits = GetNullableInt(eventElement, "bits") ?? 0;
        string message = GetString(eventElement, "message");
        string summary = string.IsNullOrWhiteSpace(message)
            ? $"{author} cheered {bits} bits"
            : $"{author} cheered {bits} bits: {message}";

        return new TwitchCheerEvent
        {
            Source = PlatformEventSource.Twitch,
            Type = PlatformEventType.Cheer,
            SourceEventType = subscriptionType,
            Author = author,
            Channel = channel,
            ExternalId = FirstNonEmpty(GetString(root, "metadata", "message_id"), GetString(eventElement, "user_id")),
            Summary = summary,
            OccurredAt = GetOccurredAt(root, eventElement),
            RawData = rawPayload,
            Bits = bits,
            Message = message
        };
    }

    private TwitchRaidEvent MapRaidEvent(JsonElement eventElement, string rawPayload, JsonElement root, string subscriptionType)
    {
        string author = FirstNonEmpty(GetString(eventElement, "from_broadcaster_user_name"), GetString(eventElement, "from_broadcaster_user_login"), GetString(eventElement, "from_broadcaster_user_id"), "unknown");
        string channel = FirstNonEmpty(GetString(eventElement, "to_broadcaster_user_login"), GetString(eventElement, "to_broadcaster_user_name"), GetString(eventElement, "to_broadcaster_user_id"), "twitch");
        int viewerCount = GetNullableInt(eventElement, "viewers") ?? 0;

        return new TwitchRaidEvent
        {
            Source = PlatformEventSource.Twitch,
            Type = PlatformEventType.Raid,
            SourceEventType = subscriptionType,
            Author = author,
            Channel = channel,
            ExternalId = FirstNonEmpty(GetString(root, "metadata", "message_id"), author),
            Summary = $"{author} raided {channel} with {viewerCount} viewers",
            OccurredAt = GetOccurredAt(root, eventElement),
            RawData = rawPayload,
            RaidingChannel = FirstNonEmpty(GetString(eventElement, "from_broadcaster_user_login"), author),
            ViewerCount = viewerCount
        };
    }

    private TwitchRedeemEvent MapRedeemEvent(JsonElement eventElement, string rawPayload, JsonElement root, string subscriptionType)
    {
        string author = FirstNonEmpty(GetString(eventElement, "user_name"), GetString(eventElement, "user_login"), GetString(eventElement, "user_id"), "unknown");
        string channel = FirstNonEmpty(GetString(eventElement, "broadcaster_user_login"), GetString(eventElement, "broadcaster_user_name"), GetString(eventElement, "broadcaster_user_id"), "twitch");
        string rewardTitle = GetString(eventElement, "reward", "title");

        return new TwitchRedeemEvent
        {
            Source = PlatformEventSource.Twitch,
            Type = PlatformEventType.Redeem,
            SourceEventType = subscriptionType,
            Author = author,
            Channel = channel,
            ExternalId = FirstNonEmpty(GetString(root, "metadata", "message_id"), GetString(eventElement, "id")),
            Summary = string.IsNullOrWhiteSpace(rewardTitle)
                ? $"{author} redeemed channel points"
                : $"{author} redeemed {rewardTitle}",
            OccurredAt = GetOccurredAt(root, eventElement),
            RawData = rawPayload,
            RewardId = GetString(eventElement, "reward", "id"),
            RewardTitle = rewardTitle,
            UserInput = GetString(eventElement, "user_input")
        };
    }

    private RawEvent MapRawEvent(string rawPayload, JsonElement root, string subscriptionType)
    {
        return new RawEvent
        {
            Source = PlatformEventSource.Twitch,
            Type = PlatformEventType.Raw,
            SourceEventType = subscriptionType,
            Author = "twitch",
            Channel = "twitch",
            ExternalId = GetString(root, "metadata", "message_id"),
            Summary = string.IsNullOrWhiteSpace(subscriptionType) ? "Unmapped Twitch EventSub payload" : $"Unmapped Twitch EventSub payload: {subscriptionType}",
            OccurredAt = GetOccurredAt(root, default),
            RawData = rawPayload
        };
    }

    private IReadOnlyList<ChatMessagePart> GetMessageParts(JsonElement eventElement)
    {
        if (!TryGetElement(eventElement, out JsonElement messageElement, "message") ||
            !TryGetElement(messageElement, out JsonElement fragmentsElement, "fragments") ||
            fragmentsElement.ValueKind != JsonValueKind.Array)
        {
            string fallbackContent = GetString(eventElement, "message", "text");
            if (string.IsNullOrWhiteSpace(fallbackContent))
            {
                return [];
            }

            return
            [
                new ChatMessagePart
                {
                    Type = ChatMessagePartType.Text,
                    Text = fallbackContent
                }
            ];
        }

        List<ChatMessagePart> parts = [];
        foreach (JsonElement fragment in fragmentsElement.EnumerateArray())
        {
            string type = GetString(fragment, "type");
            string text = GetString(fragment, "text");
            switch (type)
            {
                case "emote":
                    string emoteId = GetString(fragment, "emote", "id");
                    bool supportsAnimated = ContainsAnimatedFormat(fragment);
                    parts.Add(new ChatMessagePart
                    {
                        Type = ChatMessagePartType.Emote,
                        Text = text,
                        ReferenceId = emoteId,
                        AssetUrl = string.IsNullOrWhiteSpace(emoteId)
                            ? string.Empty
                            : TwitchEmoteCdn.GetUrl(emoteId, _emoteRenderingOptions.UseAnimatedEmotes && supportsAnimated)
                    });
                    break;

                case "mention":
                    parts.Add(new ChatMessagePart
                    {
                        Type = ChatMessagePartType.Mention,
                        Text = text,
                        ReferenceId = GetString(fragment, "mention", "user_id")
                    });
                    break;

                case "cheermote":
                    parts.Add(new ChatMessagePart
                    {
                        Type = ChatMessagePartType.Cheer,
                        Text = text,
                        ReferenceId = GetString(fragment, "cheermote", "prefix"),
                        Amount = ExtractTrailingNumber(text)
                    });
                    break;

                default:
                    parts.Add(new ChatMessagePart
                    {
                        Type = ChatMessagePartType.Text,
                        Text = text
                    });
                    break;
            }
        }

        return parts;
    }

    private static IReadOnlyList<ChatBadge> GetBadges(JsonElement eventElement)
    {
        if (!TryGetElement(eventElement, out JsonElement badgesElement, "badges") || badgesElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        List<ChatBadge> badges = [];
        foreach (JsonElement badgeElement in badgesElement.EnumerateArray())
        {
            badges.Add(new ChatBadge(
                GetString(badgeElement, "set_id"),
                GetString(badgeElement, "id"),
                GetString(badgeElement, "info")));
        }

        return badges;
    }

    private static string BuildHtml(IReadOnlyList<ChatMessagePart> parts)
    {
        if (parts.Count == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new();
        foreach (ChatMessagePart part in parts)
        {
            switch (part.Type)
            {
                case ChatMessagePartType.Emote when !string.IsNullOrWhiteSpace(part.AssetUrl):
                    builder.Append("<img class=\"chat-inline-emote\" src=\"");
                    builder.Append(HtmlEncoder.Default.Encode(part.AssetUrl));
                    builder.Append("\" alt=\"");
                    builder.Append(HtmlEncoder.Default.Encode(part.Text));
                    builder.Append("\" title=\"");
                    builder.Append(HtmlEncoder.Default.Encode(part.Text));
                    builder.Append("\" />");
                    break;

                default:
                    builder.Append(WebUtility.HtmlEncode(part.Text));
                    break;
            }
        }

        return builder.ToString();
    }

    private static bool ContainsAnimatedFormat(JsonElement fragment)
    {
        if (!TryGetElement(fragment, out JsonElement emoteElement, "emote") ||
            !TryGetElement(emoteElement, out JsonElement formatElement, "format") ||
            formatElement.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (JsonElement format in formatElement.EnumerateArray())
        {
            if (string.Equals(format.GetString(), "animated", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static int? ExtractTrailingNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string digits = new string(value.Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
        return int.TryParse(digits, out int amount) ? amount : null;
    }

    private static DateTime GetOccurredAt(JsonElement root, JsonElement eventElement)
    {
        foreach (string propertyName in new[] { "followed_at", "subscribed_at", "redeemed_at", "started_at" })
        {
            string value = GetString(eventElement, propertyName);
            if (DateTime.TryParse(value, out DateTime parsed))
            {
                return parsed;
            }
        }

        string metadataTimestamp = GetString(root, "metadata", "message_timestamp");
        if (DateTime.TryParse(metadataTimestamp, out DateTime parsedMetadataTimestamp))
        {
            return parsedMetadataTimestamp;
        }

        return DateTime.UtcNow;
    }

    private static string FirstNonEmpty(params string[] values)
    {
        return values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }

    private static bool GetBoolean(JsonElement element, string propertyName)
    {
        return TryGetElement(element, out JsonElement property, propertyName) &&
               property.ValueKind is JsonValueKind.True or JsonValueKind.False &&
               property.GetBoolean();
    }

    private static int? GetNullableInt(JsonElement element, string propertyName)
    {
        if (!TryGetElement(element, out JsonElement property, propertyName))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetInt32(out int value) => value,
            JsonValueKind.String when int.TryParse(property.GetString(), out int value) => value,
            _ => null
        };
    }

    private static string GetString(JsonElement element, params string[] path)
    {
        JsonElement current = element;
        foreach (string segment in path)
        {
            if (!TryGetElement(current, out JsonElement next, segment))
            {
                return string.Empty;
            }

            current = next;
        }

        return current.ValueKind switch
        {
            JsonValueKind.String => current.GetString() ?? string.Empty,
            JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
            _ => current.ToString()
        };
    }

    private static bool TryGetElement(JsonElement element, out JsonElement property, params string[] path)
    {
        property = element;
        foreach (string segment in path)
        {
            if (property.ValueKind != JsonValueKind.Object || !property.TryGetProperty(segment, out JsonElement next))
            {
                property = default;
                return false;
            }

            property = next;
        }

        return true;
    }

    private static string BuildMonthSuffix(int? cumulativeMonths)
    {
        return cumulativeMonths.HasValue && cumulativeMonths.Value > 0
            ? $" ({cumulativeMonths.Value} months)"
            : string.Empty;
    }
}
