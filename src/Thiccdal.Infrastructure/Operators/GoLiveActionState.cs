namespace Thiccdal.Infrastructure.Operators;

/// <summary>
/// Represents the non-visual state of the operator go-live action.
/// </summary>
public sealed record GoLiveActionState
{
    /// <summary>
    /// Gets a value indicating whether the go-live sequence is currently running.
    /// </summary>
    public bool IsRunning { get; init; }

    /// <summary>
    /// Gets the most recent operator-visible error, if any.
    /// </summary>
    public string? ErrorMessage { get; init; }
}
