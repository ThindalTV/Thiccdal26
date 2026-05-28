namespace Thiccdal.Infrastructure.Twitch;

public class TwitchHelixOptions
{
    public const string DefaultBaseAddress = "https://api.twitch.tv/helix/";

    public string BaseAddress { get; set; } = DefaultBaseAddress;

    public int StreamStateRefreshSeconds { get; set; } = 30;

    public bool SendChatMessagesViaHelix { get; set; } = true;
}
