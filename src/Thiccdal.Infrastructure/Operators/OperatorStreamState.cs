namespace Thiccdal.Infrastructure.Operators;

/// <summary>
/// Represents the current operator-visible stream metadata snapshot.
/// </summary>
public sealed record OperatorStreamState
{
    /// <summary>
    /// Gets the live session identifier when the session originated from the local go-live flow.
    /// </summary>
    public Guid? SessionId { get; init; }

    /// <summary>
    /// Gets the live stream title.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Gets the active category or game name when known.
    /// </summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>
    /// Gets the active stream tags.
    /// </summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>
    /// Gets when the live session started when known.
    /// </summary>
    public DateTimeOffset? StartedAt { get; init; }
}
