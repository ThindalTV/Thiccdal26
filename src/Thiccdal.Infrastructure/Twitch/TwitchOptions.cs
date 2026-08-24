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
    public List<string> Scopes { get; set; } = new()
    {
        "user:read:chat",
        "user:write:chat",
        "user:bot",
        "channel:bot",
        "moderator:read:followers",
        "channel:read:subscriptions",
        "bits:read",
        // channel.raid EventSub carries no scope requirement, so none is requested for it.
        "channel:read:redemptions"
    };

}
