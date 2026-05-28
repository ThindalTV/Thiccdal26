namespace Thiccdal.Infrastructure.Remotes;

/// <summary>
/// Configures heuristic identity suggestion generation.
/// </summary>
public sealed class UserIdentityOptions
{
    /// <summary>
    /// Gets the configuration section name.
    /// </summary>
    public const string SectionName = "UserIdentity";

    /// <summary>
    /// Gets or sets the minimum normalized similarity score required to create a suggestion.
    /// </summary>
    public double SimilarityThreshold { get; set; } = 0.85d;
}
