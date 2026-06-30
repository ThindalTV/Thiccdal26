using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Infrastructure.Twitch;
using Thiccdal.Remote.Twitch;

namespace Thiccdal.Remote.Twitch.Tests;

public class TwitchServiceTests
{
    private readonly TwitchOptions _options = new TwitchOptions
    {
        ClientId = "id",
        ClientSecret = "secret",
        RedirectUri = "https://localhost/callback"
    };

    private TwitchService BuildService()
    {
        return new TwitchService(
            Options.Create(_options),
            new Mock<ITwitchTokenManager>().Object,
            new Mock<ITwitchTargetChannelService>().Object,
            new Mock<ITwitchHelixClient>().Object,
            new Mock<ITwitchEventSubClient>().Object,
            new Mock<IEventBus>().Object,
            NullLogger<TwitchService>.Instance);
    }

    [Fact]
    public void WhenCreated_ThenConnectedIsFalse()
    {
        TwitchService service = BuildService();

        Assert.False(service.Connected);
    }

    [Fact]
    public async Task WhenNotConnected_ThenSendMessageDoesNotThrow()
    {
        TwitchService service = BuildService();

        Exception? exception = await Record.ExceptionAsync(() => service.SendMessage("hello"));

        Assert.Null(exception);
    }
}
