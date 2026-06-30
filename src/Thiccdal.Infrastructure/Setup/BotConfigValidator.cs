namespace Thiccdal.Infrastructure.Setup;

/// <summary>
/// Validates bot configuration settings.
/// </summary>
public static class BotConfigValidator
{
    /// <summary>
    /// The minimum allowed bot name length.
    /// </summary>
    public const int MinBotNameLength = 2;

    /// <summary>
    /// The maximum allowed bot name length.
    /// </summary>
    public const int MaxBotNameLength = 25;

    /// <summary>
    /// The minimum allowed timed message interval in minutes.
    /// </summary>
    public const int MinTimedMessageIntervalMinutes = 5;

    /// <summary>
    /// The maximum allowed timed message interval in minutes.
    /// </summary>
    public const int MaxTimedMessageIntervalMinutes = 60;

    /// <summary>
    /// Validates the bot name.
    /// </summary>
    /// <param name="botName">The bot name to validate.</param>
    /// <param name="errorMessage">Output error message if validation fails.</param>
    /// <returns>True if valid, false otherwise.</returns>
    public static bool ValidateBotName(string? botName, out string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(botName))
        {
            errorMessage = "Bot name is required.";
            return false;
        }

        if (botName.Length < MinBotNameLength)
        {
            errorMessage = $"Bot name must be at least {MinBotNameLength} characters.";
            return false;
        }

        if (botName.Length > MaxBotNameLength)
        {
            errorMessage = $"Bot name cannot exceed {MaxBotNameLength} characters.";
            return false;
        }

        if (botName.Contains(' '))
        {
            errorMessage = "Bot name cannot contain spaces.";
            return false;
        }

        errorMessage = null;
        return true;
    }

    /// <summary>
    /// Validates the timed message interval.
    /// </summary>
    /// <param name="intervalMinutes">The interval in minutes.</param>
    /// <param name="errorMessage">Output error message if validation fails.</param>
    /// <returns>True if valid, false otherwise.</returns>
    public static bool ValidateTimedMessageInterval(int intervalMinutes, out string? errorMessage)
    {
        if (intervalMinutes < MinTimedMessageIntervalMinutes)
        {
            errorMessage = $"Interval must be at least {MinTimedMessageIntervalMinutes} minutes.";
            return false;
        }

        if (intervalMinutes > MaxTimedMessageIntervalMinutes)
        {
            errorMessage = $"Interval cannot exceed {MaxTimedMessageIntervalMinutes} minutes.";
            return false;
        }

        errorMessage = null;
        return true;
    }

    /// <summary>
    /// Interpolates template placeholders in a message.
    /// </summary>
    /// <param name="template">The message template.</param>
    /// <param name="username">The username to substitute.</param>
    /// <param name="tier">The subscription tier to substitute (optional).</param>
    /// <returns>The interpolated message.</returns>
    public static string InterpolateTemplate(string template, string username, string? tier = null)
    {
        var result = template.Replace("{{username}}", username, StringComparison.OrdinalIgnoreCase);
        
        if (tier is not null)
        {
            result = result.Replace("{{tier}}", tier, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }
}
