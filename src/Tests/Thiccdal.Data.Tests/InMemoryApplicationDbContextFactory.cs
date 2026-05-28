using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Thiccdal.Data.Tests;

public sealed class InMemoryApplicationDbContextFactory : IDbContextFactory<ApplicationDbContext>, IAsyncDisposable
{
    private readonly DbContextOptions<ApplicationDbContext> _options;

    public InMemoryApplicationDbContextFactory(string? databaseName = null)
    {
        DatabaseName = string.IsNullOrWhiteSpace(databaseName)
            ? Guid.NewGuid().ToString("N")
            : databaseName;

        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(DatabaseName, new InMemoryDatabaseRoot())
            .Options;
    }

    public string DatabaseName { get; }

    public ApplicationDbContext CreateDbContext()
    {
        return new ApplicationDbContext(_options);
    }

    public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        return Task.FromResult(CreateDbContext());
    }

    public async ValueTask DisposeAsync()
    {
        await using ApplicationDbContext context = CreateDbContext();
        await context.Database.EnsureDeletedAsync();
    }
}
