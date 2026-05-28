namespace Thiccdal.Infrastructure.Facebook;

public enum FacebookConnectionState
{
    NotAuthorized,
    Authorized,
    Connecting,
    Connected,
    Disconnected,
    Error
}
