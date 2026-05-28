namespace Thiccdal.Infrastructure.Discord;

/// <summary>
/// Centralizes the current Discord relay support decision so operators and code paths stay consistent.
/// </summary>
public static class DiscordRelaySupport
{
    public const string BlockedReason =
        "Discord voice-channel RTMP relay is blocked. Discord bots can join voice channels for audio, but the official Discord API and Discord.Net do not provide a production-safe way for a bot to originate Discord Go Live video from Thiccdal's RTMP ingest.";

    public static DiscordRelayStatus BlockedStatus { get; } = new DiscordRelayStatus(false, BlockedReason);
}
