namespace Thiccdal.Infrastructure.Operators;

/// <summary>
/// Coordinates the non-visual pre-live checklist state consumed by the operator UI.
/// </summary>
public interface IPreLiveChecklistService
{
    /// <summary>
    /// Raised whenever the checklist snapshot changes.
    /// </summary>
    event EventHandler? StateChanged;

    /// <summary>
    /// Gets a value indicating whether every required item is satisfied.
    /// </summary>
    bool AllRequiredChecked { get; }

    /// <summary>
    /// Gets the number of required items that remain unchecked.
    /// </summary>
    int RequiredUncheckedCount { get; }

    /// <summary>
    /// Gets the number of optional items that remain unchecked.
    /// </summary>
    int OptionalUncheckedCount { get; }

    /// <summary>
    /// Gets the current checklist snapshot.
    /// </summary>
    /// <returns>The current checklist state.</returns>
    PreLiveChecklistState GetState();

    /// <summary>
    /// Sets the checked state for a manual or action checklist item.
    /// </summary>
    /// <param name="itemId">The checklist item identifier.</param>
    /// <param name="isChecked">The new checked state.</param>
    void SetItemChecked(string itemId, bool isChecked);

    /// <summary>
    /// Triggers the action flow for an action-backed checklist item.
    /// </summary>
    /// <param name="itemId">The checklist item identifier.</param>
    /// <param name="cancellationToken">The cancellation token for the action flow.</param>
    /// <returns>A task that completes when the action flow finishes.</returns>
    Task TriggerAction(string itemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reloads checklist definitions that come from persisted operator data.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token for the reload work.</param>
    /// <returns>A task that completes when reload finishes.</returns>
    Task Reload(CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets operator-controlled checklist state so the next pre-live session starts clean.
    /// </summary>
    void Reset();

    /// <summary>
    /// Completes the non-visual transition into live mode after the go-live action succeeds.
    /// </summary>
    /// <param name="startedAt">An optional live-start timestamp.</param>
    /// <param name="sessionId">An optional live session identifier for correlating persisted audit data.</param>
    void HandleGoLiveSucceeded(DateTimeOffset? startedAt = null, Guid? sessionId = null);
}
