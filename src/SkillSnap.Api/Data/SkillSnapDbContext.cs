using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using SkillSnap.Contracts;

namespace SkillSnap.Api.Data;

public sealed class SkillSnapDbContext(DbContextOptions<SkillSnapDbContext> options) : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<LearningSession> LearningSessions => Set<LearningSession>();
    public DbSet<Registration> Registrations => Set<Registration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        var registration = modelBuilder.Entity<Registration>();
        registration.Property(r => r.AttendanceStatus).HasDefaultValue(AttendanceStatus.NotRecorded);
        registration.Property(r => r.AttendanceVersion).HasDefaultValue(0).IsConcurrencyToken();
        registration.Property(r => r.AttendanceUpdatedAtUtc).HasConversion(
            value => value, value => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : (DateTime?)null);
        registration.ToTable(t => t.HasCheckConstraint("CK_Registration_Attendance",
            "\"AttendanceStatus\" IN (0,1,2) AND \"AttendanceVersion\" >= 0"));
        registration.Property(r => r.ParticipantHash).HasMaxLength(64);
        registration.HasIndex(r => new { r.LearningSessionId, r.ParticipantHash }).IsUnique();
        registration.HasIndex(r => new { r.LearningSessionId, r.UserId }).IsUnique();
        registration.HasOne<IdentityUser>().WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
        registration.ToTable(t => t.HasCheckConstraint("CK_Registration_Owner",
            "(\"UserId\" IS NOT NULL AND \"ParticipantHash\" IS NULL) OR (\"UserId\" IS NULL AND \"ParticipantHash\" IS NOT NULL)"));
        registration.HasOne<LearningSession>().WithMany()
            .HasForeignKey(r => r.LearningSessionId).OnDelete(DeleteBehavior.Cascade);
        var session = modelBuilder.Entity<LearningSession>();
        session.Property(s => s.Version).IsConcurrencyToken();
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
