namespace Thiccdal.Infrastructure.Teleprompter;

public interface ITeleprompterService
{
    event EventHandler<object, ScrollEventArgs>? OnScrollRequested;

    void RequestScroll(object sender, ScrollDirection direction, int scrollAmount);
}
