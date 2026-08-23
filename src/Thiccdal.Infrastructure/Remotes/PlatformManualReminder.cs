namespace Thiccdal.Infrastructure.Remotes;

/// <summary>
/// Represents a manual platform setting that must be configured in the platform's web dashboard before going live.
/// These settings cannot be controlled via API.
/// </summary>
public record PlatformManualReminder
{
    /// <summary>
    /// Gets the platform name (e.g., "Twitch").
    /// </summary>
    public string Platform { get; init; } = string.Empty;

    /// <summary>
    /// Gets the setting name (e.g., "Stream encoding", "Made for Kids").
    /// </summary>
    public string Setting { get; init; } = string.Empty;

    /// <summary>
    /// Gets the reminder text shown to the operator.
    /// </summary>
    public string ReminderText { get; init; } = string.Empty;
}
