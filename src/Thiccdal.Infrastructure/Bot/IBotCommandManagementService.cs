namespace Thiccdal.Infrastructure.Bot;

/// <summary>
/// Provides operator-facing CRUD access to chatbot commands.
/// </summary>
public interface IBotCommandManagementService
{
    /// <summary>
    /// Lists the current commands in trigger order.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The current command catalog.</returns>
    Task<IReadOnlyList<Models.BotCommandDefinition>> List(CancellationToken cancellationToken);

    /// <summary>
    /// Creates a new chatbot command.
    /// </summary>
    /// <param name="command">The command to create.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created command.</returns>
    Task<Models.BotCommandDefinition> Create(Models.BotCommandDefinitionInput command, CancellationToken cancellationToken);

    /// <summary>
    /// Updates an existing chatbot command.
    /// </summary>
    /// <param name="id">The command identifier.</param>
    /// <param name="command">The replacement command values.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated command, or <c>null</c> when the command no longer exists.</returns>
    Task<Models.BotCommandDefinition?> Update(long id, Models.BotCommandDefinitionInput command, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes an existing chatbot command.
    /// </summary>
    /// <param name="id">The command identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><c>true</c> when the command was deleted; otherwise <c>false</c>.</returns>
    Task<bool> Delete(long id, CancellationToken cancellationToken);

    /// <summary>
    /// Increments the persisted use count for the supplied trigger when it exists.
    /// </summary>
    /// <param name="trigger">The normalized command trigger.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task IncrementUseCount(string trigger, CancellationToken cancellationToken);
}
