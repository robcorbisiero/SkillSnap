using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SkillSnap.Api.Data;
using SkillSnap.Contracts;

namespace SkillSnap.Api.Endpoints;

public static class AttendanceEndpoints
{
    public static void MapAttendanceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/organizer/learning-sessions/{id:int}/attendance").RequireAuthorization("Organizer");
        group.MapGet("/", GetAsync);
        group.MapPut("/{registrationId:int}", UpdateAsync).AddEndpointFilter<CsrfFilter>();
    }

    private static async Task<IResult> GetAsync(int id, HttpResponse response, SkillSnapDbContext db, CancellationToken ct)
    {
        response.Headers.CacheControl = "no-store";
        var session = await db.LearningSessions.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id, ct);
        if (session is null) return Results.NotFound();
        var entries = await (from registration in db.Registrations.AsNoTracking()
            where registration.LearningSessionId == id
            join user in db.Users on registration.UserId equals user.Id into users
            from user in users.DefaultIfEmpty()
            orderby registration.Id
            select new AttendanceEntryDto(registration.Id, user == null ? null : user.Email,
                registration.AttendanceStatus, registration.AttendanceVersion,
                registration.AttendanceUpdatedAtUtc, registration.UserId == null)).ToListAsync(ct);
        return Results.Ok(new AttendanceRosterDto(id, session.Title, session.StartsAtUtc, entries));
    }

    private static async Task<IResult> UpdateAsync(int id, int registrationId, AttendanceUpdate input,
        ClaimsPrincipal user, SkillSnapDbContext db, CancellationToken ct)
    {
        AttendanceStatus? status = input.Status switch
        {
            "NotRecorded" => AttendanceStatus.NotRecorded,
            "Attended" => AttendanceStatus.Attended,
            "Absent" => AttendanceStatus.Absent,
            _ => null
        };
        if (status is null || input.Version < 0)
            return Results.Problem(statusCode: 400, title: "Use a valid attendance status and nonnegative version.");
        var value = (int)status.Value;
        var now = DateTime.UtcNow;
        var actorId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
        // One version-checked write also protects a concurrent cancellation.
        var changed = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Registrations"
            SET "AttendanceStatus" = {value}, "AttendanceVersion" = "AttendanceVersion" + 1,
                "AttendanceUpdatedAtUtc" = {now}, "AttendanceUpdatedByUserId" = {actorId}
            WHERE "Id" = {registrationId} AND "LearningSessionId" = {id}
              AND "UserId" IS NOT NULL AND "AttendanceVersion" = {input.Version}
            """, ct);
        if (changed == 1) return Results.NoContent();
        var registration = await db.Registrations.AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == registrationId && r.LearningSessionId == id, ct);
        if (registration is null) return Results.NotFound();
        return Results.Problem(statusCode: 409, title: registration.UserId is null
            ? "Import this browser booking into an account before recording attendance."
            : "Attendance changed since you loaded it. Refresh the roster before trying again.");
    }
}
