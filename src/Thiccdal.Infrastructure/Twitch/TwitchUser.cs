namespace Thiccdal.Infrastructure.Twitch;

/// <summary>
/// Represents a Twitch user returned from the Helix API.
/// </summary>
public sealed record TwitchUser
{
    public required string Id { get; init; }
    public required string Login { get; init; }
    public required string DisplayName { get; init; }
}
