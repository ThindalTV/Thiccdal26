namespace Thiccdal.Infrastructure.Bot.Models;

/// <summary>
/// Represents a platform-agnostic user badge attached to a chat message.
/// </summary>
/// <param name="SetId">The badge family or group identifier.</param>
/// <param name="Version">The badge version within the family.</param>
/// <param name="Info">Optional badge detail provided by the platform.</param>
public sealed record ChatBadge(string SetId, string Version, string Info);
