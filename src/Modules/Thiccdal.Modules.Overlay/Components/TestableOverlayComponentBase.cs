using Microsoft.AspNetCore.Components;
using Thiccdal.Infrastructure.Operators;
using Thiccdal.Infrastructure.Overlay;

namespace Thiccdal.Modules.Overlay.Components;

/// <summary>
/// Base class for testable overlay components. Implements test flash state and rendering.
/// </summary>
public abstract class TestableOverlayComponentBase : ComponentBase, ITestableOverlayComponent, IDisposable
{
    private IOperatorStateService? _operatorStateService;
    private CancellationTokenSource? _testFlashCancellationTokenSource;

    [Inject]
    protected IOperatorStateService OperatorStateService
    {
        get => _operatorStateService ?? throw new InvalidOperationException("OperatorStateService not injected");
        set => _operatorStateService = value;
    }

    /// <summary>
    /// Gets whether the test flash overlay is currently visible.
    /// </summary>
    protected bool IsTestFlashing { get; private set; }

    /// <summary>
    /// Gets the human-readable name of this component.
    /// </summary>
    public abstract string ComponentName { get; }

    /// <summary>
    /// Gets the Blazor component type for this overlay component.
    /// </summary>
    public Type ComponentType => GetType();

    protected string TestFlashLabel => $"■ TESTING — {ComponentName}";

    protected override void OnInitialized()
    {
        base.OnInitialized();
        OperatorStateService.OverlayTestTriggered += HandleOverlayTestTriggered;
    }

    private void HandleOverlayTestTriggered(object? sender, string componentName)
    {
        if (string.Equals(componentName, ComponentName, StringComparison.Ordinal))
        {
            _ = InvokeAsync(StartTriggeredFlash);
        }
    }

    public async Task TriggerTestFlash(CancellationToken cancellationToken)
    {
        IsTestFlashing = true;
        await InvokeAsync(StateHasChanged);

        await RunFlashDelay(cancellationToken);

        IsTestFlashing = false;
        await InvokeAsync(StateHasChanged);
    }

    public virtual void Dispose()
    {
        _testFlashCancellationTokenSource?.Cancel();
        _testFlashCancellationTokenSource?.Dispose();

        if (_operatorStateService != null)
        {
            _operatorStateService.OverlayTestTriggered -= HandleOverlayTestTriggered;
        }
    }

    private async Task StartTriggeredFlash()
    {
        _testFlashCancellationTokenSource?.Cancel();
        _testFlashCancellationTokenSource?.Dispose();
        _testFlashCancellationTokenSource = new CancellationTokenSource();
        await TriggerTestFlash(_testFlashCancellationTokenSource.Token);
    }

    private static async Task RunFlashDelay(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
        }
        catch (TaskCanceledException)
        {
        }
    }
}
