namespace SkillSnap.Contracts;

public sealed record LearningSessionDto(
    int Id, string Title, string Description, string Topic,
    string Instructor, DateTime StartsAtUtc, int DurationMinutes, int Capacity,
    int RegisteredCount = 0, bool IsRegistered = false, AttendanceStatus? AttendanceStatus = null)
{
    public int SeatsRemaining => Math.Max(0, Capacity - RegisteredCount);
}
