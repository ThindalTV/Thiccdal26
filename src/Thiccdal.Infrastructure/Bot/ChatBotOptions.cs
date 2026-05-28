namespace Thiccdal.Infrastructure.Bot;

/// <summary>
/// Configures chatbot behavior.
/// </summary>
public sealed class ChatBotOptions
{
    /// <summary>
    /// Gets the configuration section name.
    /// </summary>
    public const string SectionName = "ChatBot";

    /// <summary>
    /// Gets or sets whether inbound chat questions should automatically enter the shared queue.
    /// </summary>
    public bool AutoQueueQuestions { get; set; } = true;

    /// <summary>
    /// Gets or sets the configured public bot name that viewers must mention.
    /// </summary>
    public string BotName { get; set; } = "Thiccdal";

    /// <summary>
    /// Gets or sets the mention-gated AI responder settings.
    /// </summary>
    public ChatBotAiResponderOptions AiResponder { get; set; } = new();
}
