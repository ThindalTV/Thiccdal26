using Thiccdal.Infrastructure.Bot;
using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Modules.ChatBot.Services;

namespace Thiccdal.Tests;

public sealed class TokenInterpolatorTests
{
    [Theory]
    [InlineData("{user}", "Alice", "Alice")]
    [InlineData("{USER}", "Alice", "Alice")]
    [InlineData("{User}", "Bob", "Bob")]
    public void WhenUserToken_ThenReplacedWithDisplayName(string template, string name, string expected)
    {
        TokenInterpolator interpolator = CreateInterpolator();

        string rendered = interpolator.Interpolate(template, CreateContext(userDisplayName: name));

        Assert.Equal(expected, rendered);
    }

    [Theory]
    [InlineData("{platform}", "Twitch", "Twitch")]
    [InlineData("{PLATFORM}", "Null", "Null")]
    public void WhenPlatformToken_ThenReplacedWithPlatformName(string template, string platform, string expected)
    {
        TokenInterpolator interpolator = CreateInterpolator();

        string rendered = interpolator.Interpolate(template, CreateContext(platform: platform));

        Assert.Equal(expected, rendered);
    }

    [Theory]
    [InlineData("{count}", 1, "1")]
    [InlineData("{count}", 42, "42")]
    public void WhenCountToken_ThenReplacedWithUseCount(string template, int useCount, string expected)
    {
        TokenInterpolator interpolator = CreateInterpolator();

        string rendered = interpolator.Interpolate(template, CreateContext(useCount: useCount));

        Assert.Equal(expected, rendered);
    }

    [Fact]
    public void WhenUptimeLessThanOneHour_ThenFormattedAsMmSs()
    {
        TokenInterpolator interpolator = CreateInterpolator(new DateTimeOffset(2026, 05, 29, 12, 30, 45, TimeSpan.Zero));

        string rendered = interpolator.Interpolate(
            "{uptime}",
            CreateContext(streamStartedAt: new DateTimeOffset(2026, 05, 29, 12, 12, 15, TimeSpan.Zero)));

        Assert.Equal("18m 30s", rendered);
    }

    [Fact]
    public void WhenUptimeOneHourOrMore_ThenFormattedAsHhMm()
    {
        TokenInterpolator interpolator = CreateInterpolator(new DateTimeOffset(2026, 05, 29, 12, 30, 45, TimeSpan.Zero));

        string rendered = interpolator.Interpolate(
            "{uptime}",
            CreateContext(streamStartedAt: new DateTimeOffset(2026, 05, 29, 10, 00, 45, TimeSpan.Zero)));

        Assert.Equal("2h 30m", rendered);
    }

    [Fact]
    public void WhenUnknownToken_ThenLeftAsIs()
    {
        TokenInterpolator interpolator = CreateInterpolator();

        string rendered = interpolator.Interpolate("{unknown}", CreateContext());

        Assert.Equal("{unknown}", rendered);
    }

    [Fact]
    public void WhenMultipleTokens_ThenAllReplaced()
    {
        TokenInterpolator interpolator = CreateInterpolator(new DateTimeOffset(2026, 05, 29, 12, 30, 45, TimeSpan.Zero));

        string rendered = interpolator.Interpolate(
            "Hi {user} from {platform}; count={count}; uptime={uptime}",
            CreateContext(
                userDisplayName: "Alice",
                platform: "Twitch",
                useCount: 7,
                streamStartedAt: new DateTimeOffset(2026, 05, 29, 11, 00, 45, TimeSpan.Zero)));

        Assert.Equal("Hi Alice from Twitch; count=7; uptime=1h 30m", rendered);
    }

    [Fact]
    public void WhenNoTokens_ThenTemplateReturnedUnchanged()
    {
        TokenInterpolator interpolator = CreateInterpolator();

        string rendered = interpolator.Interpolate("No tokens here", CreateContext());

        Assert.Equal("No tokens here", rendered);
    }

    [Fact]
    public void WhenStreamHasNotStarted_ThenUptimeTokenResolvesToOffline()
    {
        TokenInterpolator interpolator = CreateInterpolator();

        string rendered = interpolator.Interpolate("{uptime}", CreateContext(streamStartedAt: null, useDefaultStartTime: false));

        Assert.Equal("offline", rendered);
    }

    private static TokenInterpolator CreateInterpolator(DateTimeOffset? utcNow = null)
    {
        return new TokenInterpolator(new FixedTimeProvider(utcNow ?? new DateTimeOffset(2026, 05, 29, 12, 00, 00, TimeSpan.Zero)));
    }

    private static CommandContext CreateContext(
        string userDisplayName = "Mal",
        string platform = "Twitch",
        int useCount = 3,
        DateTimeOffset? streamStartedAt = null,
        bool useDefaultStartTime = true)
    {
        return new CommandContext
        {
            Trigger = "!hello",
            Args = [],
            UserDisplayName = userDisplayName,
            Platform = platform,
            SourcePlatform = Enum.Parse<PlatformEventSource>(platform, ignoreCase: true),
            UseCount = useCount,
            StreamStartedAt = streamStartedAt ?? (useDefaultStartTime
                ? new DateTimeOffset(2026, 05, 29, 11, 00, 00, TimeSpan.Zero)
                : null)
        };
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }
    }
}
