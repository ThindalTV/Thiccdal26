namespace Thiccdal.Infrastructure.YouTube;

/// <summary>
/// Represents the current authorization and connection state for YouTube integration.
/// </summary>
public enum YouTubeConnectionState
{
    /// <summary>No OAuth token is stored.</summary>
    NotAuthorized = 0,

    /// <summary>OAuth token is stored but not yet connected to live chat.</summary>
    Authorized = 1,

    /// <summary>Currently establishing connection to YouTube live chat API.</summary>
    Connecting = 2,

    /// <summary>Actively polling YouTube live chat.</summary>
    Connected = 3,

    /// <summary>Intentionally disconnected but token remains valid.</summary>
    Disconnected = 4,

    /// <summary>An error occurred during connection or polling.</summary>
    Error = 5
}
