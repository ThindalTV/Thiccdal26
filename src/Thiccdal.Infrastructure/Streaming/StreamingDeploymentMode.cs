namespace Thiccdal.Infrastructure.Streaming;

/// <summary>
/// Specifies how the RTMP ingest server is deployed.
/// </summary>
public enum StreamingDeploymentMode
{
    /// <summary>
    /// RTMP server runs embedded in the main Thiccdal process.
    /// </summary>
    Embedded,

    /// <summary>
    /// RTMP server runs as a separate service (different container or host).
    /// </summary>
    External
}
