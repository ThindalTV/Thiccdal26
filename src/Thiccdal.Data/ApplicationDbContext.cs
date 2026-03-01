using Microsoft.EntityFrameworkCore;
using Thiccdal.Data.Models;

namespace Thiccdal.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<TwitchToken> TwitchTokens { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}
