namespace Thiccdal.Infrastructure.Bot;

/// <summary>
/// Represents the sanitized, derived memory context available for a single chatter on one platform and channel.
/// </summary>
/// <param name="DisplayName">The latest public display name recorded for the chatter.</param>
/// <param name="LastInteractionAt">The last interaction time considered while building the memory summary.</param>
/// <param name="Facts">The bounded set of public, filtered facts that may be injected into an AI prompt.</param>
public sealed record ChatterMemoryContext(
    string DisplayName,
    DateTime LastInteractionAt,
    IReadOnlyList<string> Facts);
