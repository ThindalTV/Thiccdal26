namespace Thiccdal.Infrastructure.Twitch;

/// <summary>
/// Target broadcaster/channel details that the authenticated bot account should connect to.
/// </summary>
/// <param name="TargetChannel">The Twitch login name for the channel the bot should join.</param>
/// <param name="BroadcasterId">The numeric Twitch user ID for the target broadcaster/channel owner.</param>
public sealed record TwitchTargetChannelSettings(string TargetChannel, string BroadcasterId);
