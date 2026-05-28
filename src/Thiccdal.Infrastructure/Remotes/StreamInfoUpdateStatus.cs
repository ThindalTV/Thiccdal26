namespace Thiccdal.Infrastructure.Remotes;

/// <summary>
/// Describes the outcome of a platform stream metadata update.
/// </summary>
public enum StreamInfoUpdateStatus
{
    /// <summary>
    /// The requested update completed successfully.
    /// </summary>
    Succeeded,

    /// <summary>
    /// Some supported fields were updated, but other requested fields were not supported.
    /// </summary>
    PartiallySucceeded,

    /// <summary>
    /// None of the requested fields could be updated because the platform does not support them.
    /// </summary>
    Unsupported,

    /// <summary>
    /// The update failed.
    /// </summary>
    Failed
}
