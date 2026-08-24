namespace Thiccdal.Infrastructure.Overlay;

/// <summary>
/// Owns the single lower-third slot shared by promoted questions, bot commands, and overlay cards.
/// </summary>
public interface ILowerThirdService
{
    /// <summary>
    /// Raised whenever the lower-third content changes.
    /// </summary>
    event EventHandler? StateChanged;

    /// <summary>
    /// Gets the content currently on screen, or <c>null</c> when the lower third is clear.
    /// </summary>
    LowerThirdContent? GetCurrent();

    /// <summary>
    /// Puts operator copy on the lower third, replacing whatever is on screen.
    /// </summary>
    /// <param name="eyebrow">The small line above the body copy.</param>
    /// <param name="text">The body copy.</param>
    /// <param name="accent">An optional accent colour used for styling.</param>
    void ShowMessage(string eyebrow, string text, string? accent = null);

    /// <summary>
    /// Clears the lower third, including any promoted question.
    /// </summary>
    void Clear();
}
