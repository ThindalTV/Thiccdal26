using System.Text.Json.Serialization;

namespace Thiccdal.Infrastructure.Facebook;

public sealed record FacebookComment
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("from")]
    public FacebookUser From { get; init; } = new();

    [JsonPropertyName("created_time")]
    public string CreatedTime { get; init; } = string.Empty;
}
