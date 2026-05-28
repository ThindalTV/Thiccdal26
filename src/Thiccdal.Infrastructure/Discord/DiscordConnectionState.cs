namespace Thiccdal.Infrastructure.Discord;

/// <summary>
/// Represents the connection state of the Discord bot.
/// </summary>
public enum DiscordConnectionState
{
    /// <summary>Bot is not configured or not authorized.</summary>
    NotAuthorized = 0,

    /// <summary>Bot is attempting to connect to Discord gateway.</summary>
    Connecting = 1,

    /// <summary>Bot is connected and ready to receive/send messages.</summary>
    Connected = 2,

    /// <summary>Bot has been explicitly disconnected.</summary>
    Disconnected = 3,

    /// <summary>Bot encountered an error and may be attempting reconnection.</summary>
    Error = 4
}
