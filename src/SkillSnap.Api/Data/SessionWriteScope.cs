using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace SkillSnap.Api.Data;

public static class SessionWriteScope
{
    public static async Task<IDbContextTransaction?> AcquireAsync(SkillSnapDbContext db, int id, CancellationToken ct)
    {
        if (!db.Database.IsNpgsql()) return null; // SQLite's conditional writes already serialize.
        var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            // The following write gets a fresh READ COMMITTED snapshot after obtaining this row lock.
            // Registrations and capacity edits take the same session lock before checking counts.
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"LearningSessions\" WHERE \"Id\" = {id} FOR UPDATE", ct);
            return transaction;
        }
        catch { await transaction.DisposeAsync(); throw; }
    }
}
