using System;
using System.Collections.Generic;
using System.Text;
using Thiccdal.Infrastructure.Teleprompter;

namespace Thiccdal.Modules.Teleprompter.Services;

public class TeleprompterService : ITeleprompterService
{
    public event EventHandler<ScrollEventArgs>? OnScrollRequested;

    public void RequestScroll(object sender, ScrollDirection direction, int scrollAmount)
    {
        OnScrollRequested?.Invoke(sender, new ScrollEventArgs(sender, direction, scrollAmount));
    }
}
