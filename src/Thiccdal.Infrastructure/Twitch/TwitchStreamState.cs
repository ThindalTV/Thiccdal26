namespace Thiccdal.Infrastructure.Twitch;

public sealed record TwitchStreamState
{
    public bool IsLive { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Category { get; init; } = string.Empty;

    public IReadOnlyList<string> Tags { get; init; } = [];

    public DateTimeOffset? StartedAt { get; init; }
}
