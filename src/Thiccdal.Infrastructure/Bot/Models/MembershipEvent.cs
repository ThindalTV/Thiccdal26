namespace Thiccdal.Infrastructure.Bot.Models;

/// <summary>
/// Represents a YouTube channel membership event.
/// </summary>
public sealed record MembershipEvent : PlatformEvent
{
    /// <summary>
    /// Gets the membership level name when YouTube supplies one.
    /// </summary>
    public string LevelName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the membership month count for milestone events.
    /// </summary>
    public int? MonthCount { get; init; }

    /// <summary>
    /// Gets a value indicating whether this event represents a new member.
    /// </summary>
    public bool IsNewMember => MonthCount is null or 0;
}
