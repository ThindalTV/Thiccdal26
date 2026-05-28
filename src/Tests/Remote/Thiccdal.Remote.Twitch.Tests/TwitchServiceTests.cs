using Microsoft.Extensions.Options;
using Moq.AutoMock;
using Thiccdal.Infrastructure.Twitch;
using Thiccdal.Remote.Twitch;

namespace Thiccdal.Remote.Twitch.Tests;

public class TwitchServiceTests
{
    private readonly AutoMocker _mocker = new AutoMocker();
    private readonly TwitchOptions _options = new TwitchOptions
    {
        Channel = "testchannel",
        Username = "testbot",
        ClientId = "id",
        ClientSecret = "secret",
        RedirectUri = "https://localhost/callback"
    };

    private TwitchService BuildService()
    {
        _mocker.Use(Options.Create(_options));
        return _mocker.CreateInstance<TwitchService>();
    }

    [Fact]
    public void WhenCreated_ThenConnectedIsFalse()
    {
        var service = BuildService();

        Assert.False(service.Connected);
    }

    [Fact]
    public async Task WhenNotConnected_ThenSendMessageDoesNotThrow()
    {
        var service = BuildService();

        var exception = await Record.ExceptionAsync(() => service.SendMessage("hello"));

        Assert.Null(exception);
    }
}
