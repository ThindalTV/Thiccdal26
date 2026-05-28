using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Thiccdal.Data;

public static class ApplicationDbContextInitializationExtensions
{
    public static async Task InitializeDatabase(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        using IServiceScope scope = services.CreateScope();
        ILogger logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Thiccdal.Data.DatabaseInitialization");

        IDbContextFactory<ApplicationDbContext> dbContextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<ApplicationDbContext>>();

        await using ApplicationDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        EnsureSqliteDirectoryExists(dbContext);

        string[] pendingMigrations = (await dbContext.Database
            .GetPendingMigrationsAsync(cancellationToken))
            .ToArray();
        string databaseTarget = GetDatabaseTarget(dbContext.Database.GetConnectionString());

        if (pendingMigrations.Length == 0)
        {
            logger.LogDebug("SQLite database at {DatabaseTarget} is already up to date.", databaseTarget);
            return;
        }

        logger.LogInformation(
            "Applying {MigrationCount} pending migration(s) to SQLite database at {DatabaseTarget}: {MigrationNames}",
            pendingMigrations.Length,
            databaseTarget,
            pendingMigrations);

        await dbContext.Database.MigrateAsync(cancellationToken);

        logger.LogInformation(
            "Applied {MigrationCount} pending migration(s) to SQLite database at {DatabaseTarget}",
            pendingMigrations.Length,
            databaseTarget);
    }

    private static void EnsureSqliteDirectoryExists(ApplicationDbContext dbContext)
    {
        string? connectionString = dbContext.Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        SqliteConnectionStringBuilder connectionStringBuilder = new(connectionString);
        if (string.IsNullOrWhiteSpace(connectionStringBuilder.DataSource) ||
            string.Equals(connectionStringBuilder.DataSource, ":memory:", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string fullPath = Path.GetFullPath(connectionStringBuilder.DataSource);
        string? directoryPath = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return;
        }

        Directory.CreateDirectory(directoryPath);
    }

    private static string GetDatabaseTarget(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return "unknown";
        }

        SqliteConnectionStringBuilder connectionStringBuilder = new(connectionString);
        if (string.IsNullOrWhiteSpace(connectionStringBuilder.DataSource))
        {
            return "unknown";
        }

        if (string.Equals(connectionStringBuilder.DataSource, ":memory:", StringComparison.OrdinalIgnoreCase))
        {
            return ":memory:";
        }

        return Path.GetFullPath(connectionStringBuilder.DataSource);
    }
}
