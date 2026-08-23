namespace Thiccdal.Infrastructure.Readiness;

/// <summary>
/// Describes how much of the system has been configured, so operator surfaces can gate themselves.
/// </summary>
public sealed record SystemReadiness
{
    /// <summary>
    /// Gets a value indicating whether a Twitch target channel has been saved.
    /// </summary>
    public bool HasChannel { get; init; }

    /// <summary>
    /// Gets a value indicating whether a Twitch account has been authorized.
    /// </summary>
    public bool HasTwitchAuth { get; init; }

    /// <summary>
    /// Gets a value indicating whether the teleprompter has everything it needs to run.
    /// </summary>
    public bool IsPrompterReady => HasChannel;

    /// <summary>
    /// Gets a value indicating whether the streamer dashboard has everything it needs to run.
    /// </summary>
    public bool IsDashboardReady => HasChannel && HasTwitchAuth;
}
