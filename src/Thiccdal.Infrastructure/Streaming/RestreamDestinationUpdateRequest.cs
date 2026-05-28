namespace Thiccdal.Infrastructure.Streaming;

/// <summary>
/// Operator request for enabling or disabling a restream destination.
/// </summary>
public sealed record RestreamDestinationUpdateRequest
{
    /// <summary>
    /// Gets the platform display name to update.
    /// </summary>
    public required string PlatformName { get; init; }

    /// <summary>
    /// Gets a value indicating whether the destination should participate in fanout.
    /// </summary>
    public bool IsEnabled { get; init; }
}
