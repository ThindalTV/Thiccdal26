namespace Thiccdal.Infrastructure.Teleprompter;

public interface ITeleprompterService
{
    event EventHandler<ScrollEventArgs>? OnScrollRequested;

    void RequestScroll(object sender, ScrollDirection direction, int scrollAmount);
}
