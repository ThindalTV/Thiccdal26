namespace Thiccdal.Infrastructure.Overlay;

/// <summary>
/// Extension of <see cref="IOverlayComponent"/> that adds a test flash capability.
/// Components implementing this interface can be triggered from the Pre-Live Checklist overlay verification section.
/// </summary>
public interface ITestableOverlayComponent : IOverlayComponent
{
    /// <summary>
    /// Triggers the 3-second test flash on this component.
    /// Called from IOperatorStateService so all connected sessions fire simultaneously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to honour for early dismissal.</param>
    /// <returns>A task that completes when the test flash completes or is cancelled.</returns>
    Task TriggerTestFlash(CancellationToken cancellationToken);
}
