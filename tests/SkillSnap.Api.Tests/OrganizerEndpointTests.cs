using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SkillSnap.Api.Data;
using SkillSnap.Api.Security;
using SkillSnap.Contracts;

namespace SkillSnap.Api.Tests;

public sealed class OrganizerEndpointTests
{
    private const string Route = "/api/organizer/learning-sessions/";

    [Fact]
    public async Task Anonymous_and_regular_accounts_cannot_manage_sessions()
    {
        using var factory = new SessionFactory();
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Route)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync(Route, Valid())).StatusCode);
        using var attendee = await TestAccounts.CreateAsync(factory);
        Assert.Equal(HttpStatusCode.Forbidden, (await attendee.GetAsync(Route)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await attendee.GetAsync(Route + "1")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await attendee.PostAsJsonAsync(Route, Valid())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await attendee.PutAsJsonAsync(Route + "1", Valid())).StatusCode);
    }

    [Fact]
    public async Task Signup_cannot_grant_itself_organizer_access()
    {
        using var factory = new SessionFactory();
        using var client = factory.CreateClient();
        await TestAccounts.CsrfAsync(client);
        var signup = new { email = "forged@example.test", password = TestAccounts.Password, isOrganizer = true, role = "Organizer", roles = new[] { "Organizer" } };
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/register", signup)).StatusCode);
        Assert.False((await client.GetFromJsonAsync<AccountInfo>("/api/auth/me"))!.IsOrganizer);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Route)).StatusCode);
    }

    [Fact]
    public async Task Local_provisioning_and_revocation_take_effect_without_replacing_login_cookie()
    {
        using var factory = new SessionFactory();
        const string email = "organizer@example.test";
        using var client = await TestAccounts.CreateAsync(factory, email);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Route)).StatusCode);
        using var scope = factory.Services.CreateScope();
        await OrganizerProvisioner.GrantAsync(scope.ServiceProvider, email);
        await OrganizerProvisioner.GrantAsync(scope.ServiceProvider, email);
        Assert.True((await client.GetFromJsonAsync<AccountInfo>("/api/auth/me"))!.IsOrganizer);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Route)).StatusCode);
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        await users.RemoveFromRoleAsync((await users.FindByEmailAsync(email))!, "Organizer");
        Assert.False((await client.GetFromJsonAsync<AccountInfo>("/api/auth/me"))!.IsOrganizer);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Route)).StatusCode);
    }

    [Fact]
    public async Task Provisioning_requires_existing_account_and_development_environment()
    {
        using var factory = new SessionFactory();
        using var client = await TestAccounts.CreateAsync(factory, "dev@example.test");
        using var scope = factory.Services.CreateScope();
        await Assert.ThrowsAsync<InvalidOperationException>(() => OrganizerProvisioner.GrantAsync(scope.ServiceProvider, "missing@example.test"));
        scope.ServiceProvider.GetRequiredService<IHostEnvironment>().EnvironmentName = Environments.Production;
        await Assert.ThrowsAsync<InvalidOperationException>(() => OrganizerProvisioner.GrantAsync(scope.ServiceProvider, "dev@example.test"));
    }

    [Fact]
    public async Task Organizer_creates_and_updates_a_session_visible_in_public_listing()
    {
        using var factory = new SessionFactory();
        using var client = await Organizer(factory);
        using var created = await client.PostAsJsonAsync(Route, Valid());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var session = (await created.Content.ReadFromJsonAsync<OrganizerSessionDto>())!;
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(created.Headers.Location)).StatusCode);
        var edit = Copy(session);
        edit.Title = "Updated workshop";
        edit.StartsAtUtc = edit.StartsAtUtc.AddHours(1);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync(Route + session.Id, edit)).StatusCode);
        using var guest = factory.CreateClient();
        var visible = (await guest.GetFromJsonAsync<List<LearningSessionDto>>("/api/learning-sessions"))!.Single(s => s.Id == session.Id);
        Assert.Equal(edit.Title, visible.Title);
        Assert.Equal(edit.StartsAtUtc, visible.StartsAtUtc);
        Assert.Equal(1, (await client.GetFromJsonAsync<OrganizerSessionDto>(Route + session.Id))!.Version);
    }

    [Theory]
    [InlineData("title")]
    [InlineData("length")]
    [InlineData("capacity")]
    [InlineData("duration")]
    [InlineData("past")]
    [InlineData("utc")]
    [InlineData("missing-date")]
    public async Task Invalid_session_data_is_rejected_on_create_and_update(string invalid)
    {
        using var factory = new SessionFactory();
        using var client = await Organizer(factory);
        var input = Valid();
        switch (invalid)
        {
            case "title": input.Title = "   "; break;
            case "length": input.Description = new string('x', 2001); break;
            case "capacity": input.Capacity = 0; break;
            case "duration": input.DurationMinutes = 481; break;
            case "past": input.StartsAtUtc = DateTime.UtcNow.AddDays(-1); break;
            case "utc": input.StartsAtUtc = DateTime.SpecifyKind(input.StartsAtUtc, DateTimeKind.Unspecified); break;
            case "missing-date": input.StartsAtUtc = default; break;
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Route, input)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(Route + "1", input)).StatusCode);
        Assert.Equal(3, (await client.GetFromJsonAsync<List<OrganizerSessionDto>>(Route))!.Count);
    }

    [Fact]
    public async Task Booked_sessions_keep_schedule_and_cannot_shrink_below_bookings()
    {
        using var factory = new SessionFactory();
        using var organizer = await Organizer(factory);
        using var attendee = await TestAccounts.CreateAsync(factory);
        using var second = await TestAccounts.CreateAsync(factory);
        await attendee.PostAsync("/api/learning-sessions/1/registration", null);
        await second.PostAsync("/api/learning-sessions/1/registration", null);
        var session = (await organizer.GetFromJsonAsync<OrganizerSessionDto>(Route + "1"))!;
        var edit = Copy(session);
        edit.Capacity = 1;
        Assert.Equal(HttpStatusCode.Conflict, (await organizer.PutAsJsonAsync(Route + "1", edit)).StatusCode);
        edit = Copy(session);
        edit.StartsAtUtc = edit.StartsAtUtc.AddHours(1);
        Assert.Equal(HttpStatusCode.Conflict, (await organizer.PutAsJsonAsync(Route + "1", edit)).StatusCode);
        edit = Copy(session);
        edit.DurationMinutes++;
        Assert.Equal(HttpStatusCode.Conflict, (await organizer.PutAsJsonAsync(Route + "1", edit)).StatusCode);
        edit = Copy(session);
        edit.Title = "New title, same bookings";
        edit.Capacity = 2;
        Assert.Equal(HttpStatusCode.NoContent, (await organizer.PutAsJsonAsync(Route + "1", edit)).StatusCode);
        var saved = (await organizer.GetFromJsonAsync<OrganizerSessionDto>(Route + "1"))!;
        Assert.Equal(2, saved.RegisteredCount);
        Assert.Equal(2, saved.Capacity);
        Assert.Equal(session.StartsAtUtc, saved.StartsAtUtc);
    }

    [Fact]
    public async Task Concurrent_edits_using_same_version_cannot_overwrite_each_other()
    {
        using var factory = new SessionFactory();
        using var client = await Organizer(factory);
        var session = (await client.GetFromJsonAsync<OrganizerSessionDto>(Route + "1"))!;
        var first = Copy(session); first.Title = "First update";
        var second = Copy(session); second.Title = "Second update";
        var results = await Task.WhenAll(client.PutAsJsonAsync(Route + "1", first), client.PutAsJsonAsync(Route + "1", second));
        try
        {
            Assert.Single(results, r => r.StatusCode == HttpStatusCode.NoContent);
            Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict);
            Assert.Equal(1, (await client.GetFromJsonAsync<OrganizerSessionDto>(Route + "1"))!.Version);
        }
        finally { foreach (var result in results) result.Dispose(); }
    }

    [Fact]
    public async Task Concurrent_capacity_change_and_registration_preserve_capacity_invariant()
    {
        using var factory = new SessionFactory();
        using var organizer = await Organizer(factory);
        using var first = await TestAccounts.CreateAsync(factory);
        using var second = await TestAccounts.CreateAsync(factory);
        using var created = await organizer.PostAsJsonAsync(Route, Valid(capacity: 2));
        var session = (await created.Content.ReadFromJsonAsync<OrganizerSessionDto>())!;
        var bookingRoute = $"/api/learning-sessions/{session.Id}/registration";
        await first.PostAsync(bookingRoute, null);
        var edit = Copy(session); edit.Capacity = 1;
        var results = await Task.WhenAll(organizer.PutAsJsonAsync(Route + session.Id, edit), second.PostAsync(bookingRoute, null));
        try
        {
            Assert.Single(results, r => r.StatusCode == HttpStatusCode.NoContent);
            Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict);
            var saved = (await organizer.GetFromJsonAsync<OrganizerSessionDto>(Route + session.Id))!;
            Assert.True(saved.RegisteredCount <= saved.Capacity);
        }
        finally { foreach (var result in results) result.Dispose(); }
    }

    [Fact]
    public async Task Organizer_mutations_require_csrf_and_report_missing_sessions()
    {
        using var factory = new SessionFactory();
        using var client = await Organizer(factory);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync(Route + "999", Valid())).StatusCode);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Route, Valid())).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(Route + "1", Valid())).StatusCode);
    }

    [Fact]
    public async Task Started_sessions_are_read_only_even_if_proposed_start_is_future()
    {
        using var factory = new SessionFactory();
        using var client = await Organizer(factory);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SkillSnapDbContext>();
            (await db.LearningSessions.FindAsync(1))!.StartsAtUtc = DateTime.UtcNow.AddDays(-1);
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync(Route + "1", Valid())).StatusCode);
    }

    private static async Task<HttpClient> Organizer(SessionFactory factory)
    {
        var email = $"organizer-{Guid.NewGuid():N}@example.test";
        var client = await TestAccounts.CreateAsync(factory, email);
        using var scope = factory.Services.CreateScope();
        await OrganizerProvisioner.GrantAsync(scope.ServiceProvider, email);
        return client;
    }

    private static SessionUpdate Valid(int capacity = 20) => new()
    {
        Title = "C# workshop", Description = "A practical learning session.", Topic = "C#", Instructor = "Demo instructor",
        StartsAtUtc = DateTime.UtcNow.Date.AddDays(30).AddHours(18), DurationMinutes = 60, Capacity = capacity
    };

    private static SessionUpdate Copy(OrganizerSessionDto s) => new()
    {
        Title = s.Title, Description = s.Description, Topic = s.Topic, Instructor = s.Instructor,
        StartsAtUtc = s.StartsAtUtc, DurationMinutes = s.DurationMinutes, Capacity = s.Capacity, Version = s.Version
    };
}
