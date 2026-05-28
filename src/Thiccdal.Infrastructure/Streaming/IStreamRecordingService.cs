namespace Thiccdal.Infrastructure.Streaming;

/// <summary>
/// Persists local recording lifecycle rows for restream sessions.
/// </summary>
public interface IStreamRecordingService
{
    /// <summary>
    /// Creates a new recording row when local capture begins.
    /// </summary>
    /// <param name="sessionId">The optional operator live-session identifier.</param>
    /// <param name="platform">The recording platform label.</param>
    /// <param name="filePath">The target recording file path.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    Task<StreamRecordingSnapshot> Start(
        Guid? sessionId,
        string platform,
        string filePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes a recording row when capture stops or fails.
    /// </summary>
    /// <param name="recordingId">The recording row identifier.</param>
    /// <param name="error">The optional error message captured at shutdown.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    Task<StreamRecordingSnapshot> Stop(
        int recordingId,
        string? error,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the latest recording row for the supplied platform.
    /// </summary>
    /// <param name="platform">The recording platform label.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    Task<StreamRecordingSnapshot?> GetLatest(string platform, CancellationToken cancellationToken = default);
}
