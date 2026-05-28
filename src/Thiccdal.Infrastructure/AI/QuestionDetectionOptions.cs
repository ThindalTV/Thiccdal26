namespace Thiccdal.Infrastructure.AI;

/// <summary>
/// Configures model-backed question detection.
/// </summary>
public sealed class QuestionDetectionOptions
{
    /// <summary>
    /// Gets the configuration section name.
    /// </summary>
    public const string SectionName = "AI:QuestionDetection";

    /// <summary>
    /// Gets the required placeholder token for the message value.
    /// </summary>
    public const string MessagePlaceholder = "{{message}}";

    /// <summary>
    /// Gets or sets a value indicating whether model-backed question detection is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the model identifier used for question detection.
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the maximum output token count.
    /// </summary>
    public int MaxOutputTokenCount { get; set; } = 32;

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
