using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Infrastructure.Twitch;
using Thiccdal.Remote.Twitch;

namespace Thiccdal.Remote.Twitch.Tests;

public class TwitchEventSubNotificationMapperTests
{
    [Fact]
    public void WhenChatNotificationContainsEmote_ThenMapsRichChatPartsAndHtml()
    {
        var mapper = CreateMapper(animatedEmotes: true);

        PlatformEvent platformEvent = mapper.Map(
            """
            {
              "metadata": {
                "message_id": "meta-1",
                "message_type": "notification",
                "message_timestamp": "2026-05-29T12:00:00Z",
                "subscription_type": "channel.chat.message"
              },
              "payload": {
                "event": {
                  "broadcaster_user_id": "12345",
                  "broadcaster_user_login": "thindal",
                  "chatter_user_id": "777",
                  "chatter_user_login": "viewer",
                  "chatter_user_name": "Viewer",
                  "message_id": "chat-1",
                  "color": "#8AE020",
                  "message": {
                    "text": "Hello Kappa",
                    "fragments": [
                      { "type": "text", "text": "Hello " },
                      {
                        "type": "emote",
                        "text": "Kappa",
                        "emote": {
                          "id": "25",
                          "format": ["animated", "static"]
                        }
                      }
                    ]
                  },
                  "badges": [
                    { "set_id": "subscriber", "id": "12", "info": "12" }
                  ]
                }
              }
            }
            """);

        ChatEvent chatEvent = Assert.IsType<ChatEvent>(platformEvent);
        Assert.Equal(PlatformEventType.ChatMessage, chatEvent.Type);
        Assert.Equal("channel.chat.message", chatEvent.SourceEventType);
        Assert.Equal("Hello Kappa", chatEvent.Content);
        Assert.Equal("#8AE020", chatEvent.Color);
        Assert.Equal(2, chatEvent.Parts.Count);
        Assert.Contains("emoticons/v2/25/animated", chatEvent.HtmlContent);
        Assert.Single(chatEvent.Badges);
    }

    [Fact]
    public void WhenFollowNotificationArrives_ThenMapsTwitchFollowEvent()
    {
        var mapper = CreateMapper();

        PlatformEvent platformEvent = mapper.Map(
            """
            {
              "metadata": {
                "message_id": "follow-meta",
                "message_type": "notification",
                "message_timestamp": "2026-05-29T12:00:00Z",
                "subscription_type": "channel.follow"
              },
              "payload": {
                "event": {
                  "user_id": "f1",
                  "user_login": "follower1",
                  "user_name": "FollowerOne",
                  "broadcaster_user_login": "thindal",
                  "followed_at": "2026-05-29T11:59:00Z"
                }
              }
            }
            """);

        TwitchFollowEvent followEvent = Assert.IsType<TwitchFollowEvent>(platformEvent);
        Assert.Equal("f1", followEvent.FollowerUserId);
        Assert.Equal("channel.follow", followEvent.SourceEventType);
        Assert.Equal("FollowerOne followed thindal", followEvent.Summary);
    }

    [Fact]
    public void WhenSubscribeNotificationArrives_ThenMapsTwitchSubscribeEvent()
    {
        var mapper = CreateMapper();

        PlatformEvent platformEvent = mapper.Map(
            """
            {
              "metadata": {
                "message_id": "sub-meta",
                "message_type": "notification",
                "message_timestamp": "2026-05-29T12:00:00Z",
                "subscription_type": "channel.subscribe"
              },
              "payload": {
                "event": {
                  "user_id": "s1",
                  "user_name": "Subber",
                  "broadcaster_user_login": "thindal",
                  "tier": "1000",
                  "is_gift": true,
                  "gifter_user_id": "g1",
                  "gifter_user_name": "GiftGiver",
                  "cumulative_months": 3
                }
              }
            }
            """);

        TwitchSubscribeEvent subscribeEvent = Assert.IsType<TwitchSubscribeEvent>(platformEvent);
        Assert.True(subscribeEvent.IsGift);
        Assert.Equal("1000", subscribeEvent.Tier);
        Assert.Equal("g1", subscribeEvent.GifterUserId);
        Assert.Contains("GiftGiver", subscribeEvent.Summary);
    }

    [Fact]
    public void WhenResubNotificationArrives_ThenMapsTwitchSubscribeEventWithMonths()
    {
        var mapper = CreateMapper();

        PlatformEvent platformEvent = mapper.Map(
            """
            {
              "metadata": {
                "message_id": "resub-meta",
                "message_type": "notification",
                "message_timestamp": "2026-05-29T12:00:00Z",
                "subscription_type": "channel.subscription.message"
              },
              "payload": {
                "event": {
                  "user_id": "s2",
                  "user_name": "Resubber",
                  "broadcaster_user_login": "thindal",
                  "tier": "2000",
                  "is_gift": false,
                  "cumulative_months": 8
                }
              }
            }
            """);

        TwitchSubscribeEvent subscribeEvent = Assert.IsType<TwitchSubscribeEvent>(platformEvent);
        Assert.False(subscribeEvent.IsGift);
        Assert.Equal("2000", subscribeEvent.Tier);
        Assert.Equal(8, subscribeEvent.CumulativeMonths);
        Assert.Contains("8", subscribeEvent.Summary);
    }

