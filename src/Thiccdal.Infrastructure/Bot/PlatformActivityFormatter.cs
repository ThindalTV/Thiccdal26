using System.Net;
using System.Text;
using Thiccdal.Infrastructure.Bot.Models;

namespace Thiccdal.Infrastructure.Bot;

/// <summary>
/// Formats normalized platform events for downstream activity surfaces.
/// </summary>
public static class PlatformActivityFormatter
{
    /// <summary>
    /// Creates an activity feed entry from a normalized platform event.
    /// </summary>
    /// <param name="platformEvent">The event to format.</param>
    /// <returns>The formatted activity feed entry.</returns>
    public static ActivityFeedEntry CreateEntry(PlatformEvent platformEvent)
    {
        ArgumentNullException.ThrowIfNull(platformEvent);

        return platformEvent switch
        {
            ChatEvent chatEvent => CreateChatEntry(chatEvent),
            TwitchFollowEvent followEvent => CreateEntry(
                followEvent,
                BuildEventContent(followEvent.Author, "followed the channel.")),
            TwitchSubscribeEvent subscribeEvent => CreateEntry(
                subscribeEvent,
                BuildSubscriptionContent(subscribeEvent)),
            TwitchCheerEvent cheerEvent => CreateEntry(
                cheerEvent,
                BuildCheerContent(cheerEvent)),
            TwitchRaidEvent raidEvent => CreateEntry(
                raidEvent,
                $"raided with {raidEvent.ViewerCount} viewers."),
            TwitchRedeemEvent redeemEvent => CreateEntry(
                redeemEvent,
                string.IsNullOrWhiteSpace(redeemEvent.RewardTitle)
                    ? "redeemed channel points."
                    : $"redeemed {redeemEvent.RewardTitle}.",
                string.IsNullOrWhiteSpace(redeemEvent.UserInput)
                    ? string.Empty
                    : $"<span class=\"activity-detail\">{WebUtility.HtmlEncode(redeemEvent.UserInput)}</span>"),
            RawEvent rawEvent => CreateEntry(rawEvent, rawEvent.Summary),
            _ => CreateEntry(platformEvent, platformEvent.Summary)
        };
    }

    private static ActivityFeedEntry CreateChatEntry(ChatEvent chatEvent)
    {
        string htmlContent = BuildChatHtml(chatEvent);
        string plainText = string.IsNullOrWhiteSpace(chatEvent.Content)
            ? chatEvent.Summary
            : chatEvent.Content;

        return new ActivityFeedEntry(
            chatEvent.DisplayAuthor,
            plainText,
            htmlContent,
            chatEvent.Source,
            chatEvent.Type,
            chatEvent.OccurredAt,
            AccentColor: string.Empty,
            SenderColor: chatEvent.Color,
            Badges: chatEvent.Badges,
            Parts: chatEvent.Parts);
    }

    private static ActivityFeedEntry CreateEntry(PlatformEvent platformEvent, string content, string? detailMarkup = null)
    {
        string htmlContent = $"<span class=\"activity-event activity-event--{platformEvent.Type.ToString().ToLowerInvariant()}\">{WebUtility.HtmlEncode(content)}</span>";
        if (!string.IsNullOrWhiteSpace(detailMarkup))
        {
            htmlContent = $"{htmlContent} {detailMarkup}";
        }

        return new ActivityFeedEntry(
            platformEvent.Author,
            content,
            htmlContent,
            platformEvent.Source,
            platformEvent.Type,
            platformEvent.OccurredAt,
            GetAccentColor(platformEvent.Type));
    }

    private static string BuildSubscriptionContent(TwitchSubscribeEvent subscribeEvent)
    {
        string tierLabel = string.IsNullOrWhiteSpace(subscribeEvent.Tier)
            ? "subscribed."
            : $"subscribed at Tier {subscribeEvent.Tier}.";

        if (subscribeEvent.CumulativeMonths is > 1)
        {
            tierLabel = $"{tierLabel.TrimEnd('.')} ({subscribeEvent.CumulativeMonths} months).";
        }

        return tierLabel;
    }

    private static string BuildCheerContent(TwitchCheerEvent cheerEvent)
    {
        return string.IsNullOrWhiteSpace(cheerEvent.Message)
            ? $"cheered {cheerEvent.Bits} bits."
            : $"cheered {cheerEvent.Bits} bits: {cheerEvent.Message}";
    }

    private static string BuildEventContent(string author, string content)
    {
        return string.IsNullOrWhiteSpace(author) ? content : content;
    }

    private static string GetAccentColor(PlatformEventType type)
    {
        return type switch
        {
            PlatformEventType.Follow => "#3CC864",
            PlatformEventType.Raid => "#7C3AED",
            PlatformEventType.Cheer => "#F59E0B",
            PlatformEventType.Subscribe => "#60A5FA",
            PlatformEventType.Redeem => "#F472B6",
            _ => string.Empty
        };
    }

    private static string BuildChatHtml(ChatEvent chatEvent)
    {
        StringBuilder builder = new();

        foreach (ChatBadge badge in chatEvent.Badges)
        {
            builder.Append("<span class=\"chat-badge\" title=\"");
            builder.Append(WebUtility.HtmlEncode($"{badge.SetId}:{badge.Version}"));
            builder.Append("\">");
            builder.Append(WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(badge.Info) ? badge.Version : badge.Info));
            builder.Append("</span>");
        }

        IReadOnlyList<ChatMessagePart> parts = chatEvent.Parts.Count == 0
            ? [new ChatMessagePart { Type = ChatMessagePartType.Text, Text = chatEvent.Content }]
            : chatEvent.Parts;

        foreach (ChatMessagePart part in parts)
        {
            switch (part.Type)
            {
                case ChatMessagePartType.Emote when !string.IsNullOrWhiteSpace(part.AssetUrl):
                    builder.Append("<img class=\"chat-inline-emote\" src=\"");
                    builder.Append(WebUtility.HtmlEncode(part.AssetUrl));
                    builder.Append("\" alt=\"");
                    builder.Append(WebUtility.HtmlEncode(part.Text));
                    builder.Append("\" title=\"");
                    builder.Append(WebUtility.HtmlEncode(part.Text));
                    builder.Append("\" />");
                    break;

                case ChatMessagePartType.Cheer:
                    builder.Append("<span class=\"chat-inline-cheer\">");
                    builder.Append(WebUtility.HtmlEncode(part.Text));
                    builder.Append("</span>");
                    break;

                case ChatMessagePartType.Mention:
                    builder.Append("<span class=\"chat-inline-mention\">");
                    builder.Append(WebUtility.HtmlEncode(part.Text));
                    builder.Append("</span>");
                    break;

                default:
                    builder.Append(WebUtility.HtmlEncode(part.Text));
                    break;
            }
        }

        return builder.ToString();
    }
}
