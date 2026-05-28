namespace Thiccdal.Infrastructure.AI;

/// <summary>
/// Configures the OpenAI-compatible transport used by Thiccdal's AI services.
/// </summary>
public sealed class OpenAiOptions
{
    /// <summary>
    /// Gets the configuration section name.
    /// </summary>
    public const string SectionName = "AI:OpenAICompatible";

    /// <summary>
    /// Gets the default local LM Studio endpoint.
    /// </summary>
    public const string DefaultEndpoint = "http://localhost:1234/v1";

    /// <summary>
    /// Gets or sets the OpenAI-compatible endpoint.
    /// </summary>
    public string Endpoint { get; set; } = DefaultEndpoint;

    /// <summary>
    /// Gets or sets the optional API key.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the outbound request timeout in seconds.
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = 30;
}
