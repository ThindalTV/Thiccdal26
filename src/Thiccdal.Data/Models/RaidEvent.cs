namespace Thiccdal.Data.Models;

/// <summary>
/// Represents a persisted raid event.
/// </summary>
public sealed class RaidEvent : PlatformEvent
{
    public string RaidingChannel { get; set; } = string.Empty;

    public int ViewerCount { get; set; }
}
