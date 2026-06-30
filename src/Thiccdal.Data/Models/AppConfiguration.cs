namespace Thiccdal.Data.Models;

/// <summary>
/// Stores application configuration key-value pairs persisted to the database.
/// Used by the installation wizard and runtime configuration.
/// </summary>
public sealed class AppConfiguration
{
    /// <summary>
    /// Gets or sets the primary key.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the configuration key (e.g., "SetupComplete", "BotName").
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the configuration value as a string.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when this configuration was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
