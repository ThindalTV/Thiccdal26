namespace Thiccdal.Data;

/// <summary>
/// Detects the database provider from a connection string.
/// </summary>
public static class DatabaseProviderDetector
{
    /// <summary>
    /// Supported database providers.
    /// </summary>
    public enum DatabaseProvider
    {
        /// <summary>SQLite database provider.</summary>
        SQLite,

        /// <summary>PostgreSQL database provider.</summary>
        PostgreSQL,

        /// <summary>SQL Server database provider.</summary>
        SqlServer
    }

    /// <summary>
    /// Detects the database provider from the connection string format.
    /// </summary>
    /// <param name="connectionString">The connection string to analyze.</param>
    /// <returns>The detected <see cref="DatabaseProvider"/>.</returns>
    public static DatabaseProvider Detect(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return DatabaseProvider.SQLite;

        // PostgreSQL: Host=...;Database=...
        if (connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase))
            return DatabaseProvider.PostgreSQL;

        // SQL Server: Server=... or Data Source=server,...
        if (connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase) ||
            (connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase) &&
             !connectionString.EndsWith(".db", StringComparison.OrdinalIgnoreCase) &&
             !connectionString.Contains(".db;", StringComparison.OrdinalIgnoreCase)))
            return DatabaseProvider.SqlServer;

        // Default to SQLite (Data Source=file.db)
        return DatabaseProvider.SQLite;
    }
}
