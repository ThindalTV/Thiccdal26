namespace Thiccdal.Infrastructure.Bot.Models;

/// <summary>
/// Represents operator-edited chatbot command fields before persistence.
/// </summary>
/// <param name="Trigger">The chat trigger.</param>
/// <param name="ResponseTemplate">The response template.</param>
/// <param name="HandlerType">The optional code-side handler type.</param>
/// <param name="IsEnabled">Whether the command should be enabled.</param>
public sealed record BotCommandDefinitionInput(
    string Trigger,
    string ResponseTemplate,
    string? HandlerType,
    bool IsEnabled);
