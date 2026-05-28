namespace Thiccdal.Data.Models;

/// <summary>
/// Stores a pending or reviewed recommendation to merge two platform users into one identity.
/// </summary>
public class UserIdentitySuggestion
{
    /// <summary>
    /// Gets or sets the database identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the lower platform user id in the suggested pair.
    /// </summary>
    public long FirstPlatformUserId { get; set; }

    /// <summary>
    /// Gets or sets the higher platform user id in the suggested pair.
    /// </summary>
    public long SecondPlatformUserId { get; set; }

    /// <summary>
    /// Gets or sets the normalized similarity score used to create the suggestion.
    /// </summary>
    public double SimilarityScore { get; set; }

    /// <summary>
    /// Gets or sets the review state.
    /// </summary>
    public UserIdentitySuggestionStatus Status { get; set; } = UserIdentitySuggestionStatus.Pending;

    /// <summary>
    /// Gets or sets when the suggestion was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the first platform user in the pair.
    /// </summary>
    public PlatformUser FirstPlatformUser { get; set; } = null!;

    /// <summary>
    /// Gets or sets the second platform user in the pair.
    /// </summary>
    public PlatformUser SecondPlatformUser { get; set; } = null!;
}
