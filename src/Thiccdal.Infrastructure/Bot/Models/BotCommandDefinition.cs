namespace Thiccdal.Infrastructure.Bot.Models;

/// <summary>
/// Represents a chatbot command visible to the operator UI and dispatcher infrastructure.
/// </summary>
public sealed class BotCommandDefinition
{
    /// <summary>
    /// Initializes a new empty command definition.
    /// </summary>
    public BotCommandDefinition()
    {
    }

    /// <summary>
    /// Initializes a new command definition with all persisted values.
    /// </summary>
    /// <param name="id">The database identifier.</param>
    /// <param name="trigger">The chat trigger.</param>
    /// <param name="responseTemplate">The response template.</param>
    /// <param name="handlerType">The optional handler type.</param>
    /// <param name="isEnabled">Whether the command is enabled.</param>
    /// <param name="useCount">The persisted use count.</param>
    public BotCommandDefinition(long id, string trigger, string responseTemplate, string? handlerType, bool isEnabled, int useCount)
    {
        Id = id;
        Trigger = trigger;
        ResponseTemplate = responseTemplate;
        HandlerType = handlerType;
        IsEnabled = isEnabled;
        UseCount = useCount;
    }

    /// <summary>
    /// Gets or sets the database identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the chat trigger, including the leading exclamation mark.
    /// </summary>
    public string Trigger { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the response template sent to chat.
    /// </summary>
    public string ResponseTemplate { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional code-side handler type.
    /// </summary>
    public string? HandlerType { get; set; }

    /// <summary>
    /// Gets or sets whether the command is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets how many times the command has been used.
    /// </summary>
    public int UseCount { get; set; }
}
