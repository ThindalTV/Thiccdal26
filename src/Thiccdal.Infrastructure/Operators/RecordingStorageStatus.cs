namespace Thiccdal.Infrastructure.Operators;

/// <summary>
/// Captures the current checklist-relevant status of the recording output path and drive.
/// </summary>
/// <param name="IsPathConfigured">Whether the configured recording folder is available.</param>
/// <param name="PathWarningMessage">Optional operator guidance for the recording folder state.</param>
/// <param name="HasSufficientDiskSpace">Whether the recording drive currently has enough free space.</param>
/// <param name="DiskSpaceWarningMessage">Optional low-space guidance for the operator.</param>
public sealed record RecordingStorageStatus(
    bool IsPathConfigured,
    string? PathWarningMessage,
    bool HasSufficientDiskSpace,
    string? DiskSpaceWarningMessage);
