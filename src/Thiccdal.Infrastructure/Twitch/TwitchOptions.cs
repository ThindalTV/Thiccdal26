namespace Thiccdal.Infrastructure.Twitch;

public class TwitchOptions
{
    public string Channel { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;

    /// <summary>Twitch numeric user ID for the broadcaster's channel (required for EventSub).</summary>
    public string BroadcasterId { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
}