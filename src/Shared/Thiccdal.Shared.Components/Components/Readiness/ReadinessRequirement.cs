namespace Thiccdal.Shared.Components.Components.Readiness;

/// <summary>
/// Identifies what a gated operator surface needs before it activates.
/// </summary>
public enum ReadinessRequirement
{
    /// <summary>
    /// A Twitch target channel has been saved.
    /// </summary>
    ChannelConfigured,

    /// <summary>
    /// A Twitch target channel has been saved and a Twitch account is authorized.
    /// </summary>
    TwitchAuthorized
}
