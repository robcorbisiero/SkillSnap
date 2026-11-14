using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using SkillSnap.Api.Data;
using SkillSnap.Contracts;

namespace SkillSnap.Api.Endpoints;

public static class OrganizerEndpoints
{
    public static void MapOrganizerEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/organizer/learning-sessions").RequireAuthorization("Organizer");
        group.MapGet("/{id:int}", async (int id, HttpResponse response, SkillSnapDbContext db, CancellationToken ct) =>
        {
            response.Headers.CacheControl = "no-store";
            var session = await db.LearningSessions.AsNoTracking().Where(s => s.Id == id)
                .Select(s => new OrganizerSessionDto(s.Id, s.Title, s.Description, s.Topic, s.Instructor,
                    s.StartsAtUtc, s.DurationMinutes, s.Capacity,
                    db.Registrations.Count(r => r.LearningSessionId == s.Id), s.Version)).SingleOrDefaultAsync(ct);
            return session is null ? Results.NotFound() : Results.Ok(session);
        });
        group.MapGet("/", async (HttpResponse response, SkillSnapDbContext db, CancellationToken ct) =>
        {
            response.Headers.CacheControl = "no-store";
            return await db.LearningSessions.AsNoTracking().OrderBy(s => s.StartsAtUtc).ThenBy(s => s.Id)
                .Select(s => new OrganizerSessionDto(s.Id, s.Title, s.Description, s.Topic, s.Instructor,
                    s.StartsAtUtc, s.DurationMinutes, s.Capacity,
                    db.Registrations.Count(r => r.LearningSessionId == s.Id), s.Version)).ToListAsync(ct);
        });
        group.MapPost("/", CreateAsync).AddEndpointFilter<CsrfFilter>();
        group.MapPut("/{id:int}", UpdateAsync).AddEndpointFilter<CsrfFilter>();
    }

    private static async Task<IResult> CreateAsync(SessionInput input, SkillSnapDbContext db, CancellationToken ct)
    {
        var errors = Validate(input);
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var session = new LearningSession
        {
            Title = input.Title.Trim(), Description = input.Description.Trim(), Topic = input.Topic.Trim(),
            Instructor = input.Instructor.Trim(), StartsAtUtc = input.StartsAtUtc,
            DurationMinutes = input.DurationMinutes, Capacity = input.Capacity
        };
        db.LearningSessions.Add(session);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/organizer/learning-sessions/{session.Id}", new OrganizerSessionDto(
            session.Id, session.Title, session.Description, session.Topic, session.Instructor,
            session.StartsAtUtc, session.DurationMinutes, session.Capacity, 0, session.Version));
    }

    private static async Task<IResult> UpdateAsync(int id, SessionUpdate input, SkillSnapDbContext db, CancellationToken ct)
    {
        var errors = Validate(input);
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var title = input.Title.Trim();
        var description = input.Description.Trim();
        var topic = input.Topic.Trim();
        var instructor = input.Instructor.Trim();
        var now = DateTime.UtcNow;
        // Capacity, schedule protection, and version comparison share one SQLite write.
        // A concurrent registration or edit cannot slip between a check and an update.
        var changed = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "LearningSessions" AS s
            SET "Title" = {title}, "Description" = {description}, "Topic" = {topic},
                "Instructor" = {instructor}, "StartsAtUtc" = {input.StartsAtUtc},
                "DurationMinutes" = {input.DurationMinutes}, "Capacity" = {input.Capacity}, "Version" = "Version" + 1
            WHERE s."Id" = {id} AND s."Version" = {input.Version} AND s."StartsAtUtc" > {now}
              AND (SELECT COUNT(*) FROM "Registrations" WHERE "LearningSessionId" = s."Id") <= {input.Capacity}
              AND (NOT EXISTS (SELECT 1 FROM "Registrations" WHERE "LearningSessionId" = s."Id")
                   OR (s."StartsAtUtc" = {input.StartsAtUtc} AND s."DurationMinutes" = {input.DurationMinutes}))
            """, ct);
        if (changed == 1) return Results.NoContent();
        if (!await db.LearningSessions.AnyAsync(s => s.Id == id, ct)) return Results.NotFound();
        return Results.Problem(statusCode: 409, title: "Session could not be updated.",
            detail: "Reload the latest session. Another organizer may have edited it, capacity may be below current bookings, or its schedule is locked by bookings or its start time.");
    }

    private static Dictionary<string, string[]> Validate(SessionInput input)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(input, new ValidationContext(input), results, validateAllProperties: true);
        return results.SelectMany(r => r.MemberNames.DefaultIfEmpty("session").Select(name => (name, r.ErrorMessage)))
            .GroupBy(r => r.name).ToDictionary(g => g.Key, g => g.Select(r => r.ErrorMessage ?? "Invalid value.").ToArray());
    }
}
