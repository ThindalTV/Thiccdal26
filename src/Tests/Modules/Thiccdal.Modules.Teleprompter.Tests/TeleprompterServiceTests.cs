using Moq;
using Thiccdal.Infrastructure.Operators;
using Thiccdal.Infrastructure.Teleprompter;
using Thiccdal.Modules.Teleprompter.Services;

namespace Thiccdal.Modules.Teleprompter.Tests;

public class TeleprompterServiceTests
{
    private static TeleprompterService CreateService()
    {
        var operatorStateService = new Mock<IOperatorStateService>();
        return new TeleprompterService(operatorStateService.Object);
    }

    [Fact]
    public void WhenScrollUp_ThenOnScrollRequestedIsRaised()
    {
        var service = CreateService();
        ScrollEventArgs? received = null;
        service.OnScrollRequested += (_, args) => received = args;

        service.RequestScroll(this, ScrollDirection.Up, 10);

        Assert.NotNull(received);
        Assert.Equal(ScrollDirection.Up, received.Direction);
    }

    [Fact]
    public void WhenScrollDown_ThenOnScrollRequestedIsRaised()
    {
        var service = CreateService();
        ScrollEventArgs? received = null;
        service.OnScrollRequested += (_, args) => received = args;

        service.RequestScroll(this, ScrollDirection.Down, 5);

        Assert.NotNull(received);
        Assert.Equal(ScrollDirection.Down, received.Direction);
    }

    [Fact]
    public void WhenScrollReset_ThenOnScrollRequestedIsRaised()
    {
        var service = CreateService();
        ScrollEventArgs? received = null;
        service.OnScrollRequested += (_, args) => received = args;

        service.RequestScroll(this, ScrollDirection.Reset, 0);

        Assert.NotNull(received);
        Assert.Equal(ScrollDirection.Reset, received.Direction);
    }

    [Theory]
    [InlineData(ScrollDirection.Up, 1)]
    [InlineData(ScrollDirection.Up, 100)]
    [InlineData(ScrollDirection.Down, 50)]
    public void WhenScrollRequested_ThenScrollAmountMatchesRequest(ScrollDirection direction, int amount)
    {
        var service = CreateService();
        ScrollEventArgs? received = null;
        service.OnScrollRequested += (_, args) => received = args;

        service.RequestScroll(this, direction, amount);

        Assert.Equal(amount, received!.ScrollAmount);
    }

    [Fact]
    public void WhenScrollRequested_ThenSenderIsPassedToEvent()
    {
        var service = CreateService();
        object? receivedSender = null;
        service.OnScrollRequested += (sender, _) => receivedSender = sender;

        service.RequestScroll(this, ScrollDirection.Up, 10);

        Assert.Same(this, receivedSender);
    }

    [Fact]
    public void WhenNoSubscribers_ThenRequestScrollDoesNotThrow()
    {
        var service = CreateService();

        var exception = Record.Exception(() => service.RequestScroll(this, ScrollDirection.Up, 10));

        Assert.Null(exception);
    }
}
