namespace Thiccdal.Remote.LMStudio;

/// <summary>
/// Holds named HTTP client identifiers for LM Studio integrations.
/// </summary>
public static class LmStudioClientNames
{
    /// <summary>
    /// Gets the named client used for reusable LM Studio requests.
    /// </summary>
    public const string Default = "LMStudio";

    /// <summary>
    /// Gets the named client used for question detection requests.
    /// </summary>
    public const string QuestionDetection = Default;
}
