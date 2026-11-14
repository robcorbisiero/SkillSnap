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
using SkillSnap.Contracts;

namespace SkillSnap.Api.Tests;

public sealed class AccountEndpointTests
{
    private const string Route = "/api/learning-sessions/1/registration";

    [Fact]
    public async Task Anonymous_csrf_token_cannot_be_reused_after_login()
    {
        using var factory = new SessionFactory();
        using var client = factory.CreateClient();
        await TestAccounts.CsrfAsync(client);
        var oldToken = client.DefaultRequestHeaders.GetValues("X-CSRF-TOKEN").Single();
        await client.PostAsJsonAsync("/api/auth/register", Credentials("token@example.test"));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync(Route, null)).StatusCode);
        await TestAccounts.CsrfAsync(client);
        Assert.NotEqual(oldToken, client.DefaultRequestHeaders.GetValues("X-CSRF-TOKEN").Single());
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync(Route, null)).StatusCode);
    }

    [Fact]
    public async Task Account_attempts_are_rate_limited()
    {
        using var factory = new SessionFactory();
        using var client = factory.CreateClient();
        await TestAccounts.CsrfAsync(client);
        for (var i = 0; i < 10; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", Credentials("missing@example.test"))).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/api/auth/login", Credentials("missing@example.test"))).StatusCode);
    }

    [Fact]
    public async Task Signup_hashes_password_issues_httponly_cookie_and_logout_removes_access()
    {
        using var factory = new SessionFactory();
        using var client = factory.CreateClient();
        await TestAccounts.CsrfAsync(client);
        const string email = "account@example.test";
        using var signup = await client.PostAsJsonAsync("/api/auth/register", Credentials(email));
        Assert.Equal(HttpStatusCode.OK, signup.StatusCode);
        var cookie = signup.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("SkillSnap.Auth="));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(email, (await client.GetFromJsonAsync<AccountInfo>("/api/auth/me"))!.Email);
        using (var scope = factory.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var user = await manager.FindByEmailAsync(email);
            Assert.NotEqual(TestAccounts.Password, user!.PasswordHash);
            Assert.True(await manager.CheckPasswordAsync(user, TestAccounts.Password));
        }
        await TestAccounts.CsrfAsync(client);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync(Route, null)).StatusCode);
    }

    [Fact]
    public async Task Same_account_in_another_client_can_manage_its_existing_booking()
    {
        using var factory = new SessionFactory();
        const string email = "same@example.test";
        using var first = await TestAccounts.CreateAsync(factory, email);
        Assert.Equal(HttpStatusCode.NoContent, (await first.PostAsync(Route, null)).StatusCode);
        using var second = factory.CreateClient();
        await TestAccounts.CsrfAsync(second);
        Assert.Equal(HttpStatusCode.OK, (await second.PostAsJsonAsync("/api/auth/login", Credentials(email))).StatusCode);
        Assert.True((await Sessions(second)).Single(s => s.Id == 1).IsRegistered);
        await TestAccounts.CsrfAsync(second);
        await second.DeleteAsync(Route);
        Assert.False((await Sessions(first)).Single(s => s.Id == 1).IsRegistered);
    }

    [Fact]
    public async Task Wrong_password_and_nonexistent_account_have_same_status_and_lockout_is_enforced()
    {
        using var factory = new SessionFactory();
        using var owner = await TestAccounts.CreateAsync(factory, "locked@example.test");
        using var client = factory.CreateClient();
        await TestAccounts.CsrfAsync(client);
        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", Credentials("locked@example.test", "Wrong-Password42!"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", Credentials("locked@example.test"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", Credentials("missing@example.test"))).StatusCode);
    }

    [Theory]
    [InlineData("not-email", "Valid-Password42!")]
    [InlineData("weak@example.test", "short")]
    [InlineData("weak@example.test", "alllowercasepassword")]
    public async Task Signup_validates_email_and_password(string email, string password)
    {
        using var factory = new SessionFactory();
        using var client = factory.CreateClient();
        await TestAccounts.CsrfAsync(client);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/register", Credentials(email, password))).StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<SkillSnapDbContext>().Users.ToListAsync());
    }

    [Fact]
    public async Task Duplicate_signup_does_not_sign_in_or_create_another_user()
    {
        using var factory = new SessionFactory();
        using var owner = await TestAccounts.CreateAsync(factory, "duplicate@example.test");
        using var other = factory.CreateClient();
        await TestAccounts.CsrfAsync(other);
        Assert.Equal(HttpStatusCode.BadRequest, (await other.PostAsJsonAsync("/api/auth/register", Credentials("duplicate@example.test"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await other.GetAsync("/api/auth/me")).StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Single(await scope.ServiceProvider.GetRequiredService<SkillSnapDbContext>().Users.ToListAsync());
    }

    [Fact]
    public async Task Csrf_is_required_for_signup_login_logout_and_bookings()
    {
        using var factory = new SessionFactory();
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await anonymous.PostAsJsonAsync("/api/auth/register", Credentials("csrf@example.test"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await anonymous.PostAsJsonAsync("/api/auth/login", Credentials("csrf@example.test"))).StatusCode);
        using var owner = await TestAccounts.CreateAsync(factory);
        owner.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsync(Route, null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.DeleteAsync(Route)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsync("/api/auth/logout", null)).StatusCode);
    }

    [Fact]
    public async Task Legacy_bookings_require_explicit_import_and_possession_of_original_browser_key()
    {
        using var factory = new SessionFactory();
        using var owner = await TestAccounts.CreateAsync(factory);
        var key = Guid.NewGuid();
        await SeedLegacy(factory, key, 1);
        Assert.False((await Sessions(owner)).Single(s => s.Id == 1).IsRegistered);
        owner.DefaultRequestHeaders.Add("X-Participant-Key", Guid.NewGuid().ToString("N"));
        Assert.Equal(0, (await owner.GetFromJsonAsync<LegacyBookingCount>("/api/registrations/legacy-count"))!.Count);
        await owner.PostAsync("/api/registrations/import", null);
        Assert.False((await Sessions(owner)).Single(s => s.Id == 1).IsRegistered);
        owner.DefaultRequestHeaders.Remove("X-Participant-Key");
        owner.DefaultRequestHeaders.Add("X-Participant-Key", key.ToString("D"));
        Assert.Equal(1, (await owner.GetFromJsonAsync<LegacyBookingCount>("/api/registrations/legacy-count"))!.Count);
        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsync("/api/registrations/import", null)).StatusCode);
        Assert.True((await Sessions(owner)).Single(s => s.Id == 1).IsRegistered);
        using var other = await TestAccounts.CreateAsync(factory);
        other.DefaultRequestHeaders.Add("X-Participant-Key", key.ToString("N"));
        await other.PostAsync("/api/registrations/import", null);
        Assert.False((await Sessions(other)).Single(s => s.Id == 1).IsRegistered);
    }

    [Fact]
    public async Task Import_merges_duplicate_sessions_without_consuming_an_extra_seat()
    {
        using var factory = new SessionFactory();
        using var owner = await TestAccounts.CreateAsync(factory);
        await owner.PostAsync(Route, null);
        var key = Guid.NewGuid();
        await SeedLegacy(factory, key, 1);
        owner.DefaultRequestHeaders.Add("X-Participant-Key", key.ToString("N"));
        Assert.Equal(2, (await Sessions(owner)).Single(s => s.Id == 1).RegisteredCount);
        await owner.PostAsync("/api/registrations/import", null);
        await owner.PostAsync("/api/registrations/import", null);
        Assert.Equal(1, (await Sessions(owner)).Single(s => s.Id == 1).RegisteredCount);
    }

    [Fact]
    public async Task Milestone_two_upgrade_preserves_legacy_registration_until_claimed()
    {
        var path = Path.Combine(Path.GetTempPath(), $"skillsnap-{Guid.NewGuid():N}.db");
        var key = Guid.NewGuid();
        try
        {
            var options = new DbContextOptionsBuilder<SkillSnapDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options;
            await using (var old = new SkillSnapDbContext(options))
            {
                await old.GetService<IMigrator>().MigrateAsync("20261114210344_AddRegistrations");
                var hash = Hash(key);
                await old.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Registrations (LearningSessionId,ParticipantHash) VALUES (1,{hash})");
            }
            using var upgraded = new SessionFactory(path);
            using var client = await TestAccounts.CreateAsync(upgraded);
            client.DefaultRequestHeaders.Add("X-Participant-Key", key.ToString("N"));
            Assert.Equal(1, (await client.GetFromJsonAsync<LegacyBookingCount>("/api/registrations/legacy-count"))!.Count);
            await client.PostAsync("/api/registrations/import", null);
            Assert.True((await Sessions(client)).Single(s => s.Id == 1).IsRegistered);
        }
        finally { File.Delete(path); }
    }

    private static AccountCredentials Credentials(string email, string password = TestAccounts.Password) => new() { Email = email, Password = password };
    private static async Task<List<LearningSessionDto>> Sessions(HttpClient client) =>
        (await client.GetFromJsonAsync<List<LearningSessionDto>>("/api/learning-sessions"))!;
    private static string Hash(Guid key) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key.ToString("N"))));
    private static async Task SeedLegacy(SessionFactory factory, Guid key, int sessionId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SkillSnapDbContext>();
        db.Registrations.Add(new Registration { LearningSessionId = sessionId, ParticipantHash = Hash(key) });
        await db.SaveChangesAsync();
    }
}
