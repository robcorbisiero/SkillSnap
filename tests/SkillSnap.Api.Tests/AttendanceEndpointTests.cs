using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using SkillSnap.Api.Data;
using SkillSnap.Api.Security;
using SkillSnap.Contracts;

namespace SkillSnap.Api.Tests;

public sealed class AttendanceEndpointTests
{
    private const string Route = "/api/organizer/learning-sessions/1/attendance/";
    private const string BookingRoute = "/api/learning-sessions/1/registration";

    [Fact]
    public async Task Attendance_and_its_version_survive_api_restart()
    {
        var path = Path.Combine(Path.GetTempPath(), $"skillsnap-{Guid.NewGuid():N}.db");
        const string email = "restart-organizer@example.test";
        try
        {
            using (var first = new SessionFactory(path))
            {
                using var client = await TestAccounts.CreateAsync(first, email);
                using (var scope = first.Services.CreateScope())
                    await OrganizerProvisioner.GrantAsync(scope.ServiceProvider, email);
                await client.PostAsync(BookingRoute, null);
                var entry = (await Roster(client)).Entries.Single();
                await client.PutAsJsonAsync(Route + entry.RegistrationId, new AttendanceUpdate("Absent", 0));
            }
            using var second = new SessionFactory(path);
            using var restored = await TestAccounts.CreateAsync(second, email);
            var saved = (await Roster(restored)).Entries.Single();
            Assert.Equal(AttendanceStatus.Absent, saved.Status);
            Assert.Equal(1, saved.Version);
            Assert.NotNull(saved.UpdatedAtUtc);
            Assert.Equal(HttpStatusCode.Conflict, (await restored.DeleteAsync(BookingRoute)).StatusCode);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Rosters_and_mutations_are_only_available_to_current_organizers()
    {
        using var factory = new SessionFactory();
        using var anonymous = factory.CreateClient();
        using var attendee = await TestAccounts.CreateAsync(factory);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Route)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PutAsJsonAsync(Route + "1", new AttendanceUpdate("Attended", 0))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await attendee.GetAsync(Route)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await attendee.PutAsJsonAsync(Route + "1", new AttendanceUpdate("Attended", 0))).StatusCode);
    }

    [Fact]
    public async Task Empty_and_missing_rosters_are_distinguished_and_not_cached()
    {
        using var factory = new SessionFactory();
        using var organizer = await Organizer(factory);
        using var response = await organizer.GetAsync(Route);
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
        var roster = (await response.Content.ReadFromJsonAsync<AttendanceRosterDto>())!;
        Assert.Equal(1, roster.SessionId);
        Assert.Empty(roster.Entries);
        Assert.Equal(HttpStatusCode.NotFound, (await organizer.GetAsync("/api/organizer/learning-sessions/999/attendance/")).StatusCode);
    }

    [Fact]
    public async Task Organizer_can_record_correct_and_clear_with_server_derived_actor_and_timestamp()
    {
        using var factory = new SessionFactory();
        using var organizer = await Organizer(factory);
        using var attendee = await TestAccounts.CreateAsync(factory, "attendee@example.test");
        await attendee.PostAsync(BookingRoute, null);
        var entry = (await Roster(organizer)).Entries.Single();
        Assert.Equal(AttendanceStatus.NotRecorded, entry.Status);
        Assert.Null(entry.UpdatedAtUtc);
        Assert.Equal("attendee@example.test", entry.Email);
        var forged = new { status = "Attended", version = entry.Version, attendanceUpdatedByUserId = "forged", updatedAtUtc = "2000-01-01T00:00:00Z" };
        Assert.Equal(HttpStatusCode.NoContent, (await organizer.PutAsJsonAsync(Route + entry.RegistrationId, forged)).StatusCode);
        entry = (await Roster(organizer)).Entries.Single();
        Assert.Equal(AttendanceStatus.Attended, entry.Status);
        Assert.Equal(1, entry.Version);
        Assert.Equal(DateTimeKind.Utc, entry.UpdatedAtUtc!.Value.Kind);
        Assert.True(entry.UpdatedAtUtc > DateTime.UtcNow.AddMinutes(-1));
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SkillSnapDbContext>();
            var row = await db.Registrations.SingleAsync();
            Assert.NotEqual("forged", row.AttendanceUpdatedByUserId);
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            Assert.True(await users.IsInRoleAsync((await users.FindByIdAsync(row.AttendanceUpdatedByUserId!))!, "Organizer"));
        }
        await organizer.PutAsJsonAsync(Route + entry.RegistrationId, new AttendanceUpdate("Absent", entry.Version));
        entry = (await Roster(organizer)).Entries.Single();
        Assert.Equal(AttendanceStatus.Absent, entry.Status);
        await organizer.PutAsJsonAsync(Route + entry.RegistrationId, new AttendanceUpdate("NotRecorded", entry.Version));
        entry = (await Roster(organizer)).Entries.Single();
        Assert.Equal(AttendanceStatus.NotRecorded, entry.Status);
        Assert.Equal(3, entry.Version);
    }

    [Fact]
    public async Task Public_and_other_account_responses_do_not_reveal_attendee_identity_or_attendance()
    {
        using var factory = new SessionFactory();
        using var organizer = await Organizer(factory);
        using var owner = await TestAccounts.CreateAsync(factory, "private-attendee@example.test");
        using var other = await TestAccounts.CreateAsync(factory);
        using var guest = factory.CreateClient();
        await owner.PostAsync(BookingRoute, null);
        var entry = (await Roster(organizer)).Entries.Single();
        await organizer.PutAsJsonAsync(Route + entry.RegistrationId, new AttendanceUpdate("Attended", entry.Version));
        var own = (await owner.GetFromJsonAsync<List<LearningSessionDto>>("/api/learning-sessions"))!.Single(s => s.Id == 1);
        Assert.Equal(AttendanceStatus.Attended, own.AttendanceStatus);
        foreach (var client in new[] { guest, other })
        {
            var json = await client.GetStringAsync("/api/learning-sessions");
            Assert.DoesNotContain("private-attendee@example.test", json);
            Assert.DoesNotContain("registrationId", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("updatedBy", json, StringComparison.OrdinalIgnoreCase);
            var session = (await client.GetFromJsonAsync<List<LearningSessionDto>>("/api/learning-sessions"))!.Single(s => s.Id == 1);
            Assert.Null(session.AttendanceStatus);
            Assert.False(session.IsRegistered);
            Assert.Equal(1, session.RegisteredCount);
        }
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData("Present", 0)]
    [InlineData("1", 0)]
    [InlineData("Attended", -1)]
    public async Task Invalid_status_or_version_is_rejected(string? status, int version)
    {
        using var factory = new SessionFactory();
        using var organizer = await Organizer(factory);
        Assert.Equal(HttpStatusCode.BadRequest, (await organizer.PutAsJsonAsync(Route + "1", new AttendanceUpdate(status, version))).StatusCode);
    }

    [Fact]
    public async Task Csrf_missing_registration_and_wrong_session_are_rejected()
    {
        using var factory = new SessionFactory();
        using var organizer = await Organizer(factory);
        using var attendee = await TestAccounts.CreateAsync(factory);
        await attendee.PostAsync(BookingRoute, null);
        var entry = (await Roster(organizer)).Entries.Single();
        Assert.Equal(HttpStatusCode.NotFound, (await organizer.PutAsJsonAsync(Route + "99999", new AttendanceUpdate("Attended", 0))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await organizer.PutAsJsonAsync($"/api/organizer/learning-sessions/2/attendance/{entry.RegistrationId}", new AttendanceUpdate("Attended", 0))).StatusCode);
        organizer.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await organizer.PutAsJsonAsync(Route + entry.RegistrationId, new AttendanceUpdate("Attended", 0))).StatusCode);
    }

    [Fact]
    public async Task Recorded_attendance_blocks_cancellation_until_explicitly_cleared()
    {
        using var factory = new SessionFactory();
        using var organizer = await Organizer(factory);
        using var owner = await TestAccounts.CreateAsync(factory);
        await owner.PostAsync(BookingRoute, null);
        var entry = (await Roster(organizer)).Entries.Single();
        await organizer.PutAsJsonAsync(Route + entry.RegistrationId, new AttendanceUpdate("Attended", 0));
        Assert.Equal(HttpStatusCode.Conflict, (await owner.DeleteAsync(BookingRoute)).StatusCode);
        Assert.Single((await Roster(organizer)).Entries);
        entry = (await Roster(organizer)).Entries.Single();
        await organizer.PutAsJsonAsync(Route + entry.RegistrationId, new AttendanceUpdate("NotRecorded", entry.Version));
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync(BookingRoute)).StatusCode);
        Assert.Empty((await Roster(organizer)).Entries);
    }

    [Fact]
    public async Task Concurrent_corrections_reject_stale_version()
    {
        using var factory = new SessionFactory();
        using var organizer = await Organizer(factory);
        using var owner = await TestAccounts.CreateAsync(factory);
        await owner.PostAsync(BookingRoute, null);
        var entry = (await Roster(organizer)).Entries.Single();
        var responses = await Task.WhenAll(
            organizer.PutAsJsonAsync(Route + entry.RegistrationId, new AttendanceUpdate("Attended", 0)),
            organizer.PutAsJsonAsync(Route + entry.RegistrationId, new AttendanceUpdate("Absent", 0)));
        try
        {
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.NoContent);
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
            Assert.Equal(1, (await Roster(organizer)).Entries.Single().Version);
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Fact]
    public async Task Concurrent_cancellation_cannot_remove_recorded_attendance()
    {
        using var factory = new SessionFactory();
        using var organizer = await Organizer(factory);
        using var owner = await TestAccounts.CreateAsync(factory);
        await owner.PostAsync(BookingRoute, null);
        var entry = (await Roster(organizer)).Entries.Single();
        var mark = organizer.PutAsJsonAsync(Route + entry.RegistrationId, new AttendanceUpdate("Attended", 0));
        var cancel = owner.DeleteAsync(BookingRoute);
        using var markResponse = await mark;
        using var cancelResponse = await cancel;
        var remaining = (await Roster(organizer)).Entries;
        if (remaining.Count == 0)
        {
            Assert.Equal(HttpStatusCode.NoContent, cancelResponse.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, markResponse.StatusCode);
        }
        else
        {
            Assert.Equal(AttendanceStatus.Attended, remaining.Single().Status);
            Assert.Equal(HttpStatusCode.NoContent, markResponse.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, cancelResponse.StatusCode);
        }
    }

    [Fact]
    public async Task Legacy_bookings_are_unidentified_and_require_import_before_attendance()
    {
        using var factory = new SessionFactory();
        using var organizer = await Organizer(factory);
        using var owner = await TestAccounts.CreateAsync(factory);
        var key = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SkillSnapDbContext>();
            db.Registrations.Add(new Registration { LearningSessionId = 1, ParticipantHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key.ToString("N")))) });
            await db.SaveChangesAsync();
        }
        var entry = (await Roster(organizer)).Entries.Single();
        Assert.True(entry.IsLegacy);
        Assert.Null(entry.Email);
        Assert.Equal(HttpStatusCode.Conflict, (await organizer.PutAsJsonAsync(Route + entry.RegistrationId, new AttendanceUpdate("Attended", 0))).StatusCode);
        owner.DefaultRequestHeaders.Add("X-Participant-Key", key.ToString("N"));
        await owner.PostAsync("/api/registrations/import", null);
        Assert.False((await Roster(organizer)).Entries.Single().IsLegacy);
        Assert.Equal(HttpStatusCode.NoContent, (await organizer.PutAsJsonAsync(Route + entry.RegistrationId, new AttendanceUpdate("Attended", 0))).StatusCode);
    }

    [Fact]
    public async Task Earlier_database_upgrades_with_booking_and_details_preserved()
    {
        var path = Path.Combine(Path.GetTempPath(), $"skillsnap-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<SkillSnapDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options;
            await using (var old = new SkillSnapDbContext(options))
            {
                await old.GetService<IMigrator>().MigrateAsync("20261114213040_AddSessionVersion");
                await old.Database.ExecuteSqlRawAsync("INSERT INTO Registrations (LearningSessionId,ParticipantHash) VALUES (1,'old-hash')");
            }
            using var factory = new SessionFactory(path);
            using var organizer = await Organizer(factory);
            var roster = await Roster(organizer);
            Assert.Equal("Build your first Blazor component", roster.Title);
            Assert.Equal(AttendanceStatus.NotRecorded, roster.Entries.Single().Status);
            Assert.Equal(0, roster.Entries.Single().Version);
        }
        finally { File.Delete(path); }
    }

    private static async Task<AttendanceRosterDto> Roster(HttpClient client) =>
        (await client.GetFromJsonAsync<AttendanceRosterDto>(Route))!;
    private static async Task<HttpClient> Organizer(SessionFactory factory)
    {
        var email = $"organizer-{Guid.NewGuid():N}@example.test";
        var client = await TestAccounts.CreateAsync(factory, email);
        using var scope = factory.Services.CreateScope();
        await OrganizerProvisioner.GrantAsync(scope.ServiceProvider, email);
        return client;
    }
}
