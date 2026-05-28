namespace Thiccdal.Infrastructure.Remotes;

/// <summary>
/// Represents a pre-live stream metadata update request from the operator UI.
/// </summary>
public sealed record StreamInfoUpdateRequest
{
    /// <summary>
    /// Gets the requested stream title.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Gets the requested stream category or game name.
    /// </summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>
    /// Gets the requested stream tags.
    /// </summary>
    public IReadOnlyList<string> Tags { get; init; } = [];
}
