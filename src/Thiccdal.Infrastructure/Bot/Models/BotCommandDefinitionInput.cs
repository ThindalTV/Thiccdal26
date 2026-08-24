namespace Thiccdal.Infrastructure.Bot.Models;

/// <summary>
/// Represents operator-edited chatbot command fields before persistence.
/// </summary>
/// <param name="Trigger">The chat trigger.</param>
/// <param name="ResponseTemplate">The response template.</param>
/// <param name="HandlerType">The optional code-side handler type.</param>
/// <param name="IsEnabled">Whether the command should be enabled.</param>
/// <param name="SendInChat">Whether running the command sends its response to chat.</param>
/// <param name="ShowOnLowerThird">Whether the operator running the command puts copy on the lower third.</param>
/// <param name="LowerThirdTitle">The lower-third heading; falls back to the trigger when empty.</param>
/// <param name="LowerThirdText">The lower-third body copy; falls back to the chat response when empty.</param>
public sealed record BotCommandDefinitionInput(
    string Trigger,
    string ResponseTemplate,
    string? HandlerType,
    bool IsEnabled,
    bool SendInChat = true,
    bool ShowOnLowerThird = false,
    string? LowerThirdTitle = null,
    string? LowerThirdText = null);
