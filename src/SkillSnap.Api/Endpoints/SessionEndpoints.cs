using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SkillSnap.Api.Data;
using SkillSnap.Contracts;

namespace SkillSnap.Api.Endpoints;

public static class SessionEndpoints
{
    public const string ParticipantHeader = "X-Participant-Key";

    public static void MapSessionEndpoints(this WebApplication app)
    {
        app.MapGet("/api/learning-sessions", ListAsync);
        app.MapPost("/api/learning-sessions/{id:int}/registration", RegisterAsync);
        app.MapDelete("/api/learning-sessions/{id:int}/registration", CancelAsync);
    }

    private static async Task<IResult> ListAsync(HttpRequest request, SkillSnapDbContext db, CancellationToken ct)
    {
        var hash = ParticipantHash(request);
        if (request.Headers.ContainsKey(ParticipantHeader) && hash is null) return InvalidKey();
        var sessions = await db.LearningSessions.AsNoTracking()
            .OrderBy(s => s.StartsAtUtc).ThenBy(s => s.Id)
            .Select(s => new LearningSessionDto(s.Id, s.Title, s.Description, s.Topic,
                s.Instructor, s.StartsAtUtc, s.DurationMinutes, s.Capacity,
                db.Registrations.Count(r => r.LearningSessionId == s.Id),
                hash != null && db.Registrations.Any(r => r.LearningSessionId == s.Id && r.ParticipantHash == hash)))
            .ToListAsync(ct);
        return Results.Ok(sessions);
    }

    private static async Task<IResult> RegisterAsync(int id, HttpRequest request, SkillSnapDbContext db, CancellationToken ct)
    {
        var hash = ParticipantHash(request);
        if (hash is null) return InvalidKey();
        var now = DateTime.UtcNow;
        // One SQLite write statement serializes the capacity check and insert.
        // Parameters are bound by EF; no user input is concatenated into SQL.
        var inserted = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Registrations" ("LearningSessionId", "ParticipantHash")
            SELECT "Id", {hash} FROM "LearningSessions" AS s
            WHERE s."Id" = {id} AND s."StartsAtUtc" > {now}
              AND (SELECT COUNT(*) FROM "Registrations" WHERE "LearningSessionId" = s."Id") < s."Capacity"
              AND NOT EXISTS (SELECT 1 FROM "Registrations" WHERE "LearningSessionId" = s."Id" AND "ParticipantHash" = {hash})
            """, ct);
        if (inserted == 1) return Results.NoContent();
        if (await db.Registrations.AnyAsync(r => r.LearningSessionId == id && r.ParticipantHash == hash, ct))
            return Results.NoContent(); // A retry must not consume another seat.
        var session = await db.LearningSessions.FindAsync([id], ct);
        if (session is null) return Results.NotFound();
        return Results.Problem(statusCode: StatusCodes.Status409Conflict,
            title: session.StartsAtUtc <= now ? "This session has already started." : "This session is full.");
    }

    private static async Task<IResult> CancelAsync(int id, HttpRequest request, SkillSnapDbContext db, CancellationToken ct)
    {
        var hash = ParticipantHash(request);
        if (hash is null) return InvalidKey();
        if (!await db.LearningSessions.AnyAsync(s => s.Id == id, ct)) return Results.NotFound();
        await db.Registrations.Where(r => r.LearningSessionId == id && r.ParticipantHash == hash).ExecuteDeleteAsync(ct);
        return Results.NoContent();
    }

    private static string? ParticipantHash(HttpRequest request)
    {
        var values = request.Headers[ParticipantHeader];
        if (values.Count != 1 || !Guid.TryParse(values[0], out var key) || key == Guid.Empty) return null;
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key.ToString("N"))));
    }

    private static IResult InvalidKey() => Results.ValidationProblem(new Dictionary<string, string[]>
    {
        [ParticipantHeader] = ["A single, nonempty UUID participant key is required."]
    });
}
