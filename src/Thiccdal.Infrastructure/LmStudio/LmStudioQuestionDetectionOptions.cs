namespace Thiccdal.Infrastructure.LmStudio;

/// <summary>
/// Configures LM Studio-backed chat question detection.
/// </summary>
public sealed class LmStudioQuestionDetectionOptions
{
    /// <summary>
    /// Gets the configuration section name.
    /// </summary>
    public const string SectionName = "LMStudio:QuestionDetection";

    /// <summary>
    /// Gets the required placeholder token for the message value.
    /// </summary>
    public const string MessagePlaceholder = "{{message}}";

    /// <summary>
    /// Gets or sets a value indicating whether model-backed question detection is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the LM Studio model identifier used for question detection.
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the maximum completion token count.
    /// </summary>
    public int MaxTokens { get; set; } = 32;

    /// <summary>
    /// Gets or sets the system prompt sent before each classification request.
    /// </summary>
    public string SystemPrompt { get; set; } =
        "You classify livestream chat for an on-air question queue. Return only JSON with properties isQuestion (boolean) and questionText (string). Only mark genuine viewer questions directed at the streamer as true. Preserve the viewer wording in questionText when true. Return an empty string when false.";

    /// <summary>
    /// Gets or sets the prompt template sent with chat metadata.
    /// </summary>
    public string UserPromptTemplate { get; set; } =
        "Chat message:\n"
        + MessagePlaceholder
        + "\n\nShould this be added to the viewer question queue? Return JSON only.";

    /// <summary>
    /// Gets or sets the completion temperature.
    /// </summary>
    public double Temperature { get; set; } = 0.1d;
}
