using System.Text.Json.Serialization;

namespace Thiccdal.Infrastructure.Facebook;

public sealed record FacebookLiveVideo
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("stream_url")]
    public string StreamUrl { get; init; } = string.Empty;

    [JsonPropertyName("secure_stream_url")]
    public string SecureStreamUrl { get; init; } = string.Empty;
}
