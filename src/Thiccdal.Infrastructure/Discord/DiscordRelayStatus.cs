namespace Thiccdal.Infrastructure.Discord;

/// <summary>
/// Describes whether Discord relay support is currently available.
/// </summary>
public sealed record DiscordRelayStatus(bool IsSupported, string StatusMessage);
