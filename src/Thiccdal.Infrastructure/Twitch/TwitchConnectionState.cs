namespace Thiccdal.Infrastructure.Twitch;

public enum TwitchConnectionState
{
    NotAuthorized,
    Authorized,
    Connecting,
    Connected,
    Disconnected,
    Error
}
