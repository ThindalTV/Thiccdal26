namespace Thiccdal.Infrastructure.Setup;

/// <summary>
/// Validates AI configuration settings.
/// </summary>
public static class AiConfigValidator
{
    /// <summary>
    /// The minimum allowed request timeout in seconds.
    /// </summary>
    public const int MinTimeoutSeconds = 5;

    /// <summary>
    /// The maximum allowed request timeout in seconds.
    /// </summary>
    public const int MaxTimeoutSeconds = 300;

    /// <summary>
    /// Validates an AI endpoint URL.
    /// </summary>
    /// <param name="endpoint">The endpoint URL to validate.</param>
    /// <param name="errorMessage">Output error message if validation fails.</param>
    /// <returns>True if valid, false otherwise.</returns>
    public static bool ValidateEndpoint(string? endpoint, out string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            errorMessage = "Endpoint URL is required.";
            return false;
        }

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            errorMessage = "Endpoint must be a valid URL.";
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            errorMessage = "Endpoint must use HTTP or HTTPS.";
            return false;
        }

        errorMessage = null;
        return true;
    }

    /// <summary>
    /// Validates the request timeout value.
    /// </summary>
    /// <param name="timeoutSeconds">The timeout in seconds.</param>
    /// <param name="errorMessage">Output error message if validation fails.</param>
    /// <returns>True if valid, false otherwise.</returns>
    public static bool ValidateTimeout(int timeoutSeconds, out string? errorMessage)
    {
        if (timeoutSeconds < MinTimeoutSeconds)
        {
            errorMessage = $"Timeout must be at least {MinTimeoutSeconds} seconds.";
            return false;
        }

        if (timeoutSeconds > MaxTimeoutSeconds)
        {
            errorMessage = $"Timeout cannot exceed {MaxTimeoutSeconds} seconds.";
            return false;
        }

        errorMessage = null;
        return true;
    }

    /// <summary>
    /// Normalizes an endpoint URL by ensuring it has no trailing slash.
    /// </summary>
    /// <param name="endpoint">The endpoint URL.</param>
    /// <returns>The normalized endpoint URL.</returns>
    public static string NormalizeEndpoint(string endpoint)
    {
        return endpoint.TrimEnd('/');
    }
}
