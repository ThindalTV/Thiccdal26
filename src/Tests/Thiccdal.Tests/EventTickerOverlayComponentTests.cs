using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Modules.Overlay.Components;

namespace Thiccdal.Tests;

public sealed class EventTickerOverlayComponentTests
{
    [Fact]
    public void WhenSubscribeEventIsNotGift_ThenTickerTextMatchesContract()
    {
        TwitchSubscribeEvent platformEvent = new()
        {
            Source = PlatformEventSource.Twitch,
            Type = PlatformEventType.Subscribe,
            Author = "ViewerOne",
            Channel = "ThindalTV",
            Tier = "Tier 1"
        };

        string? tickerText = EventTickerOverlayComponent.CreateTickerText(platformEvent);

        Assert.Equal("🎉 ViewerOne just subscribed! (Tier 1)", tickerText);
    }

    [Fact]
    public void WhenSubscribeEventIsGift_ThenTickerTextMatchesContract()
    {
        TwitchSubscribeEvent platformEvent = new()
        {
            Source = PlatformEventSource.Twitch,
            Type = PlatformEventType.Subscribe,
            Author = "LuckyViewer",
            Channel = "ThindalTV",
            IsGift = true,
            Summary = "LuckyViewer received a gifted Tier Tier 1 sub from GiftHero"
        };

        string? tickerText = EventTickerOverlayComponent.CreateTickerText(platformEvent);

        Assert.Equal("🎁 GiftHero gifted a sub to LuckyViewer!", tickerText);
    }

    [Fact]
    public void WhenFollowEventArrives_ThenTickerTextMatchesContract()
    {
        TwitchFollowEvent platformEvent = new()
        {
            Source = PlatformEventSource.Twitch,
            Type = PlatformEventType.Follow,
            Author = "FreshFollower",
            Channel = "ThindalTV"
        };

        string? tickerText = EventTickerOverlayComponent.CreateTickerText(platformEvent);

        Assert.Equal("❤️ FreshFollower followed!", tickerText);
    }

    [Fact]
    public void WhenRedeemEventArrives_ThenTickerTextMatchesContract()
    {
        TwitchRedeemEvent platformEvent = new()
        {
            Source = PlatformEventSource.Twitch,
            Type = PlatformEventType.Redeem,
            Author = "RewardFan",
            Channel = "ThindalTV",
            RewardTitle = "Hydrate"
        };

        string? tickerText = EventTickerOverlayComponent.CreateTickerText(platformEvent);

        Assert.Equal("⭐ RewardFan redeemed Hydrate", tickerText);
    }

    [Fact]
    public void WhenRaidEventArrives_ThenTickerTextMatchesContract()
    {
        TwitchRaidEvent platformEvent = new()
        {
            Source = PlatformEventSource.Twitch,
            Type = PlatformEventType.Raid,
            Author = "RaidLeader",
            Channel = "ThindalTV",
            RaidingChannel = "RaidLeader",
            ViewerCount = 42
        };

        string? tickerText = EventTickerOverlayComponent.CreateTickerText(platformEvent);

        Assert.Equal("⚔️ RaidLeader is raiding with 42 viewers!", tickerText);
    }

    [Fact]
    public void WhenSuperChatEventArrives_ThenTickerTextMatchesContract()
    {
        PlatformEvent platformEvent = new()
        {
            Source = PlatformEventSource.YouTube,
            Type = PlatformEventType.SuperChat,
            Author = "GeneroUser",
            Channel = "ThindalTV",
            Summary = "GeneroUser sent 5.00 USD: Great stream!"
        };

        string? tickerText = EventTickerOverlayComponent.CreateTickerText(platformEvent);

        Assert.Equal("💰 GeneroUser sent 5.00 USD!", tickerText);
    }

    [Fact]
    public void WhenMembershipEventArrives_ThenTickerTextMatchesContract()
    {
        PlatformEvent platformEvent = new()
        {
            Source = PlatformEventSource.YouTube,
            Type = PlatformEventType.Membership,
            Author = "NewMember",
            Channel = "ThindalTV",
            Summary = "NewMember became a member"
        };

        string? tickerText = EventTickerOverlayComponent.CreateTickerText(platformEvent);

        Assert.Equal("🌟 NewMember became a member!", tickerText);
    }
}
