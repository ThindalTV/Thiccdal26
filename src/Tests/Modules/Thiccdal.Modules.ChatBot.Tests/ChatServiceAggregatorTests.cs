using Moq;
using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Modules.ChatBot.Services;

namespace Thiccdal.Modules.ChatBot.Tests;

public class ChatServiceAggregatorTests
{
    private static ChatEvent MakeChatEvent() => new ChatEvent
    {
        Author = "streamer",
        Channel = "#testchannel",
        Content = "hello chat",
        Source = PlatformEventSource.Twitch
    };

    [Fact]
    public void WhenSourceRaisesEvent_ThenAggregatorRaisesEvent()
    {
        var source = new Mock<IChatSource>();
        using var aggregator = new ChatServiceAggregator([source.Object]);
        ChatEvent? received = null;
        aggregator.OnChatMessageRecieved += (_, e) => received = e;

        source.Raise(s => s.OnChatMessageRecieved += null, this, MakeChatEvent());

        Assert.NotNull(received);
        Assert.Equal("hello chat", received.Content);
    }

    [Fact]
    public void WhenMultipleSourcesRaiseEvents_ThenAggregatorForwardsAll()
    {
        var source1 = new Mock<IChatSource>();
        var source2 = new Mock<IChatSource>();
        using var aggregator = new ChatServiceAggregator([source1.Object, source2.Object]);
        var received = new List<ChatEvent>();
        aggregator.OnChatMessageRecieved += (_, e) => received.Add(e);

        source1.Raise(s => s.OnChatMessageRecieved += null, this, MakeChatEvent());
        source2.Raise(s => s.OnChatMessageRecieved += null, this, MakeChatEvent());

        Assert.Equal(2, received.Count);
    }

    [Fact]
    public async Task WhenConnect_ThenAllDisconnectedSourcesAreConnected()
    {
        var source = new Mock<IChatSource>();
        source.Setup(s => s.Connected).Returns(false);
        using var aggregator = new ChatServiceAggregator([source.Object]);

        await aggregator.Connect(CancellationToken.None);

        source.Verify(s => s.Connect(It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task WhenAlreadyConnectedSource_ThenConnectSkipsIt()
    {
        var source = new Mock<IChatSource>();
        source.Setup(s => s.Connected).Returns(true);
        using var aggregator = new ChatServiceAggregator([source.Object]);

        await aggregator.Connect(CancellationToken.None);

        source.Verify(s => s.Connect(It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task WhenConnectCalledTwice_ThenSourceIsOnlyConnectedOnce()
    {
        var source = new Mock<IChatSource>();
        source.Setup(s => s.Connected).Returns(false);
        using var aggregator = new ChatServiceAggregator([source.Object]);

        await aggregator.Connect(CancellationToken.None);
        await aggregator.Connect(CancellationToken.None);

        source.Verify(s => s.Connect(It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task WhenDisconnect_ThenAllConnectedSourcesAreDisconnected()
    {
        var source = new Mock<IChatSource>();
        source.Setup(s => s.Connected).Returns(true);
        using var aggregator = new ChatServiceAggregator([source.Object]);

        await aggregator.Disconnect(CancellationToken.None);

        source.Verify(s => s.Disconnect(It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task WhenDisconnect_ThenDisconnectedSourcesAreSkipped()
    {
        var source = new Mock<IChatSource>();
        source.Setup(s => s.Connected).Returns(false);
        using var aggregator = new ChatServiceAggregator([source.Object]);

        await aggregator.Disconnect(CancellationToken.None);

        source.Verify(s => s.Disconnect(It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task WhenSendMessage_ThenAllConnectedSourcesReceiveIt()
    {
        var source = new Mock<IChatSource>();
        source.Setup(s => s.Connected).Returns(true);
        using var aggregator = new ChatServiceAggregator([source.Object]);

        await aggregator.SendMessage("hello", CancellationToken.None);

        source.Verify(s => s.SendMessage("hello", It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task WhenSendMessage_ThenDisconnectedSourcesAreSkipped()
    {
        var source = new Mock<IChatSource>();
        source.Setup(s => s.Connected).Returns(false);
        using var aggregator = new ChatServiceAggregator([source.Object]);

        await aggregator.SendMessage("hello", CancellationToken.None);

        source.Verify(s => s.SendMessage(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public void WhenDisposed_ThenSourceEventsNoLongerPropagate()
    {
        var source = new Mock<IChatSource>();
        var aggregator = new ChatServiceAggregator([source.Object]);
        int eventCount = 0;
        aggregator.OnChatMessageRecieved += (_, _) => eventCount++;

        aggregator.Dispose();
        source.Raise(s => s.OnChatMessageRecieved += null, this, MakeChatEvent());

        Assert.Equal(0, eventCount);
    }
}
