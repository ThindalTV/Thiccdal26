namespace Thiccdal.Infrastructure.Discord;

/// <summary>
/// Configuration options for the Discord platform integration.
/// </summary>
public class DiscordOptions
{
    public const string SectionName = "Discord";

    /// <summary>
    /// Gets or sets the Discord bot token.
    /// This token is obtained from the Discord Developer Portal when you create a bot application.
    /// The bot must have the MESSAGE CONTENT privileged intent enabled in the Discord Developer Portal.
    /// </summary>
    public string BotToken { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Discord guild (server) ID where the bot will operate.
    /// This is the numeric snowflake ID of your Discord server.
    /// </summary>
    public string GuildId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the text channel ID where the bot will read and send chat messages.
    /// This is the numeric snowflake ID of the text channel used for stream chat.
    /// </summary>
    public string StreamChannelId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the voice/stage channel ID where the bot will relay the stream.
    /// This is the numeric snowflake ID of the voice or stage channel.
    /// Leave empty if you don't want to relay the stream to a Discord voice channel.
    /// </summary>
    public string VoiceChannelId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the delay in seconds before attempting to reconnect after a disconnect.
    /// </summary>
    public int ReconnectDelaySeconds { get; set; } = 5;
}
