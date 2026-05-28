namespace Thiccdal.Infrastructure.Remotes;

/// <summary>
/// Represents the normalized public connection state for a platform adapter.
/// </summary>
public enum PlatformConnectionState
{
    Connected,
    Connecting,
    Disconnected,
    Error,
    PendingApproval,
    Disabled
}
