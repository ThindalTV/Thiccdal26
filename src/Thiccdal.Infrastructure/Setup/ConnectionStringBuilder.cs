namespace Thiccdal.Infrastructure.Setup;

/// <summary>
/// Builds database connection strings for different providers.
/// </summary>
public static class ConnectionStringBuilder
{
    /// <summary>
    /// Gets the default port for a database provider.
    /// </summary>
    /// <param name="provider">The database provider name.</param>
    /// <returns>The default port number.</returns>
    public static int GetDefaultPort(string provider)
    {
        return provider switch
        {
            "PostgreSQL" => 5432,
            "SqlServer" => 1433,
            _ => 0
        };
    }

    /// <summary>
    /// Builds a connection string for the specified provider.
    /// </summary>
    /// <param name="provider">The database provider (PostgreSQL or SqlServer).</param>
    /// <param name="server">The server hostname or IP address.</param>
    /// <param name="port">The port number (0 to use default).</param>
    /// <param name="database">The database name.</param>
    /// <param name="username">The username for authentication.</param>
    /// <param name="password">The password for authentication.</param>
    /// <returns>A formatted connection string.</returns>
    /// <exception cref="ArgumentException">Thrown when the provider is not supported.</exception>
    public static string Build(
        string provider,
        string server,
        int port,
        string database,
        string username,
        string password)
    {
        var effectivePort = port > 0 ? port : GetDefaultPort(provider);

        return provider switch
        {
            "PostgreSQL" => $"Host={server};Port={effectivePort};Database={database};Username={username};Password={password}",
            "SqlServer" => $"Server={server},{effectivePort};Database={database};User Id={username};Password={password};TrustServerCertificate=True",
            _ => throw new ArgumentException($"Unsupported database provider: {provider}", nameof(provider))
        };
    }

    /// <summary>
    /// Validates that required connection parameters are provided.
    /// </summary>
    /// <param name="server">The server hostname.</param>
    /// <param name="database">The database name.</param>
    /// <param name="username">The username.</param>
    /// <param name="errorMessage">Output error message if validation fails.</param>
    /// <returns>True if valid, false otherwise.</returns>
    public static bool Validate(
        string server,
        string database,
        string username,
        out string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(server))
        {
            errorMessage = "Server is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(database))
        {
            errorMessage = "Database name is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            errorMessage = "Username is required.";
            return false;
        }

        errorMessage = null;
        return true;
    }
}
