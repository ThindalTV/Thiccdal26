namespace Thiccdal.API.Status;

/// <summary>
/// Builds the public stream status payload exposed by the host status endpoints.
/// </summary>
public interface IStreamStatusService
{
    /// <summary>
    /// Gets the current public stream status snapshot.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the request.</param>
    /// <returns>The current stream status response.</returns>
    Task<StreamStatusResponse> GetStatus(CancellationToken cancellationToken = default);
}
