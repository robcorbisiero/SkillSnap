using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SkillSnap.Api.Data;

// A separate context gives PostgreSQL its own migration history/model snapshot.
public sealed class PostgresSkillSnapDbContext(DbContextOptions<PostgresSkillSnapDbContext> options)
    : SkillSnapDbContext(options), IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("skillsnap");
        // Raw parameterized statements use this private schema through the connection's Search Path.
    }
}

public sealed class PostgresDesignFactory : IDesignTimeDbContextFactory<PostgresSkillSnapDbContext>
{
    public PostgresSkillSnapDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<PostgresSkillSnapDbContext>().UseNpgsql(
            "Host=localhost;Database=design_only;Username=design_only;Search Path=skillsnap",
            options => options.MigrationsHistoryTable("__EFMigrationsHistory", "skillsnap")).Options);
}
