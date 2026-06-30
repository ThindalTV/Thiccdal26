namespace Thiccdal.Infrastructure.Setup;

/// <summary>
/// Manages the state of the application setup wizard.
/// </summary>
public interface ISetupStateService
{
    /// <summary>
    /// Gets the current setup state.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The current <see cref="SetupState"/>.</returns>
    Task<SetupState> GetSetupState(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current step index in the setup wizard.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The zero-based index of the current setup step.</returns>
    Task<int> GetCurrentStepIndex(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the current step index in the setup wizard.
    /// </summary>
    /// <param name="stepIndex">The zero-based index of the step to set as current.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task SetCurrentStepIndex(int stepIndex, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the setup wizard as complete.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task MarkSetupComplete(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether the setup wizard has been completed.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><c>true</c> if setup is complete; otherwise, <c>false</c>.</returns>
    Task<bool> IsSetupComplete(CancellationToken cancellationToken = default);
}
