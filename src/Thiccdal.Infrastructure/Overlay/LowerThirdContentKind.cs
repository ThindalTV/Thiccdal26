namespace Thiccdal.Infrastructure.Overlay;

/// <summary>
/// Identifies what put the current content on the lower third.
/// </summary>
public enum LowerThirdContentKind
{
    /// <summary>
    /// A viewer question promoted from the question queue.
    /// </summary>
    Question,

    /// <summary>
    /// Operator copy pushed by a bot command or a predefined overlay card.
    /// </summary>
    Message
}
