using Thiccdal.Infrastructure.Teleprompter;

namespace Thiccdal.Modules.Teleprompter.Services;

public class TeleprompterService : ITeleprompterService
{
    public event EventHandler<object, ScrollEventArgs>? OnScrollRequested;

    public void RequestScroll(object sender, ScrollDirection direction, int scrollAmount)
    {
        OnScrollRequested?.Invoke(sender, new ScrollEventArgs(sender, direction, scrollAmount));
    }
}
