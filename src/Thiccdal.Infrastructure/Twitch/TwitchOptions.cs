namespace Thiccdal.Infrastructure.Twitch;

public class TwitchOptions
{
    public const string SectionName = "Twitch";
    public const string DefaultOAuthBaseAddress = "https://id.twitch.tv/oauth2/";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string OAuthBaseAddress { get; set; } = DefaultOAuthBaseAddress;
    public TwitchHelixOptions Helix { get; set; } = new();
    public TwitchEventSubOptions EventSub { get; set; } = new();
    /// <summary>
    /// Gets or sets the scopes requested from the account that reads and writes chat.
    /// </summary>
    public List<string> BotScopes { get; set; } = new()
    {
        "user:read:chat",
        "user:write:chat",
        "user:bot"
    };

    /// <summary>
    /// Gets or sets the scopes requested from the channel owner. Twitch grants channel-level reads
    /// only to the broadcaster, so the bot account cannot subscribe to them on the channel's behalf.
    /// </summary>
    public List<string> BroadcasterScopes { get; set; } = new()
    {
        // Twitch requires every subscription on one WebSocket session to come from the same account,
        // so the broadcaster reads chat too whenever that account is connected.
        "user:read:chat",
        "channel:bot",
        "moderator:read:followers",
        "channel:read:subscriptions",
        "bits:read",
        // channel.raid EventSub carries no scope requirement, so none is requested for it.
        "channel:read:redemptions",
        "channel:manage:broadcast"
    };

}
