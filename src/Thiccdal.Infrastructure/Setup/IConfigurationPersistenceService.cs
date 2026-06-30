namespace Thiccdal.Infrastructure.Setup;

/// <summary>
/// Persists application configuration to the database.
/// </summary>
public interface IConfigurationPersistenceService
{
    /// <summary>
    /// Gets a configuration value by key.
    /// </summary>
    /// <param name="key">The configuration key.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The configuration value, or null if the key does not exist.</returns>
    Task<string?> GetValue(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a configuration value by key.
    /// </summary>
    /// <param name="key">The configuration key.</param>
    /// <param name="value">The configuration value.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task SetValue(string key, string value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a typed configuration value by key, deserializing from JSON.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <param name="key">The configuration key.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The deserialized configuration value, or null if the key does not exist or deserialization fails.</returns>
    Task<T?> GetValue<T>(string key, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Sets a typed configuration value by key, serializing to JSON.
    /// </summary>
    /// <typeparam name="T">The type to serialize from.</typeparam>
    /// <param name="key">The configuration key.</param>
    /// <param name="value">The configuration value.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task SetValue<T>(string key, T value, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Checks whether a configuration key exists.
    /// </summary>
    /// <param name="key">The configuration key.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>True if the key exists, false otherwise.</returns>
    Task<bool> HasKey(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a configuration key.
    /// </summary>
    /// <param name="key">The configuration key.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task RemoveKey(string key, CancellationToken cancellationToken = default);
}
