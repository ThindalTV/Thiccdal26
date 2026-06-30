using System.Diagnostics.CodeAnalysis;

namespace Thiccdal.Infrastructure.Setup;

/// <summary>
/// Validates streaming configuration values.
/// </summary>
public static class StreamingConfigValidator
{
    /// <summary>
    /// Validates an RTMP ingest URL.
    /// </summary>
    public static bool ValidateIngestUrl(string? url, [NotNullWhen(false)] out string? error)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            error = "Ingest URL is required.";
            return false;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            error = "Ingest URL must be a valid absolute URL.";
            return false;
        }

        if (!string.Equals(uri.Scheme, "rtmp", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, "rtmps", StringComparison.OrdinalIgnoreCase))
        {
            error = "Ingest URL must use rtmp:// or rtmps:// scheme.";
            return false;
        }

        string streamPath = uri.AbsolutePath.Trim().Trim('/');
        if (string.IsNullOrWhiteSpace(streamPath))
        {
            error = "Ingest URL must include a stream path (e.g., rtmp://host:1935/live).";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Validates an external RTMP host address.
    /// </summary>
    public static bool ValidateExternalHost(string? host, [NotNullWhen(false)] out string? error)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            error = "External RTMP host is required when using external deployment mode.";
            return false;
        }

        if (host.Contains("://"))
        {
            error = "External host should be a hostname or IP address, not a URL.";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Validates an API port number.
    /// </summary>
    public static bool ValidateApiPort(int port, [NotNullWhen(false)] out string? error)
    {
        if (port < 1 || port > 65535)
        {
            error = "API port must be between 1 and 65535.";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Validates an API key.
    /// </summary>
    public static bool ValidateApiKey(string? apiKey, [NotNullWhen(false)] out string? error)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            error = "API key is required for external RTMP server authentication.";
            return false;
        }

        if (apiKey.Length < 16)
        {
            error = "API key should be at least 16 characters for security.";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Validates a recording output path.
    /// </summary>
    public static bool ValidateRecordingPath(string? path, bool isRequired, [NotNullWhen(false)] out string? error)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            if (isRequired)
            {
                error = "Recording output path is required.";
                return false;
            }

            error = null;
            return true;
        }

        if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            error = "Recording path contains invalid characters.";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Validates an FFmpeg executable path.
    /// </summary>
    public static bool ValidateFfmpegPath(string? path, [NotNullWhen(false)] out string? error)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            error = "FFmpeg path is required.";
            return false;
        }

        if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            error = "FFmpeg path contains invalid characters.";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Validates a BRB slate file path (optional).
    /// </summary>
    public static bool ValidateBrbSlatePath(string? path, [NotNullWhen(false)] out string? error)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            error = null;
            return true;
        }

        if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            error = "BRB slate path contains invalid characters.";
            return false;
        }

        string extension = Path.GetExtension(path).ToLowerInvariant();
        string[] validExtensions = [".png", ".jpg", ".jpeg", ".mp4", ".webm", ".mov"];
        if (!validExtensions.Contains(extension))
        {
            error = $"BRB slate must be an image or video file ({string.Join(", ", validExtensions)}).";
            return false;
        }

        error = null;
        return true;
    }
}
