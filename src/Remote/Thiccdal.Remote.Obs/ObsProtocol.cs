using System.Text.Json;
using System.Text.Json.Serialization;

namespace Thiccdal.Remote.Obs;

/// <summary>
/// obs-websocket v5 opcodes. Only the subset Thiccdal needs is modelled.
/// </summary>
internal enum ObsOpCode
{
    Hello = 0,
    Identify = 1,
    Identified = 2,
    Event = 5,
    Request = 6,
    RequestResponse = 7
}

internal sealed class ObsMessage
{
    [JsonPropertyName("op")]
    public ObsOpCode Op { get; set; }

    [JsonPropertyName("d")]
    public JsonElement? D { get; set; }
}

internal sealed class ObsHelloData
{
    [JsonPropertyName("obsWebSocketVersion")]
    public string ObsWebSocketVersion { get; set; } = string.Empty;

    [JsonPropertyName("rpcVersion")]
    public int RpcVersion { get; set; }

    [JsonPropertyName("authentication")]
    public ObsAuthenticationData? Authentication { get; set; }
}

internal sealed class ObsAuthenticationData
{
    [JsonPropertyName("challenge")]
    public string Challenge { get; set; } = string.Empty;

    [JsonPropertyName("salt")]
    public string Salt { get; set; } = string.Empty;
}

internal sealed class ObsIdentifyData
{
    [JsonPropertyName("rpcVersion")]
    public int RpcVersion { get; set; }

    [JsonPropertyName("authentication")]
    public string? Authentication { get; set; }
}

internal sealed class ObsEventData
{
    [JsonPropertyName("eventType")]
    public string EventType { get; set; } = string.Empty;

    [JsonPropertyName("eventData")]
    public JsonElement? EventData { get; set; }
}

internal sealed class ObsStreamStateChangedData
{
    [JsonPropertyName("outputActive")]
    public bool OutputActive { get; set; }

    [JsonPropertyName("outputState")]
    public string OutputState { get; set; } = string.Empty;
}

internal sealed class ObsRequestData
{
    [JsonPropertyName("requestType")]
    public string RequestType { get; set; } = string.Empty;

    [JsonPropertyName("requestId")]
    public string RequestId { get; set; } = string.Empty;
}

internal sealed class ObsRequestResponseData
{
    [JsonPropertyName("requestType")]
    public string RequestType { get; set; } = string.Empty;

    [JsonPropertyName("requestId")]
    public string RequestId { get; set; } = string.Empty;

    [JsonPropertyName("responseData")]
    public JsonElement? ResponseData { get; set; }
}

internal sealed class ObsStreamStatusData
{
    [JsonPropertyName("outputActive")]
    public bool OutputActive { get; set; }
}