    [Fact]
    public void WhenCheerNotificationArrives_ThenMapsTwitchCheerEvent()
    {
        var mapper = CreateMapper();

        PlatformEvent platformEvent = mapper.Map(
            """
            {
              "metadata": {
                "message_id": "cheer-meta",
                "message_type": "notification",
                "message_timestamp": "2026-05-29T12:00:00Z",
                "subscription_type": "channel.cheer"
              },
              "payload": {
                "event": {
                  "user_id": "c1",
                  "user_name": "Cheerer",
                  "broadcaster_user_login": "thindal",
                  "bits": 500,
                  "message": "Let us go"
                }
              }
            }
            """);

        TwitchCheerEvent cheerEvent = Assert.IsType<TwitchCheerEvent>(platformEvent);
        Assert.Equal(500, cheerEvent.Bits);
        Assert.Contains("500", cheerEvent.Summary);
        Assert.Contains("Let us go", cheerEvent.Summary);
    }

    [Fact]
    public void WhenRaidNotificationArrives_ThenMapsTwitchRaidEvent()
    {
        var mapper = CreateMapper();

        PlatformEvent platformEvent = mapper.Map(
            """
            {
              "metadata": {
                "message_id": "raid-meta",
                "message_type": "notification",
                "message_timestamp": "2026-05-29T12:00:00Z",
                "subscription_type": "channel.raid"
              },
              "payload": {
                "event": {
                  "from_broadcaster_user_id": "r1",
                  "from_broadcaster_user_login": "raidboss",
                  "from_broadcaster_user_name": "RaidBoss",
                  "to_broadcaster_user_login": "thindal",
                  "viewers": 42
                }
              }
            }
            """);

        TwitchRaidEvent raidEvent = Assert.IsType<TwitchRaidEvent>(platformEvent);
        Assert.Equal("raidboss", raidEvent.RaidingChannel);
        Assert.Equal(42, raidEvent.ViewerCount);
    }

    [Fact]
    public void WhenRedeemNotificationArrives_ThenMapsTwitchRedeemEvent()
    {
        var mapper = CreateMapper();

        PlatformEvent platformEvent = mapper.Map(
            """
            {
              "metadata": {
                "message_id": "redeem-meta",
                "message_type": "notification",
                "message_timestamp": "2026-05-29T12:00:00Z",
                "subscription_type": "channel.channel_points_custom_reward_redemption.add"
              },
              "payload": {
                "event": {
                  "id": "reward-evt-1",
                  "user_id": "u1",
                  "user_name": "Redeemer",
                  "broadcaster_user_login": "thindal",
                  "user_input": "Play the siren",
                  "reward": {
                    "id": "reward-1",
                    "title": "Siren"
                  }
                }
              }
            }
            """);

        TwitchRedeemEvent redeemEvent = Assert.IsType<TwitchRedeemEvent>(platformEvent);
        Assert.Equal("reward-1", redeemEvent.RewardId);
        Assert.Equal("Siren", redeemEvent.RewardTitle);
        Assert.Equal("Play the siren", redeemEvent.UserInput);
    }

    [Fact]
    public void WhenNotificationTypeIsUnknown_ThenMapsRawEvent()
    {
        var mapper = CreateMapper();

        PlatformEvent platformEvent = mapper.Map(
            """
            {
              "metadata": {
                "message_id": "raw-meta",
                "message_type": "notification",
                "message_timestamp": "2026-05-29T12:00:00Z",
                "subscription_type": "channel.unknown"
              },
              "payload": {}
            }
            """);

        RawEvent rawEvent = Assert.IsType<RawEvent>(platformEvent);
        Assert.Equal(PlatformEventType.Raw, rawEvent.Type);
        Assert.Equal("channel.unknown", rawEvent.SourceEventType);
        Assert.Contains("channel.unknown", rawEvent.Summary);
    }

    [Fact]
    public void WhenBuildingEmoteUrl_ThenUsesDeterministicCdnPath()
    {
        string url = TwitchEmoteCdn.GetUrl("25", animated: true);

        Assert.Equal("https://static-cdn.jtvnw.net/emoticons/v2/25/animated/dark/1.0", url);
    }

    private static TwitchEventSubNotificationMapper CreateMapper(bool animatedEmotes = false)
    {
        return new TwitchEventSubNotificationMapper(
            Options.Create(new TwitchOptions
            {
                EventSub = new TwitchEventSubOptions
                {
                    UseAnimatedEmotes = animatedEmotes
                }
            }));
    }
}
