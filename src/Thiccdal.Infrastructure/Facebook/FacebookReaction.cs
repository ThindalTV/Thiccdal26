using System.Text.Json.Serialization;

namespace Thiccdal.Infrastructure.Facebook;

public sealed record FacebookReaction
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
}
