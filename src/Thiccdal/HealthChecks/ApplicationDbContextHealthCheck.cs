using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Thiccdal.Data;

namespace Thiccdal.HealthChecks;

public sealed class ApplicationDbContextHealthCheck : IHealthCheck
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly ILogger<ApplicationDbContextHealthCheck> _logger;

    public ApplicationDbContextHealthCheck(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        ILogger<ApplicationDbContextHealthCheck> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        string databaseTarget = "unknown";

        try
        {
            await using ApplicationDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            databaseTarget = GetDatabaseTarget(dbContext.Database.GetConnectionString());

            if (await dbContext.Database.CanConnectAsync(cancellationToken))
            {
                return HealthCheckResult.Healthy($"SQLite database '{databaseTarget}' is reachable.");
            }

            _logger.LogWarning(
                "SQLite readiness check failed because the database connection could not be opened for {DatabaseTarget}",
                databaseTarget);

            return HealthCheckResult.Unhealthy($"SQLite database '{databaseTarget}' could not be reached.");
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "SQLite readiness check failed for {DatabaseTarget}",
                databaseTarget);

            return HealthCheckResult.Unhealthy(
                $"SQLite database '{databaseTarget}' is unavailable.",
                exception);
        }
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
