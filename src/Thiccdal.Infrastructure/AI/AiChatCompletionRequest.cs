namespace Thiccdal.Infrastructure.AI;

/// <summary>
/// Represents a chat-completion request sent through Thiccdal's AI boundary.
/// </summary>
/// <param name="Model">The target model identifier.</param>
/// <param name="Messages">The ordered conversation messages.</param>
/// <param name="Temperature">The optional sampling temperature.</param>
/// <param name="MaxOutputTokenCount">The optional maximum output token count.</param>
public sealed record AiChatCompletionRequest(
    string Model,
    IReadOnlyList<AiChatMessage> Messages,
    double? Temperature = null,
    int? MaxOutputTokenCount = null);
