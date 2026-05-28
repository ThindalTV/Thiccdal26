namespace Thiccdal.Infrastructure.Twitch;

/// <summary>
/// Resolves and persists the Twitch target channel that the bot should connect to.
/// Keeps the authenticated bot identity separate from the broadcaster/channel owner.
/// </summary>
public interface ITwitchTargetChannelService
{
    /// <summary>
    /// Raised when the resolved Twitch chat connection profile changes.
    /// </summary>
    event EventHandler<TwitchChatConnectionProfile>? ConnectionProfileChanged;

    /// <summary>
    /// Returns the current Twitch chat connection profile.
    /// </summary>
    Task<TwitchChatConnectionProfile> GetConnectionProfile(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the Twitch target channel details that the bot should connect to.
    /// </summary>
    Task<TwitchChatConnectionProfile> UpdateTargetChannel(TwitchTargetChannelSettings targetChannel, CancellationToken cancellationToken = default);
}
