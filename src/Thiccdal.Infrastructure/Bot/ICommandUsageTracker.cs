namespace Thiccdal.Infrastructure.Bot;

/// <summary>
/// Records command usage for the current app session and any backing persistence implementation.
/// </summary>
public interface ICommandUsageTracker
{
    /// <summary>
    /// Raised after a command invocation has been recorded, so live surfaces can refresh their counters.
    /// </summary>
    event EventHandler? UsageRecorded;

    /// <summary>
    /// Records a command invocation and returns the new in-session use count.
    /// </summary>
    /// <param name="trigger">The normalized command trigger.</param>
    /// <param name="cancellationToken">Cancels the update.</param>
    /// <returns>The command's current in-session use count after the invocation is recorded.</returns>
    Task<int> RecordUse(string trigger, CancellationToken cancellationToken = default);
}
