using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using SkillSnap.Api.Data;
using SkillSnap.Contracts;

namespace SkillSnap.Api.Tests;

public sealed class RegistrationEndpointTests
{
    private const string Route = "/api/learning-sessions/1/registration";

    [Fact]
    public async Task Milestone_one_database_upgrades_without_losing_existing_sessions()
    {
        var path = Path.Combine(Path.GetTempPath(), $"skillsnap-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<SkillSnapDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options;
            await using (var old = new SkillSnapDbContext(options))
            {
                await old.GetService<IMigrator>().MigrateAsync("20261114205304_InitialCreate");
                var row = await old.LearningSessions.FindAsync(1);
                row!.Title = "My existing session";
                await old.SaveChangesAsync();
            }
            using var upgraded = new SessionFactory(path);
            using var client = Participant(upgraded);
            Assert.Equal("My existing session", (await First(client)).Title);
            await ConfigureSession(upgraded);
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync(Route, null)).StatusCode);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Register_is_idempotent_and_cancel_frees_seat()
    {
        using var factory = new SessionFactory();
        using var client = Participant(factory);
        await ConfigureSession(factory, capacity: 2);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync(Route, null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync(Route, null)).StatusCode);
        var session = await First(client);
        Assert.True(session.IsRegistered);
        Assert.Equal(1, session.RegisteredCount);
        Assert.Equal(1, session.SeatsRemaining);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(Route)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(Route)).StatusCode);
        session = await First(client);
        Assert.False(session.IsRegistered);
        Assert.Equal(2, session.SeatsRemaining);
    }

    [Fact]
    public async Task Another_participant_cannot_cancel_or_claim_a_registration()
    {
        using var factory = new SessionFactory();
        using var owner = Participant(factory);
        using var other = Participant(factory);
        await ConfigureSession(factory);
        await owner.PostAsync(Route, null);
        Assert.False((await First(other)).IsRegistered);
        await other.DeleteAsync(Route);
        Assert.True((await First(owner)).IsRegistered);
        Assert.Equal(1, (await First(other)).RegisteredCount);
        using var anonymous = factory.CreateClient();
        Assert.False((await First(anonymous)).IsRegistered);
        Assert.DoesNotContain("ParticipantHash", await anonymous.GetStringAsync("/api/learning-sessions"), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("invalid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task Anonymous_mutations_are_rejected_regardless_of_legacy_key(string? key)
    {
        using var factory = new SessionFactory();
        using var client = factory.CreateClient();
        if (key is not null) client.DefaultRequestHeaders.Add("X-Participant-Key", key);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync(Route, null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.DeleteAsync(Route)).StatusCode);
    }

    [Fact]
    public async Task Full_session_rejects_new_registration_and_cancellation_makes_space()
    {
        using var factory = new SessionFactory();
        using var first = Participant(factory);
        using var second = Participant(factory);
        await ConfigureSession(factory, capacity: 1);
        await first.PostAsync(Route, null);
        using var full = await second.PostAsync(Route, null);
        Assert.Equal(HttpStatusCode.Conflict, full.StatusCode);
        Assert.Contains("full", await full.Content.ReadAsStringAsync());
        await first.DeleteAsync(Route);
        Assert.Equal(HttpStatusCode.NoContent, (await second.PostAsync(Route, null)).StatusCode);
    }

    [Fact]
    public async Task Concurrent_requests_cannot_overbook_last_seat()
    {
        using var factory = new SessionFactory();
        using var first = Participant(factory);
        using var second = Participant(factory);
        await ConfigureSession(factory, capacity: 1);
        var responses = await Task.WhenAll(first.PostAsync(Route, null), second.PostAsync(Route, null));
        try
        {
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.NoContent);
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
            Assert.Equal(1, (await First(first)).RegisteredCount);
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Fact]
    public async Task Concurrent_duplicate_requests_only_create_one_registration()
    {
        using var factory = new SessionFactory();
        using var client = Participant(factory);
        await ConfigureSession(factory);
        var responses = await Task.WhenAll(client.PostAsync(Route, null), client.PostAsync(Route, null));
        try
        {
            Assert.All(responses, r => Assert.Equal(HttpStatusCode.NoContent, r.StatusCode));
            Assert.Equal(1, (await First(client)).RegisteredCount);
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Fact]
    public async Task Started_and_missing_sessions_cannot_be_booked()
    {
        using var factory = new SessionFactory();
        using var client = Participant(factory);
        await ConfigureSession(factory, started: true);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync(Route, null)).StatusCode);
        const string missing = "/api/learning-sessions/999/registration";
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync(missing, null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync(missing)).StatusCode);
    }

    [Fact]
    public async Task Registration_survives_restart_and_belongs_to_account()
    {
        var path = Path.Combine(Path.GetTempPath(), $"skillsnap-{Guid.NewGuid():N}.db");
        var key = Guid.NewGuid().ToString("N");
        try
        {
            using (var first = new SessionFactory(path))
            {
                using var client = Participant(first, key);
                await ConfigureSession(first);
                await client.PostAsync(Route, null);
                using var scope = first.Services.CreateScope();
                var row = await scope.ServiceProvider.GetRequiredService<SkillSnapDbContext>().Registrations.SingleAsync();
                Assert.Null(row.ParticipantHash);
                Assert.False(string.IsNullOrWhiteSpace(row.UserId));
            }
            using var second = new SessionFactory(path);
            using var restored = Participant(second, key);
            Assert.True((await First(restored)).IsRegistered);
            Assert.Equal(HttpStatusCode.NoContent, (await restored.DeleteAsync(Route)).StatusCode);
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("DELETE")]
    public async Task Browser_preflight_allows_registration_methods_and_key_header(string method)
    {
        using var factory = new SessionFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, Route);
        request.Headers.Add("Origin", "http://localhost:5180");
        request.Headers.Add("Access-Control-Request-Method", method);
        request.Headers.Add("Access-Control-Request-Headers", "x-csrf-token");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Contains(method, response.Headers.GetValues("Access-Control-Allow-Methods").Single());
        Assert.Contains("x-csrf-token", response.Headers.GetValues("Access-Control-Allow-Headers").Single());
        Assert.Equal("true", response.Headers.GetValues("Access-Control-Allow-Credentials").Single());
    }

    private static HttpClient Participant(SessionFactory factory, string? key = null)
    {
        return TestAccounts.CreateAsync(factory, key is null ? null : $"{key}@example.test").GetAwaiter().GetResult();
    }

    private static async Task<LearningSessionDto> First(HttpClient client) =>
        (await client.GetFromJsonAsync<List<LearningSessionDto>>("/api/learning-sessions"))!.Single(s => s.Id == 1);

    private static async Task ConfigureSession(SessionFactory factory, int capacity = 20, bool started = false)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SkillSnapDbContext>();
        var session = await db.LearningSessions.FindAsync(1);
        session!.Capacity = capacity;
        session.StartsAtUtc = DateTime.UtcNow.AddDays(started ? -1 : 1);
        await db.SaveChangesAsync();
    }
}
