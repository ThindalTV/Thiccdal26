namespace Thiccdal.Infrastructure.Operators;

/// <summary>
/// Orchestrates the operator go-live workflow across streaming, fanout, and shared operator state.
/// </summary>
public interface IGoLiveActionService
{
    /// <summary>
    /// Raised whenever the go-live action state changes.
    /// </summary>
    event EventHandler? StateChanged;

    /// <summary>
    /// Gets the current go-live action state.
    /// </summary>
    /// <returns>The current state snapshot.</returns>
    GoLiveActionState GetState();

    /// <summary>
    /// Executes the go-live workflow.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>A task that completes when the workflow finishes.</returns>
    Task Execute(CancellationToken cancellationToken = default);
}
