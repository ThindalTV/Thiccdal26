namespace Thiccdal.Infrastructure.Streaming;

/// <summary>
/// Creates live and BRB relay sessions.
/// </summary>
public interface IStreamingRelaySessionFactory
{
    /// <summary>
    /// Starts a live-copy relay session from ingest to a platform destination.
    /// </summary>
    Task<IStreamingRelaySession> StartLiveRelay(
        string platformName,
        string sourceUrl,
        string destinationUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a BRB slate relay session to a platform destination.
    /// </summary>
    Task<IStreamingRelaySession> StartBrbRelay(
        string platformName,
        string slatePath,
        string destinationUrl,
        CancellationToken cancellationToken = default);
}
