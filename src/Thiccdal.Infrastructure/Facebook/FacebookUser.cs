using System.Text.Json.Serialization;

namespace Thiccdal.Infrastructure.Facebook;

public sealed record FacebookUser
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
}
