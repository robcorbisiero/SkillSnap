using System.Security.Cryptography;
using System.Text;
using System.Security.Claims;
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
        app.MapPost("/api/learning-sessions/{id:int}/registration", RegisterAsync).RequireAuthorization().AddEndpointFilter<CsrfFilter>();
        app.MapDelete("/api/learning-sessions/{id:int}/registration", CancelAsync).RequireAuthorization().AddEndpointFilter<CsrfFilter>();
        app.MapGet("/api/registrations/legacy-count", LegacyCountAsync).RequireAuthorization();
        app.MapPost("/api/registrations/import", ImportAsync).RequireAuthorization().AddEndpointFilter<CsrfFilter>();
    }

    private static async Task<IResult> ListAsync(ClaimsPrincipal user, HttpResponse response, SkillSnapDbContext db, CancellationToken ct)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        response.Headers.CacheControl = "no-store";
        var sessions = await db.LearningSessions.AsNoTracking()
            .OrderBy(s => s.StartsAtUtc).ThenBy(s => s.Id)
            .Select(s => new LearningSessionDto(s.Id, s.Title, s.Description, s.Topic,
                s.Instructor, s.StartsAtUtc, s.DurationMinutes, s.Capacity,
                db.Registrations.Count(r => r.LearningSessionId == s.Id),
                userId != null && db.Registrations.Any(r => r.LearningSessionId == s.Id && r.UserId == userId),
                userId == null ? null : db.Registrations.Where(r => r.LearningSessionId == s.Id && r.UserId == userId)
                    .Select(r => (AttendanceStatus?)r.AttendanceStatus).FirstOrDefault()))
            .ToListAsync(ct);
        return Results.Ok(sessions);
    }

    private static async Task<IResult> RegisterAsync(int id, ClaimsPrincipal user, SkillSnapDbContext db, CancellationToken ct)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var now = DateTime.UtcNow;
        // One SQLite write statement serializes the capacity check and insert.
        // Parameters are bound by EF; no user input is concatenated into SQL.
        var inserted = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Registrations" ("LearningSessionId", "UserId")
            SELECT "Id", {userId} FROM "LearningSessions" AS s
            WHERE s."Id" = {id} AND s."StartsAtUtc" > {now}
              AND (SELECT COUNT(*) FROM "Registrations" WHERE "LearningSessionId" = s."Id") < s."Capacity"
              AND NOT EXISTS (SELECT 1 FROM "Registrations" WHERE "LearningSessionId" = s."Id" AND "UserId" = {userId})
            """, ct);
        if (inserted == 1) return Results.NoContent();
        if (await db.Registrations.AnyAsync(r => r.LearningSessionId == id && r.UserId == userId, ct))
            return Results.NoContent(); // A retry must not consume another seat.
        var session = await db.LearningSessions.FindAsync([id], ct);
        if (session is null) return Results.NotFound();
        return Results.Problem(statusCode: StatusCodes.Status409Conflict,
            title: session.StartsAtUtc <= now ? "This session has already started." : "This session is full.");
    }

    private static async Task<IResult> CancelAsync(int id, ClaimsPrincipal user, SkillSnapDbContext db, CancellationToken ct)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (!await db.LearningSessions.AnyAsync(s => s.Id == id, ct)) return Results.NotFound();
        await db.Registrations.Where(r => r.LearningSessionId == id && r.UserId == userId && r.AttendanceStatus == AttendanceStatus.NotRecorded).ExecuteDeleteAsync(ct);
        if (await db.Registrations.AnyAsync(r => r.LearningSessionId == id && r.UserId == userId && r.AttendanceStatus != AttendanceStatus.NotRecorded, ct))
            return Results.Problem(statusCode: 409, title: "Attendance is recorded. Ask an organizer to clear it before cancelling.");
        return Results.NoContent();
    }

    private static async Task<IResult> LegacyCountAsync(HttpRequest request, HttpResponse response, SkillSnapDbContext db, CancellationToken ct)
    {
        response.Headers.CacheControl = "no-store";
        if (!request.Headers.ContainsKey(ParticipantHeader)) return Results.Ok(new LegacyBookingCount(0));
        var hash = ParticipantHash(request);
        if (hash is null) return InvalidKey();
        return Results.Ok(new LegacyBookingCount(await db.Registrations.CountAsync(r => r.ParticipantHash == hash && r.UserId == null, ct)));
    }

    private static async Task<IResult> ImportAsync(HttpRequest request, ClaimsPrincipal user, SkillSnapDbContext db, CancellationToken ct)
    {
        var hash = ParticipantHash(request);
        if (hash is null) return InvalidKey();
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // An account might already hold a seat in a legacy session. Merge, never double-book.
        await db.Registrations.Where(r => r.ParticipantHash == hash && r.UserId == null &&
            db.Registrations.Any(owned => owned.LearningSessionId == r.LearningSessionId && owned.UserId == userId)).ExecuteDeleteAsync(ct);
        var imported = await db.Registrations.Where(r => r.ParticipantHash == hash && r.UserId == null)
            .ExecuteUpdateAsync(update => update.SetProperty(r => r.UserId, userId)
                .SetProperty(r => r.ParticipantHash, (string?)null), ct);
        await transaction.CommitAsync(ct);
        return Results.Ok(new LegacyBookingCount(imported));
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
