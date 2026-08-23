using Thiccdal.Data.Models;
using RuntimeChatEvent = Thiccdal.Infrastructure.Bot.Models.ChatEvent;
using RuntimeFollowEvent = Thiccdal.Infrastructure.Bot.Models.TwitchFollowEvent;
using RuntimePlatformEvent = Thiccdal.Infrastructure.Bot.Models.PlatformEvent;
using RuntimeRaidEvent = Thiccdal.Infrastructure.Bot.Models.TwitchRaidEvent;
using RuntimeRedeemEvent = Thiccdal.Infrastructure.Bot.Models.TwitchRedeemEvent;
using RuntimeSubscribeEvent = Thiccdal.Infrastructure.Bot.Models.TwitchSubscribeEvent;

namespace Thiccdal.Data;

internal static class PlatformEventRecordFactory
{
    public static PlatformEvent Create(RuntimePlatformEvent platformEvent, PlatformUser? gifterPlatformUser)
    {
        return platformEvent switch
        {
            RuntimeSubscribeEvent subscribeEvent => new SubscribeEvent
            {
                Source = subscribeEvent.Source,
                Type = subscribeEvent.Type,
                SourceEventType = subscribeEvent.SourceEventType,
                ExternalId = subscribeEvent.ExternalId,
                Author = subscribeEvent.Author,
                Channel = subscribeEvent.Channel,
                Summary = subscribeEvent.Summary,
                RawData = subscribeEvent.RawData,
                OccurredAt = subscribeEvent.OccurredAt,
                Tier = subscribeEvent.Tier,
                IsGift = subscribeEvent.IsGift,
                GifterPlatformUser = gifterPlatformUser,
                GifterPlatformUserId = gifterPlatformUser?.Id
            },
            RuntimeFollowEvent followEvent => new FollowEvent
            {
                Source = followEvent.Source,
                Type = followEvent.Type,
                SourceEventType = followEvent.SourceEventType,
                ExternalId = followEvent.ExternalId,
                Author = followEvent.Author,
                Channel = followEvent.Channel,
                Summary = followEvent.Summary,
                RawData = followEvent.RawData,
                OccurredAt = followEvent.OccurredAt
            },
            RuntimeRedeemEvent redeemEvent => new RedeemEvent
            {
                Source = redeemEvent.Source,
                Type = redeemEvent.Type,
                SourceEventType = redeemEvent.SourceEventType,
                ExternalId = redeemEvent.ExternalId,
                Author = redeemEvent.Author,
                Channel = redeemEvent.Channel,
                Summary = redeemEvent.Summary,
                RawData = redeemEvent.RawData,
                OccurredAt = redeemEvent.OccurredAt,
                RewardId = redeemEvent.RewardId,
                RewardTitle = redeemEvent.RewardTitle,
                UserInput = string.IsNullOrWhiteSpace(redeemEvent.UserInput) ? null : redeemEvent.UserInput
            },
            RuntimeRaidEvent raidEvent => new RaidEvent
            {
                Source = raidEvent.Source,
                Type = raidEvent.Type,
                SourceEventType = raidEvent.SourceEventType,
                ExternalId = raidEvent.ExternalId,
                Author = raidEvent.Author,
                Channel = raidEvent.Channel,
                Summary = raidEvent.Summary,
                RawData = raidEvent.RawData,
                OccurredAt = raidEvent.OccurredAt,
                RaidingChannel = raidEvent.RaidingChannel,
                ViewerCount = raidEvent.ViewerCount
            },
            RuntimeChatEvent chatEvent => new PlatformEvent
            {
                Source = chatEvent.Source,
                Type = chatEvent.Type,
                SourceEventType = chatEvent.SourceEventType,
                ExternalId = chatEvent.ExternalId,
                Author = chatEvent.Author,
                Channel = chatEvent.Channel,
                Summary = chatEvent.Summary,
                Content = chatEvent.Content,
                HtmlContent = chatEvent.HtmlContent,
                RawData = chatEvent.RawData,
                OccurredAt = chatEvent.OccurredAt
            },
            _ => new PlatformEvent
            {
                Source = platformEvent.Source,
                Type = platformEvent.Type,
                SourceEventType = platformEvent.SourceEventType,
                ExternalId = platformEvent.ExternalId,
                Author = platformEvent.Author,
                Channel = platformEvent.Channel,
                Summary = platformEvent.Summary,
                RawData = platformEvent.RawData,
                OccurredAt = platformEvent.OccurredAt
            }
        };
    }
}
