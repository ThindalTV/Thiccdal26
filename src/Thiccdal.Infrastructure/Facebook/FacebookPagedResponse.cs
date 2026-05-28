using System.Text.Json.Serialization;

namespace Thiccdal.Infrastructure.Facebook;

public sealed record FacebookPagedResponse<T>
{
    [JsonPropertyName("data")]
    public IReadOnlyList<T> Data { get; init; } = [];
}
