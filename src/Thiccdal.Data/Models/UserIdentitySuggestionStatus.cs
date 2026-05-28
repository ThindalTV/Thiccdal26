namespace Thiccdal.Data.Models;

/// <summary>
/// Describes the review state for a potential cross-platform identity match.
/// </summary>
public enum UserIdentitySuggestionStatus
{
    /// <summary>
    /// The suggestion is waiting for operator review.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// The operator accepted the suggestion.
    /// </summary>
    Accepted = 1,

    /// <summary>
    /// The operator rejected the suggestion.
    /// </summary>
    Rejected = 2
}
