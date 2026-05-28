namespace Thiccdal.Infrastructure.Bot.Models;

/// <summary>
/// Represents a YouTube Super Chat event.
/// </summary>
public sealed record SuperChatEvent : PlatformEvent
{
    /// <summary>
    /// Gets the paid amount in micros.
    /// </summary>
    public long AmountMicros { get; init; }

    /// <summary>
    /// Gets the ISO 4217 currency code.
    /// </summary>
    public string Currency { get; init; } = string.Empty;

    /// <summary>
    /// Gets the platform-formatted display string for the amount.
    /// </summary>
    public string DisplayString { get; init; } = string.Empty;

    /// <summary>
    /// Gets the sender comment when one was included.
    /// </summary>
    public string? UserComment { get; init; }
}
