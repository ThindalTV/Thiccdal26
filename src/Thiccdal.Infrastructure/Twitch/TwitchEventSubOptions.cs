namespace Thiccdal.Infrastructure.Twitch;

public class TwitchEventSubOptions
{
    public const string DefaultWebSocketUrl = "wss://eventsub.wss.twitch.tv/ws";

    public string WebSocketUrl { get; set; } = DefaultWebSocketUrl;

    public int ReconnectDelaySeconds { get; set; } = 5;

    public bool RequireModeratorAccess { get; set; } = true;

    public bool UseAnimatedEmotes { get; set; } = true;
}
