namespace Thiccdal.Infrastructure.Streaming;

/// <summary>
/// Starts the external media process used for local recording.
/// </summary>
public interface IRecordingProcessRunner
{
    /// <summary>
    /// Starts a recording process for the supplied request.
    /// </summary>
    /// <param name="request">The recording process request.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>A handle for the running recording process.</returns>
    IRecordingProcess Start(RecordingProcessRequest request, CancellationToken cancellationToken = default);
}
