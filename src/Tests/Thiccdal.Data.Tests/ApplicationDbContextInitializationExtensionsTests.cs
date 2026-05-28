using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Thiccdal.Data;

namespace Thiccdal.Data.Tests;

public class ApplicationDbContextInitializationExtensionsTests
{
    [Fact]
    public async Task WhenDatabaseFileIsMissing_ThenInitializeDatabaseCreatesFileAndAppliesMigrations()
    {
        string databasePath = PrepareDatabasePath(nameof(WhenDatabaseFileIsMissing_ThenInitializeDatabaseCreatesFileAndAppliesMigrations));
        IServiceProvider services = BuildServices(databasePath);

        await services.InitializeDatabase();

        Assert.True(File.Exists(databasePath));

        await using ApplicationDbContext context = services
            .GetRequiredService<IDbContextFactory<ApplicationDbContext>>()
            .CreateDbContext();

        string[] appliedMigrations = (await context.Database.GetAppliedMigrationsAsync()).ToArray();

        Assert.Contains("20260228231810_TwitchAuthInitial", appliedMigrations);
        Assert.Contains(appliedMigrations, static migration => migration.EndsWith("_AddChecklistSession", StringComparison.Ordinal));
        Assert.Contains(appliedMigrations, static migration => migration.EndsWith("_AddCustomChecklistItem", StringComparison.Ordinal));
        Assert.Contains(appliedMigrations, static migration => migration.EndsWith("_AddUserIdentity", StringComparison.Ordinal));
        Assert.Contains(appliedMigrations, static migration => migration.EndsWith("_AddUserIdentitySuggestions", StringComparison.Ordinal));
        Assert.True(await context.Database.CanConnectAsync());
    }

    [Fact]
    public async Task WhenConfiguredDirectoryIsMissing_ThenInitializeDatabaseCreatesDirectoryAndDatabase()
    {
        string rootPath = Path.Combine(
            AppContext.BaseDirectory,
            "DatabaseInitializationTests",
            nameof(WhenConfiguredDirectoryIsMissing_ThenInitializeDatabaseCreatesDirectoryAndDatabase));

        if (Directory.Exists(rootPath))
        {
            Directory.Delete(rootPath, recursive: true);
        }

        string databasePath = Path.Combine(rootPath, "nested", "thiccdal.db");
        IServiceProvider services = BuildServices(databasePath);

        await services.InitializeDatabase();

        Assert.True(Directory.Exists(Path.GetDirectoryName(databasePath)!));
        Assert.True(File.Exists(databasePath));
    }

    private static IServiceProvider BuildServices(string databasePath)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddDbContextFactory<ApplicationDbContext>(options => options.UseSqlite($"Data Source={databasePath}"));

        return services.BuildServiceProvider();
    }

    private static string PrepareDatabasePath(string testName)
    {
        string rootPath = Path.Combine(AppContext.BaseDirectory, "DatabaseInitializationTests", testName);
        if (Directory.Exists(rootPath))
        {
            Directory.Delete(rootPath, recursive: true);
        }

        Directory.CreateDirectory(rootPath);
        return Path.Combine(rootPath, "thiccdal.db");
    }
}
