using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SkillSnap.Api.Data;
using SkillSnap.Contracts;

namespace SkillSnap.Api.Tests;

public sealed class SessionEndpointTests
{
    [Fact]
    public async Task Fresh_database_is_migrated_seeded_and_returned_as_ordered_utc_json()
    {
        using var factory = new SessionFactory();
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/learning-sessions");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var sessions = await response.Content.ReadFromJsonAsync<List<LearningSessionDto>>();
        Assert.NotNull(sessions);
        Assert.Equal(3, sessions.Count);
        Assert.Equal([1, 2, 3], sessions.Select(s => s.Id));
        Assert.All(sessions, s => Assert.Equal(DateTimeKind.Utc, s.StartsAtUtc.Kind));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SkillSnapDbContext>();
        Assert.Equal(5, (await db.Database.GetAppliedMigrationsAsync()).Count());
    }

    [Fact]
    public async Task Empty_database_returns_empty_array()
    {
        using var factory = new SessionFactory();
        using var client = factory.CreateClient();
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<SkillSnapDbContext>().LearningSessions.ExecuteDeleteAsync();
        Assert.Equal("[]", await client.GetStringAsync("/api/learning-sessions"));
    }

    [Fact]
    public async Task Endpoint_reads_persisted_rows_in_date_order_with_id_tiebreaker()
    {
        using var factory = new SessionFactory();
        using var client = factory.CreateClient();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SkillSnapDbContext>();
            await db.LearningSessions.ExecuteDeleteAsync();
            db.AddRange(Row(30, 3), Row(20, 1), Row(10, 1));
            await db.SaveChangesAsync();
        }
        var sessions = await client.GetFromJsonAsync<List<LearningSessionDto>>("/api/learning-sessions");
        Assert.Equal([10, 20, 30], sessions!.Select(s => s.Id));
    }

    [Theory]
    [InlineData("http://localhost:5180", true)]
    [InlineData("https://example.com", false)]
    public async Task Cors_only_allows_configured_frontend(string origin, bool allowed)
    {
        using var factory = new SessionFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/learning-sessions");
        request.Headers.Add("Origin", origin);
        using var response = await client.SendAsync(request);
        Assert.Equal(allowed, response.Headers.Contains("Access-Control-Allow-Origin"));
        if (allowed) Assert.Equal(origin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }

    [Fact]
    public async Task Restart_preserves_data_and_does_not_reseed_or_duplicate()
    {
        var path = Path.Combine(Path.GetTempPath(), $"skillsnap-{Guid.NewGuid():N}.db");
        try
        {
            using (var first = new SessionFactory(path))
            {
                using var client = first.CreateClient();
                using var scope = first.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<SkillSnapDbContext>();
                db.Add(Row(10, 1));
                await db.SaveChangesAsync();
            }
            using var second = new SessionFactory(path);
            using var secondClient = second.CreateClient();
            var sessions = await secondClient.GetFromJsonAsync<List<LearningSessionDto>>("/api/learning-sessions");
            Assert.Equal(4, sessions!.Count);
            Assert.Contains(sessions, s => s.Id == 10);
        }
        finally { File.Delete(path); }
    }

    private static LearningSession Row(int id, int day) => new()
    {
        Id = id, Title = "Persisted test session", Description = "Stored in real SQLite",
        Topic = "Testing", Instructor = "Test instructor", Capacity = 10, DurationMinutes = 60,
        StartsAtUtc = new DateTime(2027, 2, day, 12, 0, 0, DateTimeKind.Utc)
    };
}

internal sealed class SessionFactory(string? databasePath = null) : WebApplicationFactory<Program>
{
    private readonly string path = databasePath ?? Path.Combine(Path.GetTempPath(), $"skillsnap-{Guid.NewGuid():N}.db");
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<SkillSnapDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<SkillSnapDbContext>>();
            services.AddDbContext<SkillSnapDbContext>(options => options.UseSqlite($"Data Source={path};Pooling=False"));
        });
    }
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && databasePath is null) File.Delete(path);
    }
}
