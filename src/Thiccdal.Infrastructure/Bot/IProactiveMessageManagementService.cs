namespace Thiccdal.Infrastructure.Bot;

/// <summary>
/// Provides operator-facing CRUD access to the bot's timed autoresponses.
/// </summary>
public interface IProactiveMessageManagementService
{
    /// <summary>
    /// Lists every autoresponse, enabled or not, in interval order.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    Task<IReadOnlyList<ProactiveMessageDefinition>> List(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new autoresponse.
    /// </summary>
    /// <param name="message">The autoresponse values.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    Task<ProactiveMessageDefinition> Create(ProactiveMessageInput message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing autoresponse.
    /// </summary>
    /// <param name="id">The autoresponse identifier.</param>
    /// <param name="message">The replacement values.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>The updated autoresponse, or <c>null</c> when it no longer exists.</returns>
    Task<ProactiveMessageDefinition?> Update(long id, ProactiveMessageInput message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an autoresponse.
    /// </summary>
    /// <param name="id">The autoresponse identifier.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns><c>true</c> when the autoresponse was deleted; otherwise <c>false</c>.</returns>
    Task<bool> Delete(long id, CancellationToken cancellationToken = default);
}
