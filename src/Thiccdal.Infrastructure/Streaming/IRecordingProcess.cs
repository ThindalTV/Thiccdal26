namespace Thiccdal.Infrastructure.Streaming;

/// <summary>
/// Represents a running external recording process.
/// </summary>
public interface IRecordingProcess : IDisposable
{
    /// <summary>
    /// Raised when the underlying recording process exits.
    /// </summary>
    event EventHandler? Exited;

    /// <summary>
    /// Gets a value indicating whether the recording process has exited.
    /// </summary>
    bool HasExited { get; }

    /// <summary>
    /// Gets the process exit code when available.
    /// </summary>
    int ExitCode { get; }

    /// <summary>
    /// Requests a graceful stop for the recording process.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    Task Stop(CancellationToken cancellationToken = default);
}
