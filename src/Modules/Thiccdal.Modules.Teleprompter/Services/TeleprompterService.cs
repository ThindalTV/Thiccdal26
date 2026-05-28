using Thiccdal.Infrastructure.Operators;
using Thiccdal.Infrastructure.Teleprompter;

namespace Thiccdal.Modules.Teleprompter.Services;

public sealed class TeleprompterService : ITeleprompterService
{
    private readonly IOperatorStateService _operatorStateService;

    public TeleprompterService(IOperatorStateService operatorStateService)
    {
        _operatorStateService = operatorStateService;
    }

    public event EventHandler<object, ScrollEventArgs>? OnScrollRequested;

    public void RequestScroll(object sender, ScrollDirection direction, int scrollAmount)
    {
        _operatorStateService.ScrollTeleprompter(direction);
        OnScrollRequested?.Invoke(sender, new ScrollEventArgs(direction, scrollAmount));
    }
}
