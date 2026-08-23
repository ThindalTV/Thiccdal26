namespace Thiccdal.Infrastructure.Readiness;

/// <summary>
/// Reports which operator surfaces are usable given the current configuration.
/// </summary>
public interface ISystemReadinessService
{
    /// <summary>
    /// Raised when configuration changes in a way that may change readiness.
    /// </summary>
    event EventHandler? ReadinessChanged;

    /// <summary>
    /// Returns the current readiness snapshot.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    Task<SystemReadiness> GetReadiness(CancellationToken cancellationToken = default);
}
