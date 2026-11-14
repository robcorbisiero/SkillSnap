using Microsoft.EntityFrameworkCore;

namespace SkillSnap.Api.Data;

public sealed class SkillSnapDbContext(DbContextOptions<SkillSnapDbContext> options) : DbContext(options)
{
    public DbSet<LearningSession> LearningSessions => Set<LearningSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var session = modelBuilder.Entity<LearningSession>();
        session.Property(s => s.Title).HasMaxLength(160).IsRequired();
        session.Property(s => s.Description).HasMaxLength(2000).IsRequired();
        session.Property(s => s.Topic).HasMaxLength(80).IsRequired();
        session.Property(s => s.Instructor).HasMaxLength(120).IsRequired();
        // SQLite drops DateTime.Kind; restore UTC on reads.
        session.Property(s => s.StartsAtUtc).HasConversion(
            value => value.ToUniversalTime(), value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
        session.HasIndex(s => s.StartsAtUtc);
        session.HasData(
            new LearningSession { Id = 1, Title = "Build your first Blazor component", Topic = "Blazor",
                Description = "Explore Razor components, parameters, and events by building a small interactive view.",
                Instructor = "Alex Morgan", StartsAtUtc = new DateTime(2027, 1, 12, 18, 0, 0, DateTimeKind.Utc), DurationMinutes = 60, Capacity = 20 },
            new LearningSession { Id = 2, Title = "APIs that make sense", Topic = "ASP.NET Core",
                Description = "Follow a request through an ASP.NET Core endpoint and return a clear JSON response.",
                Instructor = "Sam Rivera", StartsAtUtc = new DateTime(2027, 1, 14, 19, 0, 0, DateTimeKind.Utc), DurationMinutes = 90, Capacity = 16 },
            new LearningSession { Id = 3, Title = "From C# objects to SQLite", Topic = "Entity Framework Core",
                Description = "Use a DbContext, a migration, and an asynchronous query to read real persisted data.",
                Instructor = "Taylor Chen", StartsAtUtc = new DateTime(2027, 1, 19, 18, 30, 0, DateTimeKind.Utc), DurationMinutes = 75, Capacity = 24 });
    }
}
