namespace Thiccdal.Infrastructure.Bot.Models;

/// <summary>
/// Represents a normalized portion of a rich chat message.
/// </summary>
public sealed record ChatMessagePart
{
    /// <summary>
    /// Gets the message part kind.
    /// </summary>
    public required ChatMessagePartType Type { get; init; }

    /// <summary>
    /// Gets the plain-text representation of the part.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Gets a remote asset URL when the part is backed by an image.
    /// </summary>
    public string AssetUrl { get; init; } = string.Empty;

    /// <summary>
    /// Gets a platform identifier for the part when available.
    /// </summary>
    public string ReferenceId { get; init; } = string.Empty;

    /// <summary>
    /// Gets a numeric amount when the part represents a quantity-based token such as bits.
    /// </summary>
    public int? Amount { get; init; }
}

/// <summary>
/// Identifies the kind of rich message part.
/// </summary>
public enum ChatMessagePartType
{
    Text = 1,
    Emote = 2,
    Mention = 3,
    Cheer = 4
}
