using Microsoft.EntityFrameworkCore;

namespace Thiccdal.Data.Tests;

public abstract class ApplicationDbContextTestFixture : IAsyncLifetime
{
    private readonly InMemoryApplicationDbContextFactory _dbContextFactory = new();

    protected IDbContextFactory<ApplicationDbContext> DbContextFactory => _dbContextFactory;

    protected ApplicationDbContext CreateDbContext()
    {
        return _dbContextFactory.CreateDbContext();
    }

    protected Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        return _dbContextFactory.CreateDbContextAsync(cancellationToken);
    }

    public virtual async Task InitializeAsync()
    {
        await ResetDatabase();
    }

    public virtual async Task DisposeAsync()
    {
        await _dbContextFactory.DisposeAsync();
    }

    protected async Task ResetDatabase(CancellationToken cancellationToken = default)
    {
        await using ApplicationDbContext context = await CreateDbContextAsync(cancellationToken);
        await context.Database.EnsureDeletedAsync(cancellationToken);
        await context.Database.EnsureCreatedAsync(cancellationToken);
    }
}
