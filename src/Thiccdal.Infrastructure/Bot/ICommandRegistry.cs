namespace Thiccdal.Infrastructure.Bot;

/// <summary>
/// Provides the current enabled chatbot commands from the active backing store.
/// </summary>
public interface ICommandRegistry
{
    /// <summary>
    /// Gets the enabled commands available for dispatch.
    /// </summary>
    /// <returns>The current enabled command set.</returns>
    IReadOnlyList<Models.BotCommandDefinition> GetEnabledCommands();

    /// <summary>
    /// Reloads the enabled command cache from the backing store.
    /// </summary>
    /// <param name="cancellationToken">Cancels the reload operation.</param>
    Task Reload(CancellationToken cancellationToken = default);
}
