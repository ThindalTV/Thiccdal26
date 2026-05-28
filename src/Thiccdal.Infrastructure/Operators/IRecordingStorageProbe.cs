namespace Thiccdal.Infrastructure.Operators;

/// <summary>
/// Evaluates recording-path availability and free-space health for the operator checklist.
/// </summary>
public interface IRecordingStorageProbe
{
    /// <summary>
    /// Gets the current recording storage status snapshot.
    /// </summary>
    /// <returns>The current recording-path and disk-space status.</returns>
    RecordingStorageStatus GetStatus();
}
