namespace Thiccdal.Infrastructure.LmStudio;

/// <summary>
/// Configures the reusable LM Studio client.
/// </summary>
public sealed class LmStudioOptions
{
    /// <summary>
    /// Gets the configuration section name.
    /// </summary>
    public const string SectionName = "LMStudio";

    /// <summary>
    /// Gets the default LM Studio base address.
    /// </summary>
    public const string DefaultBaseAddress = "http://localhost:1234/";

    /// <summary>
    /// Gets or sets the LM Studio server base address.
    /// </summary>
    public string BaseAddress { get; set; } = DefaultBaseAddress;

    /// <summary>
    /// Gets or sets the optional API key header value.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the outbound request timeout in seconds.
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = 30;
}
